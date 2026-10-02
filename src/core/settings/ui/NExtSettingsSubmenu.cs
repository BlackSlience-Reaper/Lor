using System;
using System.Linq;
using System.Reflection;
using Godot;
using LibraryOfRuina.addons.mega_text;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using LibraryOfRuina.interop;

namespace LibraryOfRuina.core.settings.ui;

internal partial class NExtSettingsSubmenu : NSubmenu
{
    private NBackButton? _extBackButton;
    private NExtScrollContainer _leftScrollArea;
    private VBoxContainer _modListVbox;
    private Control _modListPanel;
    private MegaRichTextLabel _modListTitle;

    private NExtScrollContainer _rightScrollArea;
    private VBoxContainer? _optionContainer;
    private Control _contentPanel;
    private MegaRichTextLabel _modTitle;
    private Tween? _fadeInTween;

    private ExtModSettings? _currentConfig;
    private string? _lastModId;
    private double _saveTimer = -1;
    private const double AutosaveDelay = 5;
    private bool _isUsingController;
    private bool _lastFocusOnRight;

    private const float ModTitleHeight = 90f;
    private const float TopOffset = ModTitleHeight + 30f;
    private const float ModListPosition = 180f;
    private const float ModListWidth = 360f;
    private const float MaxRightSideWidth = 1200f;

    protected override Control? InitialFocusedControl =>
        _lastFocusOnRight ? FindFirstFocusable(_optionContainer) : GetActiveModButton();

    public NExtSettingsSubmenu()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        GrowHorizontal = GrowDirection.Both;
        GrowVertical = GrowDirection.Both;

        _leftScrollArea = new NExtScrollContainer(TopOffset);
        _modListPanel = new Control
        {
            Name = "ModListContent",
            MouseFilter = MouseFilterEnum.Ignore
        };
        _modListPanel.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_leftScrollArea);

        _rightScrollArea = new NExtScrollContainer(TopOffset);
        _contentPanel = new Control
        {
            Name = "ConfigContent",
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _contentPanel.SetAnchorsPreset(LayoutPreset.TopLeft);
        _contentPanel.CustomMinimumSize = new Vector2(MaxRightSideWidth, 0);
        AddChild(_rightScrollArea);

        _modListTitle = CreateTitleControl("ModListTitle", "[center]Mods[/center]", 0f);
        _modListTitle.OffsetLeft = ModListPosition;
        _modListTitle.OffsetRight = ModListPosition + ModListWidth - NExtScrollContainer.ScrollbarGutterWidth;

        _modTitle = CreateTitleControl("ModTitle", "[center]...[/center]", 0f);
        _modListVbox = new VBoxContainer();
    }

    public override void _Ready()
    {
        AddChild(_modTitle);
        AddChild(_modListTitle);

        _modListPanel.AddChild(_modListVbox);
        _modListPanel.SetAnchorsPreset(LayoutPreset.TopLeft);

        InitializeModList();

        _modListVbox.MinimumSizeChanged += () =>
        {
            _modListPanel.CustomMinimumSize = new Vector2(_leftScrollArea.AvailableContentWidth, _modListVbox.GetMinimumSize().Y);
        };

        _leftScrollArea.AttachContent(_modListPanel);
        _leftScrollArea.DisableScrollingIfContentFits();
        _rightScrollArea.AttachContent(_contentPanel);
        _rightScrollArea.DisableScrollingIfContentFits();

        _extBackButton = PreloadManager.Cache.GetScene(SceneHelper.GetScenePath("ui/back_button"))
            .Instantiate<NBackButton>();
        _extBackButton.Name = "BackButton";
        AddChild(_extBackButton);

        _isUsingController =
            GameApi.IsDirectionalNavigation(NControllerManager.Instance);

        ConnectSignals();
        GetViewport().Connect(Viewport.SignalName.SizeChanged, Callable.From(RefreshSize));
        NControllerManager.Instance?.Connect(NControllerManager.SignalName.MouseDetected, Callable.From(InputTypeChanged));
        NControllerManager.Instance?.Connect(NControllerManager.SignalName.ControllerDetected, Callable.From(InputTypeChanged));
    }

    private void InitializeModList()
    {
        var selfNodePath = new NodePath(".");

        foreach (var modConfig in ExtSettingsRegistry.GetAll().Where(mod => mod.HasVisibleSettings()))
        {
            var modName = GetModTitle(modConfig);
            var modButton = new NExtModButton(modName);
            _modListVbox.AddChild(modButton);

            modButton.Connect(NClickableControl.SignalName.Released, Callable.From<NExtModButton>(button =>
                ModButtonClicked(button, modConfig)));
            modButton.Connect(NClickableControl.SignalName.Focused, Callable.From<NExtModButton>(ModButtonFocused));

            modButton.FocusNeighborLeft = selfNodePath;
            modButton.FocusNeighborRight = selfNodePath;
        }

        var mods = _modListVbox.GetChildren();
        if (mods.Count > 0)
        {
            var firstMod = mods.First() as NExtModButton;
            var lastMod = mods.Last() as NExtModButton;
            if (firstMod != null) firstMod.FocusNeighborTop = firstMod.GetPathTo(lastMod);
            if (lastMod != null) lastMod.FocusNeighborBottom = lastMod.GetPathTo(firstMod);
        }

        var topSpacer = new Control { CustomMinimumSize = new Vector2(0, 20) };
        _modListVbox.AddChild(topSpacer);
        _modListVbox.MoveChild(topSpacer, 0);
        _modListVbox.AddChild(new Control { CustomMinimumSize = new Vector2(0, 24) });
    }

    private NExtModButton? GetActiveModButton()
    {
        if (_currentConfig == null) return null;
        foreach (var button in _modListPanel.GetChild(0).GetChildren())
        {
            if (button is NExtModButton listButton && listButton.ModName == GetModTitle(_currentConfig))
                return listButton;
        }
        return null;
    }

    private void ModButtonClicked(NExtModButton button, ExtModSettings modConfig)
    {
        LoadModConfig(modConfig);
        if (!_isUsingController) return;

        SetBackButtonVisible(false);
        button.SetHotkeyIconVisible(true);
        Callable.From(() => { FindFirstFocusable(_optionContainer)?.TryGrabFocus(); }).CallDeferred();
        _lastFocusOnRight = true;
    }

    private void ModButtonFocused(NExtModButton button)
    {
        _lastFocusOnRight = false;
        SetBackButtonVisible(true);
    }

    private void FocusModList()
    {
        SetBackButtonVisible(true);
        foreach (var modButton in _modListVbox.GetChildren())
        {
            if (modButton is NExtModButton listButton)
                listButton.SetHotkeyIconVisible(false);
        }
        GetActiveModButton()?.TryGrabFocus();
    }

    private void SetBackButtonVisible(bool visible)
    {
        if (_extBackButton == null) return;
        if (!visible) { _extBackButton.Disable(); return; }

        VanillaPrivate.ClickableControlIsEnabled.Set(_extBackButton, false);
        _extBackButton.Enable();
    }

    private void SetHighlightedModButton(ExtModSettings config)
    {
        foreach (var button in _modListPanel.GetChild(0).GetChildren())
        {
            if (button is NExtModButton listButton)
                listButton.SetActiveState(listButton.ModName == GetModTitle(config));
        }
    }

    private void InputTypeChanged()
    {
        _isUsingController =
            GameApi.IsDirectionalNavigation(NControllerManager.Instance);
        _lastFocusOnRight = false;
        FocusModList();
    }

    public override void _Input(InputEvent @event)
    {
        base._Input(@event);
        if (_extBackButton?.IsEnabled == true) return;

        if (!@event.IsActionReleased(MegaInput.cancel) &&
            !@event.IsActionReleased(MegaInput.pauseAndBack) &&
            !@event.IsActionReleased(MegaInput.back)) return;

        var focusOwner = GetViewport().GuiGetFocusOwner();
        if (focusOwner == null || _optionContainer?.IsAncestorOf(focusOwner) != true) return;

        FocusModList();
        AcceptEvent();
    }

    private void LoadModConfig(ExtModSettings config)
    {
        if (config.ModId != null) _lastModId = config.ModId;

        if (_optionContainer != null || _currentConfig != null)
            SaveAndClearCurrentMod();

        _currentConfig = config;
        config.ConfigChanged += OnConfigChanged;
        SetHighlightedModButton(config);
        _lastFocusOnRight = false;

        _optionContainer = CreateOptionContainer();
        _contentPanel.AddChild(_optionContainer);

        try
        {
            config.SetupConfigUI(_optionContainer);
        }
        catch (Exception e)
        {
            ExtModSettings.SettingsLogger.Error("Failed setting up config for mod.\n" +
                                                "Check for incorrect setup or compatibility issues.");
            Log.Error(e.ToString());
            _stack.Pop();
            return;
        }

        try
        {
            var title = $"[center]{GetModSettingsTitle(config)}[/center]";
            _modTitle.SetTextAutoSize(title);
            RefreshSize();
            _rightScrollArea.InstantlyScrollToTop();
            ExtModSettings.ShowAndClearPendingErrors();
        }
        catch (Exception e)
        {
            ExtModSettings.SettingsLogger.Error("An error occurred while loading the mod config screen.");
            Log.Error(e.ToString());
            _stack.Pop();
        }
    }

    private VBoxContainer CreateOptionContainer()
    {
        var container = new VBoxContainer
        {
            Name = "VBoxContainer",
            CustomMinimumSize = new Vector2(0f, 0f),
            AnchorRight = 1f,
            GrowHorizontal = GrowDirection.End,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        container.AddChild(new Control { CustomMinimumSize = new Vector2(0, 16) });
        container.AddThemeConstantOverride("separation", 8);
        container.MinimumSizeChanged += RefreshSize;
        return container;
    }

    private static MegaRichTextLabel CreateTitleControl(string name, string defaultText, float minimumWidth)
    {
        var title = ExtModSettings.CreateRawLabelControl(defaultText, 36);
        title.Name = name;
        title.AutoSizeEnabled = true;
        title.MaxFontSize = 64;
        title.CustomMinimumSize = new Vector2(minimumWidth, ModTitleHeight);
        title.SetAnchorsPreset(LayoutPreset.TopLeft);
        title.OffsetBottom = TopOffset - 10;
        title.OffsetTop = title.OffsetBottom - ModTitleHeight;
        return title;
    }

    private static string GetModTitle(ExtModSettings config)
    {
        var locKey = $"{config.ModPrefix}MOD_NAME.title";
        var locStr = LocString.GetIfExists("settings_ui", locKey);
        if (locStr != null) return locStr.GetFormattedText();

        var fallbackTitle = ExtModSettings.GetRootNamespace(config.GetType());
        if (string.IsNullOrWhiteSpace(fallbackTitle)) fallbackTitle = "Unknown Mod";
        return fallbackTitle;
    }

    private static string GetModSettingsTitle(ExtModSettings config)
    {
        var locStr = LocString.GetIfExists("settings_ui", $"{config.ModPrefix}MOD_CONFIG.title");
        return locStr?.GetFormattedText() ?? GetModTitle(config);
    }

    private void RefreshSize()
    {
        if (_optionContainer == null) return;

        var (screenWidth, screenHeight) = GetViewportRect().Size;

        _leftScrollArea.Position = new Vector2(ModListPosition, 0);
        _leftScrollArea.Size = new Vector2(ModListWidth, screenHeight);

        var leftContentWidth = _leftScrollArea.AvailableContentWidth;
        _modListPanel.CustomMinimumSize = new Vector2(leftContentWidth, _modListPanel.CustomMinimumSize.Y);
        _modListVbox.CustomMinimumSize = new Vector2(leftContentWidth, _modListVbox.CustomMinimumSize.Y);
        _modListVbox.Size = new Vector2(leftContentWidth, _modListVbox.Size.Y);

        const float minLeftGap = 24f;
        const float minRightGap = 32f;
        const float modListEnd = ModListPosition + ModListWidth;
        const float scrollbarGutter = 60f;
        const float sliderClippingFix = 8f;

        var totalAvailableSpace = screenWidth - modListEnd;
        var maxSettingsWidth = MaxRightSideWidth - scrollbarGutter - sliderClippingFix;
        var spaceForSettings = totalAvailableSpace - minLeftGap - minRightGap - scrollbarGutter - sliderClippingFix;
        var actualSettingsWidth = Mathf.Min(spaceForSettings, maxSettingsWidth);

        var leftoverSpace = totalAvailableSpace - actualSettingsWidth - scrollbarGutter - sliderClippingFix;
        var unallocatedSpace = leftoverSpace - minLeftGap - minRightGap;

        var extraScrollbarSpacing = 0f;
        var centeringOffset = 0f;

        if (unallocatedSpace > 0)
        {
            extraScrollbarSpacing = Mathf.Min(unallocatedSpace, 64f);
            unallocatedSpace -= extraScrollbarSpacing;
            centeringOffset = unallocatedSpace / 2f;
        }

        var contentPosition = modListEnd + minLeftGap + centeringOffset;
        var containerWidth = actualSettingsWidth + extraScrollbarSpacing + scrollbarGutter + sliderClippingFix;

        _rightScrollArea.Position = new Vector2(contentPosition, 0);
        _rightScrollArea.Size = new Vector2(containerWidth, screenHeight);

        var clipperSize = _contentPanel.GetParent<Control>().Size;
        var requiredHeight = _optionContainer.GetMinimumSize().Y;
        var paddedHeight = requiredHeight + 30f;

        if (paddedHeight >= clipperSize.Y)
            paddedHeight += clipperSize.Y * 0.3f;

        var rightContentWidth = _rightScrollArea.AvailableContentWidth;
        _contentPanel.CustomMinimumSize = new Vector2(rightContentWidth, paddedHeight);
        _contentPanel.Size = new Vector2(rightContentWidth, paddedHeight);

        _optionContainer.CustomMinimumSize = new Vector2(actualSettingsWidth, requiredHeight);
        _optionContainer.Size = new Vector2(actualSettingsWidth, requiredHeight);

        _modTitle.OffsetLeft = contentPosition;
        _modTitle.OffsetRight = contentPosition + actualSettingsWidth;
        _modTitle.CustomMinimumSize = new Vector2(actualSettingsWidth, ModTitleHeight);
    }

    protected override void OnSubmenuShown()
    {
        base.OnSubmenuShown();
        _saveTimer = -1;

        var allConfigs = ExtSettingsRegistry.GetAll();
        ExtModSettings? targetMod = null;

        if (!string.IsNullOrWhiteSpace(_lastModId))
            targetMod = ExtSettingsRegistry.Get(_lastModId);
        targetMod ??= allConfigs.FirstOrDefault();

        if (targetMod != null)
            LoadModConfig(targetMod);

        _fadeInTween?.Kill();
        _fadeInTween = CreateTween().SetParallel();
        _fadeInTween.TweenProperty(_contentPanel, "modulate", Colors.White, 0.5f)
            .From(new Color(0, 0, 0, 0))
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Cubic);

        Callable.From(InputTypeChanged).CallDeferred();
    }

    protected override void OnSubmenuHidden()
    {
        SaveAndClearCurrentMod();
        base.OnSubmenuHidden();
    }

    private void SaveAndClearCurrentMod()
    {
        if (_currentConfig != null) _currentConfig.ConfigChanged -= OnConfigChanged;
        SaveCurrentConfig();

        if (_optionContainer != null)
        {
            _optionContainer.MinimumSizeChanged -= RefreshSize;
            _optionContainer.QueueFree();
            _optionContainer = null;
        }

        if (_currentConfig is ExtAutoModSettings autoSettings)
            autoSettings.ClearUIEventHandlers();

        _currentConfig = null;

        if (ExtModSettings.SettingsLogger.PendingUserMessages.Count > 0)
            Callable.From(ExtModSettings.ShowAndClearPendingErrors).CallDeferred();
    }

    private void OnConfigChanged(object? sender, EventArgs e)
    {
        _saveTimer = AutosaveDelay;
    }

    private static Control? FindFirstFocusable(Node? parent)
    {
        if (parent == null) return null;
        foreach (var child in parent.GetChildren())
        {
            if (child is Control { FocusMode: FocusModeEnum.All or FocusModeEnum.Click } control)
                return control;
            var nestedFocus = FindFirstFocusable(child);
            if (nestedFocus != null) return nestedFocus;
        }
        return null;
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        if (_saveTimer <= 0) return;
        _saveTimer -= delta;
        if (_saveTimer <= 0) SaveCurrentConfig();
    }

    private void SaveCurrentConfig()
    {
        _currentConfig?.Save();
        _saveTimer = -1;
    }

    public override void _ExitTree()
    {
        GetViewport().Disconnect(Viewport.SignalName.SizeChanged, Callable.From(RefreshSize));
        NControllerManager.Instance?.Disconnect(NControllerManager.SignalName.MouseDetected, Callable.From(InputTypeChanged));
        NControllerManager.Instance?.Disconnect(NControllerManager.SignalName.ControllerDetected, Callable.From(InputTypeChanged));
        base._ExitTree();
    }
}
