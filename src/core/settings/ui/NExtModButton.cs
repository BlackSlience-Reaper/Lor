using Godot;
using LibraryOfRuina.framework.assets;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;

namespace LibraryOfRuina.core.settings.ui;

internal partial class NExtModButton : NButton
{
    // 悬停、按下时的文字颜色补间直接改标签的主题覆盖 font_color。
    private const string FontColorOverrideProperty = "theme_override_colors/font_color";

    private MegaLabel _label;
    private Panel _backgroundPanel;
    private StyleBoxFlat _styleBox;
    private Tween? _stateTween;

    private bool _isButtonDown;

    private bool IsSelectedMod { get; set; }

    public string ModName { get; private set; }

    private readonly Color _textNormal = new(0.7f, 0.7f, 0.7f);
    private readonly Color _textHover = Colors.White;
    private readonly Color _textActive = StsColors.gold;

    private readonly Color _bgNormal = new(0f, 0f, 0f, 0.2f);
    private readonly Color _bgHover = new(0.15f, 0.15f, 0.15f, 0.5f);
    private readonly Color _bgPressed = new(0.2f, 0.2f, 0.2f, 0.7f);

    private TextureRect? _controllerIconRect;
    private bool _isHotkeyIconVisible;

    public NExtModButton(string modName)
    {
        ModName = modName;
        Name = $"ModButton_{modName}";
        CustomMinimumSize = new Vector2(0f, 66f);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        FocusMode = FocusModeEnum.All;

        _styleBox = new StyleBoxFlat
        {
            BgColor = _bgNormal,
            CornerRadiusTopLeft = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomRight = 8,
            BorderColor = StsColors.gold,
            BorderWidthLeft = 0
        };

        _backgroundPanel = new Panel { MouseFilter = MouseFilterEnum.Ignore };
        _backgroundPanel.SetAnchorsPreset(LayoutPreset.FullRect);
        _backgroundPanel.AddThemeStyleboxOverride("panel", _styleBox);
        AddChild(_backgroundPanel);

        // 用 MegaLabel 加主题覆盖，不用裸 Label 和 LabelSettings：原版按语言换字体只改主题覆盖 font，且只在 MegaLabel._Ready 里做；
        // LabelSettings.Font 会盖住主题覆盖，中文模组名会落到没有中文字形的 Kreon 上。
        // 下面的覆盖逐项对应原来的 LabelSettings（含它的默认值），项目主题里 Label 的描边、阴影不会混进来。
        _label = new MegaLabel
        {
            Text = modName,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            AutoSizeEnabled = false
        };
        _label.AddThemeFontOverride(ThemeConstants.Label.Font,
            PreloadManager.Cache.GetAsset<Font>(SharedAssets.KreonRegularGlyphSpaceOneResource));
        _label.AddThemeFontSizeOverride(ThemeConstants.Label.FontSize, 24);
        _label.AddThemeColorOverride(ThemeConstants.Label.FontColor, _textNormal);
        _label.AddThemeConstantOverride(ThemeConstants.Label.OutlineSize, 0);
        _label.AddThemeColorOverride(ThemeConstants.Label.FontShadowColor, new Color(0f, 0f, 0f, 0.8f));
        _label.AddThemeConstantOverride("shadow_outline_size", 2);
        _label.AddThemeConstantOverride("shadow_offset_x", 1);
        _label.AddThemeConstantOverride("shadow_offset_y", 1);
        _label.AddThemeConstantOverride(ThemeConstants.Label.LineSpacing, 3);
        _label.SetAnchorsPreset(LayoutPreset.FullRect);
        _label.OffsetLeft = 24f;
        _label.OffsetRight = -16f;
        AddChild(_label);

        const float size = 48;
        _controllerIconRect = new TextureRect
        {
            CustomMinimumSize = Vector2.One * size,
            Size = Vector2.One * size,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            Visible = false
        };
        AddChild(_controllerIconRect);
        _controllerIconRect.SetAnchorsPreset(LayoutPreset.CenterRight);
        _controllerIconRect.Position = new Vector2(-size, -size / 2);
    }

    public override void _Ready()
    {
        ConnectSignals();
        UpdateVisualState(instant: true);

        if (NControllerManager.Instance != null)
        {
            NControllerManager.Instance.Connect(NControllerManager.SignalName.MouseDetected, Callable.From(RefreshIconVisibility));
            NControllerManager.Instance.Connect(NControllerManager.SignalName.ControllerDetected, Callable.From(RefreshIconVisibility));
            NControllerManager.Instance.Connect(NControllerManager.SignalName.ControllerTypeChanged, Callable.From(RefreshIconVisibility));
        }
        RefreshIconVisibility();
    }

    public void SetHotkeyIconVisible(bool enabled)
    {
        _isHotkeyIconVisible = enabled;
        RefreshIconVisibility();
    }

    private void RefreshIconVisibility()
    {
        if (_controllerIconRect == null) return;
        bool isController =
            GameApi.IsDirectionalNavigation(NControllerManager.Instance);
        _controllerIconRect.Visible = _isHotkeyIconVisible && isController;
        if (_controllerIconRect.Visible)
            _controllerIconRect.Texture = NInputManager.Instance?.GetHotkeyIcon(MegaInput.cancel);
    }

    public void SetActiveState(bool isActive)
    {
        IsSelectedMod = isActive;
        UpdateVisualState();
    }

    protected override void OnFocus() { base.OnFocus(); UpdateVisualState(); }

    protected override void OnUnfocus() { base.OnUnfocus(); UpdateVisualState(); }

    protected override void OnPress() { base.OnPress(); _isButtonDown = true; UpdateVisualState(); }

    protected override void OnRelease() { base.OnRelease(); _isButtonDown = false; UpdateVisualState(); }

    private void UpdateVisualState(bool instant = false)
    {
        Color targetBg;
        Color targetText;
        int targetBorderWidth;

        if (_isButtonDown)
        {
            targetBg = _bgPressed;
            targetText = _textHover;
            targetBorderWidth = 0;
        }
        else if (IsFocused)
        {
            targetBg = _bgHover;
            targetText = _textHover;
            targetBorderWidth = IsSelectedMod ? 4 : 0;
        }
        else if (IsSelectedMod)
        {
            targetBg = _bgHover;
            targetText = _textActive;
            targetBorderWidth = 4;
        }
        else
        {
            targetBg = _bgNormal;
            targetText = _textNormal;
            targetBorderWidth = 0;
        }

        if (instant)
        {
            _styleBox.BgColor = targetBg;
            _styleBox.BorderWidthLeft = targetBorderWidth;
            _label.AddThemeColorOverride(ThemeConstants.Label.FontColor, targetText);
            return;
        }

        _stateTween?.Kill();
        _stateTween = CreateTween().SetParallel().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        _stateTween.TweenProperty(_styleBox, "bg_color", targetBg, 0.1f);
        _stateTween.TweenProperty(_label, FontColorOverrideProperty, targetText, 0.15f);
        _stateTween.TweenProperty(_styleBox, "border_width_left", targetBorderWidth, 0.2f);
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        if (NControllerManager.Instance == null) return;
        NControllerManager.Instance.Disconnect(NControllerManager.SignalName.MouseDetected, Callable.From(RefreshIconVisibility));
        NControllerManager.Instance.Disconnect(NControllerManager.SignalName.ControllerDetected, Callable.From(RefreshIconVisibility));
        NControllerManager.Instance.Disconnect(NControllerManager.SignalName.ControllerTypeChanged, Callable.From(RefreshIconVisibility));
    }
}
