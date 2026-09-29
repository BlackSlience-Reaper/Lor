using System;
using System.Reflection;
using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using Range = Godot.Range;

namespace LibraryOfRuina.core.settings.ui;

internal partial class NExtSlider : Control
{
    private ExtModSettings? _config;
    private PropertyInfo? _property;
    private string _displayFormat = "{0}";
    private string[]? _valueLabelKeys;

    private bool _fullyInitialized;
    private NSlider _slider;
    private MegaLabel _sliderLabel;
    private NSelectionReticle _selectionReticle;

    private const int LabelFontSize = 28;

    private enum HoldDirection { None, Left, Right }

    private HoldDirection _holdDir = HoldDirection.None;
    private float _holdTimer;
    private float _stepTimer;
    private float _currentRepeatRate = 0.1f;
    private const float InitialDelay = 0.3f;
    private const float StartingRepeatRate = 0.1f;
    private float _minRepeatDelay = 0.002f;

    public double MinValue { get; private set; }

    public double MaxValue { get; private set; }

    public double Step => _slider.Step;

    public NExtSlider()
    {
        var targetSize = new Vector2(324, 64);
        CustomMinimumSize = targetSize;
        Size = targetSize;
        SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        SizeFlagsVertical = SizeFlags.Fill;
        FocusMode = FocusModeEnum.All;

        this.TransferAllNodes(SceneHelper.GetScenePath("screens/settings_slider"));

        _slider = GetNode<NSlider>("Slider");
        _sliderLabel = GetNode<MegaLabel>("SliderValue");
        _selectionReticle = GetNode<NSelectionReticle>((NodePath)"SelectionReticle");
    }

    public void SetRange(double min, double max, double? step = null)
    {
        if (min >= max)
            throw new ArgumentException($"Invalid slider range: min ({min}) must be less than max ({max}).");

        var currentRealValue = _slider.Value + MinValue;
        MinValue = min;
        MaxValue = max;
        if (step != null) _slider.Step = step.Value;
        _slider.MaxValue = MaxValue - MinValue;
        RecalculateMinRepeatDelay();
        if (_fullyInitialized) SetValue(currentRealValue);
    }

    private void RecalculateMinRepeatDelay()
    {
        var numSteps = (float)((_slider.MaxValue - _slider.MinValue) / _slider.Step);
        var dynamicFloor = 1.5f / numSteps;
        _minRepeatDelay = Mathf.Max(0.002f, dynamicFloor);
    }

    public override void _Ready()
    {
        _slider.FocusMode = FocusModeEnum.All;
        _sliderLabel.AutoSizeEnabled = false;
        _sliderLabel.AddThemeFontSizeOverride("font_size", LabelFontSize);
        _sliderLabel.GrowHorizontal = GrowDirection.Begin;
        _sliderLabel.HorizontalAlignment = HorizontalAlignment.Right;
        _sliderLabel.ClipContents = false;

        _selectionReticle.AnchorRight = 1f;
        _selectionReticle.OffsetRight = 10f;

        SetFromProperty();
        _slider.Connect(Range.SignalName.ValueChanged, Callable.From<double>(OnValueChanged));
        Connect(Control.SignalName.FocusEntered, Callable.From(OnFocus));
        Connect(Control.SignalName.FocusExited, Callable.From(OnUnfocus));

        _fullyInitialized = true;
    }

    public void Initialize(ExtModSettings modConfig, PropertyInfo property)
    {
        if (property.PropertyType != typeof(double))
            throw new ArgumentException("NExtSlider requires a double property");

        _config = modConfig;
        _property = property;

        var rangeAttr = property.GetCustomAttribute<SliderRangeAttribute>();
        var formatAttr = property.GetCustomAttribute<SliderLabelFormatAttribute>();

        var min = rangeAttr?.Min ?? 0;
        var max = rangeAttr?.Max ?? 100;
        var step = rangeAttr?.Step ?? 1;
        _displayFormat = formatAttr?.Format ?? "{0}";
        _valueLabelKeys = property.GetCustomAttribute<SliderValueLabelsAttribute>()?.Keys;

        if (min >= max)
            throw new ArgumentException($"Invalid slider range: min ({min}) must be less than max ({max}).");

        MinValue = min;
        MaxValue = max;
        _slider.MinValue = 0;
        _slider.MaxValue = MaxValue - MinValue;
        _slider.Step = step;

        RecalculateMinRepeatDelay();
        _config.OnConfigReloaded += SetFromProperty;
    }

    private void SetFromProperty()
    {
        var propValue = (double)_property!.GetValue(null)!;
        SetValue(propValue);
    }

    private void SetValue(double value)
    {
        var clampedValue = Math.Clamp(value, MinValue, MaxValue);
        _slider.SetValueWithoutAnimation(clampedValue - MinValue);
        UpdateLabel(clampedValue);
        
        if (value != clampedValue) _property?.SetValue(null, clampedValue);
    }

    private void OnValueChanged(double proxyValue)
    {
        var realValue = proxyValue + MinValue;
        var step = _slider.Step;
        if (step > 0)
        {
            var decimalPlaces = BitConverter.GetBytes(decimal.GetBits((decimal)step)[3])[2];
            realValue = Math.Round(realValue, decimalPlaces);
        }
        _property?.SetValue(null, realValue);
        _config?.Changed();
        UpdateLabel(realValue);
    }

    private void UpdateLabel(double value)
    {
        _sliderLabel.Text = GetDisplayText(value);
        var textWidth = _sliderLabel.GetMinimumSize().X;
        var labelRightEdge = _sliderLabel.Position.X + _sliderLabel.Size.X;
        var labelLeftEdge = labelRightEdge - textWidth;
        _selectionReticle.OffsetLeft = labelLeftEdge - 10f;
    }

    private string GetDisplayText(double value)
    {
        if (_valueLabelKeys is { Length: > 0 } keys && _property != null && _config != null)
        {
            var index = (int)Math.Round((value - MinValue) / _slider.Step);
            if (index >= 0 && index < keys.Length)
            {
                var locKey = $"{_config.ModPrefix}{StringHelper.Slugify(_property.Name)}.{keys[index]}";
                var loc = LocString.GetIfExists("settings_ui", locKey);
                if (loc != null)
                {
                    return loc.GetFormattedText();
                }
            }
        }

        return string.Format(_displayFormat, value);
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        if (_config != null) _config.OnConfigReloaded -= SetFromProperty;
    }

    private void OnFocus()
    {
        if (NControllerManager.Instance?.IsUsingDirectionalNavigation != true) return;
        _selectionReticle.OnSelect();
    }

    private void OnUnfocus()
    {
        _selectionReticle.OnDeselect();
    }

    public override void _GuiInput(InputEvent @event)
    {
        base._GuiInput(@event);

        if (@event.IsActionPressed(MegaInput.left))
        {
            _slider.Value -= _slider.Step;
            StartHolding(HoldDirection.Left);
            AcceptEvent();
        }
        else if (@event.IsActionPressed(MegaInput.right))
        {
            _slider.Value += _slider.Step;
            StartHolding(HoldDirection.Right);
            AcceptEvent();
        }
        else if (@event.IsActionReleased(MegaInput.left) && _holdDir == HoldDirection.Left ||
                 @event.IsActionReleased(MegaInput.right) && _holdDir == HoldDirection.Right)
        {
            _holdDir = HoldDirection.None;
        }
    }

    private void StartHolding(HoldDirection dir)
    {
        _holdDir = dir;
        _holdTimer = 0f;
        _stepTimer = 0f;
        _currentRepeatRate = StartingRepeatRate;
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        if (_holdDir == HoldDirection.None) return;
        if (!HasFocus()) { _holdDir = HoldDirection.None; return; }

        _holdTimer += (float)delta;
        if (_holdTimer < InitialDelay) return;

        _stepTimer += (float)delta;
        if (_stepTimer < _currentRepeatRate) return;

        _stepTimer = 0f;
        _currentRepeatRate = Mathf.Clamp(_currentRepeatRate - 0.01f, _minRepeatDelay, 0.15f);

        if (_holdDir == HoldDirection.Left) _slider.Value -= _slider.Step;
        else _slider.Value += _slider.Step;
    }
}

