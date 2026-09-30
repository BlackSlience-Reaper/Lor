using System;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.addons.mega_text;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.Fonts;
using MegaCrit.Sts2.Core.Nodes.Ftue;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;

namespace LibraryOfRuina.features.ftue;

/// <summary>
/// A generic FTUE popup for the Library of Ruina mod.
/// Built entirely in code (no .tscn required) to work as a mod component.
/// Mimics the vanilla FTUE popup style: semi-opaque panel, title, body text, confirm button.
/// Supports optional multi-page navigation and per-page anchored pointers.
/// Uses standard Godot Button (not NButton) since we build UI programmatically.
/// </summary>
public partial class NLibraryOfRuinaFtuePopup : NFtue
{
    private const string LocTable = "ftues";

    // Layout constants (matching vanilla ftue popup proportions)
    private const float PanelWidth = 768f;
    private const float PanelMinHeight = 410f;
    private const float PanelPaddingLeft = 62f;
    private const float PanelPaddingRight = 54f;
    private const float PanelPaddingTop = 34f;
    private const float PanelPaddingBottom = 18f;
    private const int TitleFontSize = 26;
    private const int BodyFontSize = 22;
    private const int ButtonFontSize = 22;
    private const float ButtonWidth = 275f;
    private const float ButtonHeight = 75f;
    private const float ElementSpacing = 10f;
    private const float ArrowWidth = 244f;
    private const float ArrowHeight = 204f;
    private const float HighlightPadding = 10f;
    private static readonly Vector2 PageTurnAnimOffset = new(80f, 0f);

    private readonly string _ftueId;
    private readonly string[] _titleKeys;
    private readonly string[] _bodyKeys;
    private readonly Func<Control?>?[]? _targetResolvers;
    private readonly Vector2[]? _popupOffsets;
    private int _currentPage;
    private readonly int _totalPages;

    // UI nodes
    private TextureRect? _panel;
    private MegaRichTextLabel? _titleLabel;
    private MegaRichTextLabel? _bodyLabel;
    private MegaLabel? _pageCountLabel;
    private NButton? _confirmButton;
    private Button? _prevButton;
    private Button? _nextButton;
    private TextureRect? _arrow;
    private Panel? _highlight;
    private Control? _highlightedTarget;
    private int _highlightedTargetZIndex;
    private bool _hasHighlightedTargetZIndex;

    // Tween
    private Tween? _pageTween;

    private TaskCompletionSource<bool>? _completionSource;

    /// <summary>
    /// Create a single-page FTUE popup.
    /// </summary>
    public static NLibraryOfRuinaFtuePopup Create(string ftueId, string titleKey, string bodyKey)
    {
        return new NLibraryOfRuinaFtuePopup(ftueId, [titleKey], [bodyKey], null, null);
    }

    /// <summary>
    /// Create a multi-page FTUE popup.
    /// </summary>
    public static NLibraryOfRuinaFtuePopup CreateMultiPage(string ftueId, string[] titleKeys, string[] bodyKeys)
    {
        return new NLibraryOfRuinaFtuePopup(ftueId, titleKeys, bodyKeys, null, null);
    }

    public static NLibraryOfRuinaFtuePopup CreateAnchored(
        string ftueId,
        string titleKey,
        string bodyKey,
        Func<Control?> targetResolver,
        Vector2 popupOffset)
    {
        return CreateAnchoredMultiPage(ftueId, [titleKey], [bodyKey], [targetResolver], [popupOffset]);
    }

    public static NLibraryOfRuinaFtuePopup CreateAnchoredMultiPage(
        string ftueId,
        string[] titleKeys,
        string[] bodyKeys,
        Func<Control?>?[] targetResolvers,
        Vector2[] popupOffsets)
    {
        return new NLibraryOfRuinaFtuePopup(ftueId, titleKeys, bodyKeys, targetResolvers, popupOffsets);
    }

    private NLibraryOfRuinaFtuePopup(
        string ftueId,
        string[] titleKeys,
        string[] bodyKeys,
        Func<Control?>?[]? targetResolvers,
        Vector2[]? popupOffsets)
    {
        _ftueId = ftueId;
        _titleKeys = titleKeys.Length > 0 ? titleKeys : [ftueId];
        _bodyKeys = bodyKeys.Length > 0 ? bodyKeys : [ftueId];
        _targetResolvers = targetResolvers;
        _popupOffsets = popupOffsets;
        _totalPages = Math.Max(1, _bodyKeys.Length);
        _currentPage = 0;
    }

    private Func<Control?>? CurrentTargetResolver
    {
        get
        {
            if (_targetResolvers == null || _targetResolvers.Length == 0)
                return null;

            int index = Math.Min(_currentPage, _targetResolvers.Length - 1);
            return _targetResolvers[index];
        }
    }

    private Vector2 CurrentPopupOffset
    {
        get
        {
            if (_popupOffsets == null || _popupOffsets.Length == 0)
                return Vector2.Zero;

            int index = Math.Min(_currentPage, _popupOffsets.Length - 1);
            return _popupOffsets[index];
        }
    }

    public override void _Ready()
    {
        Name = "NLibraryOfRuinaFtuePopup";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        BuildUI();
        ShowPage(0);
    }

    public override void _Process(double delta)
    {
        UpdateAnchoredLayout();
    }

    public override void _ExitTree()
    {
        RestoreHighlightedTarget();
        base._ExitTree();
    }

    private void BuildUI()
    {
        _highlight = new Panel
        {
            Name = "TargetHighlight",
            MouseFilter = MouseFilterEnum.Ignore,
            Visible = false
        };
        var highlightStyle = new StyleBoxFlat
        {
            BgColor = new Color(1f, 0.82f, 0.16f, 0.08f),
            BorderColor = new Color(1f, 0.25f, 0.1f, 0.92f),
            BorderWidthBottom = 3,
            BorderWidthTop = 3,
            BorderWidthLeft = 3,
            BorderWidthRight = 3,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6
        };
        _highlight.AddThemeStyleboxOverride("panel", highlightStyle);
        AddChild(_highlight);

        _arrow = new TextureRect
        {
            Name = "Arrow",
            Texture = ResourceLoader.Load<Texture2D>(LibraryOfRuinaFtueAssets.FtuePointerArrowTexture),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore,
            Visible = false,
            Size = new Vector2(ArrowWidth, ArrowHeight),
            PivotOffset = new Vector2(ArrowWidth * 0.5f, ArrowHeight * 0.5f)
        };
        AddChild(_arrow);

        _panel = new TextureRect();
        _panel.Name = "FtuePopup";
        _panel.Texture = ResourceLoader.Load<Texture2D>(LibraryOfRuinaFtueAssets.FtuePopupTexture);
        _panel.Material = ResourceLoader.Load<Material>(LibraryOfRuinaFtueAssets.FtuePopupResource);
        _panel.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        _panel.StretchMode = TextureRect.StretchModeEnum.Scale;
        _panel.MouseFilter = MouseFilterEnum.Stop;
        _panel.CustomMinimumSize = new Vector2(PanelWidth, PanelMinHeight);
        _panel.Size = new Vector2(PanelWidth, PanelMinHeight);
        _panel.SetAnchorsPreset(LayoutPreset.TopLeft);
        _panel.GrowHorizontal = GrowDirection.Both;
        _panel.GrowVertical = GrowDirection.Both;

        // Vertical layout inside panel
        var vbox = new VBoxContainer();
        vbox.Name = "VBox";
        vbox.SetAnchorsPreset(LayoutPreset.FullRect);
        vbox.OffsetLeft = PanelPaddingLeft;
        vbox.OffsetTop = PanelPaddingTop;
        vbox.OffsetRight = -PanelPaddingRight;
        vbox.OffsetBottom = -PanelPaddingBottom;
        vbox.AddThemeConstantOverride("separation", (int)ElementSpacing);

        // Title
        _titleLabel = CreateRichLabel("Title", TitleFontSize);
        _titleLabel.CustomMinimumSize = new Vector2(0, 56);
        _titleLabel.VerticalAlignment = VerticalAlignment.Center;
        _titleLabel.AddThemeFontOverride(
            "normal_font",
            PreloadManager.Cache.GetAsset<Font>(
                LibraryOfRuinaFtueAssets.KreonBoldGlyphSpaceTwoResource));
        _titleLabel.AddThemeColorOverride(
            "default_color",
            new Color(0.937255f, 0.784314f, 0.317647f));
        _titleLabel.AddThemeColorOverride(
            "font_outline_color",
            new Color(0.33f, 0.2475f, 0f));
        _titleLabel.AddThemeConstantOverride("outline_size", 12);
        vbox.AddChild(_titleLabel);

        // Body text
        _bodyLabel = CreateRichLabel("Body", BodyFontSize);
        _bodyLabel.SizeFlagsVertical = SizeFlags.ExpandFill;
        _bodyLabel.CustomMinimumSize = new Vector2(0, 185);
        _bodyLabel.AddThemeColorOverride(
            "default_color",
            new Color(1f, 0.964706f, 0.886275f));
        _bodyLabel.AddThemeColorOverride(
            "font_shadow_color",
            new Color(0f, 0f, 0f, 0.25098f));
        _bodyLabel.AddThemeConstantOverride("line_separation", -2);
        _bodyLabel.AddThemeConstantOverride("shadow_offset_x", 3);
        _bodyLabel.AddThemeConstantOverride("shadow_offset_y", 2);
        vbox.AddChild(_bodyLabel);

        // Bottom bar: page count (left) + buttons (right)
        var bottomBar = new HBoxContainer();
        bottomBar.Name = "BottomBar";
        bottomBar.AddThemeConstantOverride("separation", 12);
        if (_totalPages == 1)
        {
            bottomBar.Alignment = BoxContainer.AlignmentMode.Center;
        }

        // Page count label (only shown for multi-page)
        _pageCountLabel = new MegaLabel();
        _pageCountLabel.Name = "PageCount";
        _pageCountLabel.HorizontalAlignment = HorizontalAlignment.Left;
        _pageCountLabel.VerticalAlignment = VerticalAlignment.Center;
        _pageCountLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _pageCountLabel.Visible = _totalPages > 1;

        var kreonNormal = PreloadManager.Cache.GetAsset<Font>(LibraryOfRuinaFtueAssets.KreonRegularSharedResource);
        _pageCountLabel.AddThemeFontOverride("font", kreonNormal);
        _pageCountLabel.AddThemeFontSizeOverride("font_size", BodyFontSize);
        _pageCountLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.65f, 0.55f));

        bottomBar.AddChild(_pageCountLabel);

        // Navigation buttons for multi-page
        if (_totalPages > 1)
        {
            _prevButton = CreateStyledButton("PrevBtn", "<");
            _prevButton.CustomMinimumSize = new Vector2(48, ButtonHeight);
            _prevButton.Pressed += OnPrevPressed;
            bottomBar.AddChild(_prevButton);

            _nextButton = CreateStyledButton("NextBtn", ">");
            _nextButton.CustomMinimumSize = new Vector2(48, ButtonHeight);
            _nextButton.Pressed += OnNextPressed;
            bottomBar.AddChild(_nextButton);
        }

        // Native FTUE confirm button
        _confirmButton = PreloadManager.Cache
            .GetScene(SceneHelper.GetScenePath("ftue/ftue_confirm_button"))
            .Instantiate<NFtueConfirmButton>();
        _confirmButton.Name = "ConfirmBtn";
        _confirmButton.CustomMinimumSize = new Vector2(ButtonWidth, ButtonHeight);
        _confirmButton.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        _confirmButton.Connect(
            NClickableControl.SignalName.Released,
            Callable.From<NButton>(_ => OnConfirmPressed()));
        bottomBar.AddChild(_confirmButton);

        vbox.AddChild(bottomBar);
        _panel.AddChild(vbox);
        AddChild(_panel);
    }

    private void ShowPage(int pageIndex, int direction = 0)
    {
        pageIndex = Math.Clamp(pageIndex, 0, _totalPages - 1);
        _currentPage = pageIndex;

        // Title
        var titleKey = pageIndex < _titleKeys.Length ? _titleKeys[pageIndex] : _titleKeys[0];
        var titleLoc = LocString.GetIfExists(LocTable, titleKey);
        _titleLabel!.Text = titleLoc?.GetFormattedText() ?? titleKey;

        // Body
        var bodyKey = pageIndex < _bodyKeys.Length ? _bodyKeys[pageIndex] : _bodyKeys[^1];
        var bodyLoc = LocString.GetIfExists(LocTable, bodyKey);
        _bodyLabel!.Text = bodyLoc?.GetFormattedText() ?? bodyKey;

        // Page count
        if (_totalPages > 1 && _pageCountLabel != null)
        {
            _pageCountLabel.Text = $"({_currentPage + 1} / {_totalPages})";
        }

        // Button visibility for multi-page
        if (_prevButton != null)
        {
            _prevButton.Visible = _currentPage > 0;
            _prevButton.Disabled = _currentPage <= 0;
        }

        if (_nextButton != null)
        {
            // On last page, hide next button and show confirm
            _nextButton.Visible = _currentPage < _totalPages - 1;
            _nextButton.Disabled = _currentPage >= _totalPages - 1;
        }

        // Confirm button: always visible on single-page; on multi-page, only on last page
        if (_totalPages > 1)
        {
            _confirmButton!.Visible = _currentPage == _totalPages - 1;
        }

        RestoreHighlightedTarget();
        UpdateAnchoredLayout();

        // Animate page transition
        AnimatePageIn(direction);
    }

    private void AnimatePageIn(int direction)
    {
        _pageTween?.Kill();
        _pageTween = CreateTween().SetParallel();

        if (_bodyLabel != null)
        {
            Vector2 bodyPosition = _bodyLabel.Position;
            _bodyLabel.Modulate = new Color(1, 1, 1, 0);
            _pageTween.TweenProperty(_bodyLabel, "modulate:a", 1f, 0.4)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Linear);
            if (direction != 0)
            {
                _pageTween.TweenProperty(_bodyLabel, "position", bodyPosition, 0.35)
                    .SetEase(Tween.EaseType.Out)
                    .SetTrans(Tween.TransitionType.Expo)
                    .From(bodyPosition + PageTurnAnimOffset * direction);
            }
            _pageTween.TweenProperty(_bodyLabel, "visible_ratio", 1f, 0.5)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Sine)
                .From(0f);
        }

        if (_titleLabel != null)
        {
            Vector2 titlePosition = _titleLabel.Position;
            _titleLabel.Modulate = new Color(1, 1, 1, 0);
            _pageTween.TweenProperty(_titleLabel, "modulate:a", 1f, 0.25)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Linear);
            if (direction != 0)
            {
                _pageTween.TweenProperty(_titleLabel, "position", titlePosition, 0.35)
                    .SetEase(Tween.EaseType.Out)
                    .SetTrans(Tween.TransitionType.Expo)
                    .From(titlePosition + PageTurnAnimOffset * direction);
            }
        }
    }

    private void OnPrevPressed()
    {
        if (_currentPage > 0)
            ShowPage(_currentPage - 1, -1);
    }

    private void OnNextPressed()
    {
        if (_currentPage < _totalPages - 1)
            ShowPage(_currentPage + 1, 1);
    }

    private void OnConfirmPressed()
    {
        _completionSource?.TrySetResult(true);
        CloseFtue();
    }

    /// <summary>
    /// Await this to block until the player confirms the popup.
    /// </summary>
    public Task WaitForPlayerToConfirm()
    {
        _completionSource = new TaskCompletionSource<bool>();
        return _completionSource.Task;
    }

    public override void _Input(InputEvent inputEvent)
    {
        if (!IsVisibleInTree()) return;

        // Keyboard/gamepad navigation for multi-page
        if (_totalPages > 1)
        {
            if (inputEvent.IsActionPressed("ui_left") && _prevButton is { Visible: true, Disabled: false })
            {
                OnPrevPressed();
                GetViewport().SetInputAsHandled();
                return;
            }
            if (inputEvent.IsActionPressed("ui_right") && _nextButton is { Visible: true, Disabled: false })
            {
                OnNextPressed();
                GetViewport().SetInputAsHandled();
                return;
            }
        }

        // Accept/confirm
        if (inputEvent.IsActionPressed("ui_accept") && _confirmButton is { Visible: true })
        {
            OnConfirmPressed();
            GetViewport().SetInputAsHandled();
        }
    }

    private void UpdateAnchoredLayout()
    {
        if (_panel == null)
            return;

        Func<Control?>? targetResolver = CurrentTargetResolver;
        if (targetResolver == null)
        {
            _panel.SetAnchorsPreset(LayoutPreset.TopLeft);
            _panel.Position = (Size - _panel.Size) * 0.5f;
            if (_arrow != null) _arrow.Visible = false;
            if (_highlight != null) _highlight.Visible = false;
            RestoreHighlightedTarget();
            return;
        }

        Control? target = targetResolver();
        if (target == null || !IsInstanceValid(target) || !target.IsVisibleInTree())
        {
            _panel.Position = GetDefaultPanelPosition();
            if (_arrow != null) _arrow.Visible = false;
            if (_highlight != null) _highlight.Visible = false;
            RestoreHighlightedTarget();
            return;
        }

        Rect2 targetRect = target.GetGlobalRect();
        Vector2 targetCenter = targetRect.GetCenter();
        Vector2 panelPosition = targetCenter + CurrentPopupOffset - _panel.Size * 0.5f;
        Vector2 viewportSize = GetViewportRect().Size;
        panelPosition.X = Mathf.Clamp(panelPosition.X, 20f, Mathf.Max(20f, viewportSize.X - _panel.Size.X - 20f));
        panelPosition.Y = Mathf.Clamp(panelPosition.Y, 20f, Mathf.Max(20f, viewportSize.Y - _panel.Size.Y - 20f));
        _panel.SetAnchorsPreset(LayoutPreset.TopLeft);
        _panel.Position = panelPosition;

        UpdateArrow(targetRect, _panel.GetGlobalRect());
        UpdateHighlight(target, targetRect);
    }

    private Vector2 GetDefaultPanelPosition()
    {
        return (GetViewportRect().Size - (_panel?.Size ?? new Vector2(PanelWidth, PanelMinHeight))) * 0.5f;
    }

    private void UpdateArrow(Rect2 targetRect, Rect2 panelRect)
    {
        if (_arrow == null)
            return;

        Vector2 targetCenter = targetRect.GetCenter();
        Vector2 panelCenter = panelRect.GetCenter();
        Vector2 start = panelCenter;
        Vector2 direction = targetCenter - panelCenter;
        if (direction.LengthSquared() < 1f)
        {
            _arrow.Visible = false;
            return;
        }

        _arrow.Visible = true;
        _arrow.Position = start - _arrow.Size * 0.5f;
        _arrow.Rotation = direction.Angle() - (Mathf.Pi * 0.75f);
    }

    private void UpdateHighlight(Control target, Rect2 targetRect)
    {
        if (_highlight == null)
            return;

        if (_highlightedTarget != target)
        {
            RestoreHighlightedTarget();
            _highlightedTarget = target;
            _highlightedTargetZIndex = target.ZIndex;
            _hasHighlightedTargetZIndex = true;
            target.ZIndex = Math.Max(target.ZIndex, 50);
        }

        _highlight.Visible = true;
        _highlight.Position = targetRect.Position - Vector2.One * HighlightPadding;
        _highlight.Size = targetRect.Size + Vector2.One * (HighlightPadding * 2f);
    }

    private void RestoreHighlightedTarget()
    {
        if (_highlightedTarget != null
            && _hasHighlightedTargetZIndex
            && IsInstanceValid(_highlightedTarget))
        {
            _highlightedTarget.ZIndex = _highlightedTargetZIndex;
        }

        _highlightedTarget = null;
        _hasHighlightedTargetZIndex = false;
    }

    #region UI Factory Helpers

    private static MegaRichTextLabel CreateRichLabel(string name, int fontSize)
    {
        var kreonNormal = PreloadManager.Cache.GetAsset<Font>(LibraryOfRuinaFtueAssets.KreonRegularSharedResource);
        var kreonBold = PreloadManager.Cache.GetAsset<Font>(LibraryOfRuinaFtueAssets.KreonBoldSharedResource);

        var label = new MegaRichTextLabel
        {
            Name = name,
            AutoSizeEnabled = false,
            MouseFilter = MouseFilterEnum.Ignore,
            FocusMode = FocusModeEnum.None,
            BbcodeEnabled = true,
            ScrollActive = false,
            VerticalAlignment = VerticalAlignment.Top
        };

        label.AddThemeFontOverride("normal_font", kreonNormal);
        label.AddThemeFontOverride("bold_font", kreonBold);

        string[] fontSizeNames =
        [
            "font_size", "normal_font_size", "bold_font_size",
            "italics_font_size", "bold_italics_font_size", "mono_font_size"
        ];
        foreach (var fsn in fontSizeNames)
            label.AddThemeFontSizeOverride(fsn, fontSize);

        return label;
    }

    private static Button CreateStyledButton(string name, string text)
    {
        var button = new Button();
        button.Name = name;
        button.Text = text;
        button.FocusMode = FocusModeEnum.All;

        // Style the button to match game aesthetic
        var normalStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.2f, 0.18f, 0.25f, 0.9f),
            BorderColor = new Color(0.85f, 0.75f, 0.55f, 0.8f),
            BorderWidthBottom = 1,
            BorderWidthTop = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            ContentMarginLeft = 16,
            ContentMarginRight = 16,
            ContentMarginTop = 8,
            ContentMarginBottom = 8
        };

        var hoverStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.3f, 0.27f, 0.35f, 0.95f),
            BorderColor = new Color(0.95f, 0.85f, 0.6f),
            BorderWidthBottom = 2,
            BorderWidthTop = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            ContentMarginLeft = 16,
            ContentMarginRight = 16,
            ContentMarginTop = 8,
            ContentMarginBottom = 8
        };

        button.AddThemeStyleboxOverride("normal", normalStyle);
        button.AddThemeStyleboxOverride("hover", hoverStyle);
        button.AddThemeStyleboxOverride("pressed", hoverStyle);
        button.AddThemeStyleboxOverride("focus", hoverStyle);
        button.AddThemeColorOverride("font_color", new Color(0.9f, 0.85f, 0.7f));
        button.AddThemeColorOverride("font_hover_color", new Color(1f, 0.95f, 0.8f));
        button.AddThemeFontSizeOverride("font_size", ButtonFontSize);
        // 没有 font 覆盖的 Button 用引擎默认字体。原版只替换主题覆盖 font，所以先设成与页码相同的 Kreon，
        // 再按当前语言替换（MegaLabel 在 _Ready 里做同样的事，Button 要手动调用）。
        button.AddThemeFontOverride("font", PreloadManager.Cache.GetAsset<Font>(LibraryOfRuinaFtueAssets.KreonRegularSharedResource));
        FontControlUtils.ApplyLocaleFontSubstitution(button, FontType.Regular, "font");

        return button;
    }

    #endregion
}
