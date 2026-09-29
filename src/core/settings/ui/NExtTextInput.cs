using System;
using System.Reflection;
using System.Text.RegularExpressions;
using Godot;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;

namespace LibraryOfRuina.core.settings.ui;

internal partial class NExtTextInput : NMegaLineEdit
{
    private ExtModSettings? _config;
    private PropertyInfo? _property;

    private StyleBoxFlat? _focusInvalid;
    private Regex? _validationRegex;
    private string _lastValidText = "";

    public NExtTextInput()
    {
        var targetSize = new Vector2(324, 64);
        CustomMinimumSize = targetSize;
        Size = targetSize;
        SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        SizeFlagsVertical = SizeFlags.Fill;
        FocusMode = FocusModeEnum.All;
    }

    private bool ValidateString(string value)
    {
        return _validationRegex == null || _validationRegex.IsMatch(value);
    }

    public void Initialize(ExtModSettings modConfig, PropertyInfo property)
    {
        if (property.PropertyType != typeof(string))
            throw new ArgumentException("NExtTextInput requires a string property");

        _config = modConfig;
        _property = property;

        var attr = property.GetCustomAttribute<SettingsTextInputAttribute>();
        if (attr?.MaxLength > 0) MaxLength = attr.MaxLength;

        var placeholderKey = $"{_config.ModPrefix}{StringHelper.Slugify(property.Name)}.placeholder";
        var locPlaceholder = LocString.GetIfExists("settings_ui", placeholderKey)?.GetFormattedText();
        PlaceholderText = locPlaceholder ?? "";

        try
        {
            var pattern = attr?.AllowedCharactersRegex ?? ".*";
            _validationRegex = new Regex($"^(?:{pattern})$", RegexOptions.Compiled);
        }
        catch (Exception e)
        {
            ExtModSettings.SettingsLogger.Error($"Unable to compile validation regex for {property.Name}: {e.Message}");
        }
    }

    public override void _Ready()
    {
        SetFromProperty();
        if (_config != null) _config.OnConfigReloaded += SetFromProperty;

        _focusInvalid = new StyleBoxFlat();
        _focusInvalid.DrawCenter = false;
        _focusInvalid.BorderColor = new Color(1, 0, 0);
        _focusInvalid.SetBorderWidthAll(2);
        _focusInvalid.SetCornerRadiusAll(3);
        _focusInvalid.SetExpandMarginAll(2);

        Connect(LineEdit.SignalName.TextChanged, Callable.From<string>(OnTextChanged));
        Connect(LineEdit.SignalName.TextSubmitted, Callable.From<string>(OnTextSubmitted));
        Connect(Control.SignalName.FocusExited, Callable.From(OnUnfocus));
    }

    private void SetFromProperty()
    {
        var propValue = (string?)_property!.GetValue(null) ?? "";

        if (!ValidateString(propValue))
        {
            Log.Warn($"{_property.Name}: stored value '{propValue}' violates validation; resetting to default");
            propValue = _config?.GetDefaultValue<string>(_property.Name) ?? "";
            _property.SetValue(null, propValue);
            _config?.Changed();
        }

        _lastValidText = propValue;
        Text = propValue;
    }

    private void OnTextChanged(string newText)
    {
        if (!ValidateString(newText))
        {
            AddThemeStyleboxOverride("focus", _focusInvalid);
            return;
        }

        RemoveThemeStyleboxOverride("focus");
        _lastValidText = newText;
        _property?.SetValue(null, newText);
        _config?.Changed();
    }

    private void OnTextSubmitted(string submittedText)
    {
        RevertIfInvalid();
        ReleaseFocus();
    }

    private void OnUnfocus()
    {
        RevertIfInvalid();
    }

    private void RevertIfInvalid()
    {
        RemoveThemeStyleboxOverride("focus");
        if (ValidateString(Text)) return;
        Text = _lastValidText;
    }

    public override void _GuiInput(InputEvent @event)
    {
        base._GuiInput(@event);
        if (@event.IsActionPressed(MegaInput.select) && CaretColumn == 0)
            CaretColumn = Text.Length;
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        if (_config != null) _config.OnConfigReloaded -= SetFromProperty;
    }
}

