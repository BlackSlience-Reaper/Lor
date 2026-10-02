using System;
using Godot;
using LibraryOfRuina.addons.mega_text;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.Debug;
using MegaCrit.Sts2.Core.Nodes.Ftue;

namespace LibraryOfRuina.features.ftue;

internal enum LibraryOfRuinaCombatFtueVisual
{
    Image,
    CenteredImage,
    ResistanceExamples,
    AttackTypeMapping,
    SpecialGuestPortraits,
    EmotionTrack
}

internal readonly record struct LibraryOfRuinaCombatFtuePage(
    string TitleKey,
    string BodyKey,
    string? ImagePath,
    LibraryOfRuinaCombatFtueVisual Visual = LibraryOfRuinaCombatFtueVisual.Image,
    string TitleTable = "ftues",
    string BodyTable = "ftues");

/// <summary>
/// Full-screen combat tutorial styled after the vanilla combat rules FTUE.
/// </summary>
public partial class NLibraryOfRuinaCombatRulesFtue : NFtue
{
    private const string LocTable = "ftues";
    private const float BaseWidth = 1920f;
    private const float BaseHeight = 1080f;
    private static readonly Vector2 ImageAnimOffset = new(200f, 0f);

    private readonly string _ftueId;
    private readonly LibraryOfRuinaCombatFtuePage[] _pages;
    private readonly int _totalPages;
    private int _currentPage;

    private Control? _contentRoot;
    private Control? _visualRoot;
    private TextureRect? _image;
    private Control? _resistanceExamples;
    private Control? _attackTypeMapping;
    private Control? _specialGuestPortraits;
    private Control? _emotionTrack;
    private MegaRichTextLabel? _bodyText;
    private MegaLabel? _header;
    private MegaLabel? _pageCount;
    private TextureButton? _prevButton;
    private TextureButton? _nextButton;
    private Tween? _pageTurnTween;
    private Vector2 _visualPosition;
    private Vector2 _textPosition;

    internal static NLibraryOfRuinaCombatRulesFtue Create(
        string ftueId,
        LibraryOfRuinaCombatFtuePage[] pages)
    {
        return new NLibraryOfRuinaCombatRulesFtue(ftueId, pages);
    }

    private NLibraryOfRuinaCombatRulesFtue(
        string ftueId,
        LibraryOfRuinaCombatFtuePage[] pages)
    {
        _ftueId = ftueId;
        _pages = pages.Length > 0
            ? pages
            : [new LibraryOfRuinaCombatFtuePage(ftueId, ftueId, LibraryOfRuinaFtueAssets.CombatFtue0Texture)];
        _totalPages = _pages.Length;
    }

    public override void _Ready()
    {
        Name = "NLibraryOfRuinaCombatRulesFtue";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        BuildUi();
        UpdateLayoutScale();
        ShowPage(0);
    }

    public override void _Process(double delta)
    {
        UpdateLayoutScale();
    }

    public override void _Input(InputEvent inputEvent)
    {
        if (!IsVisibleInTree() || NDevConsole.Instance.Visible)
            return;

        Control focusOwner = GetViewport().GuiGetFocusOwner();
        if (focusOwner is TextEdit or LineEdit)
            return;

        if (inputEvent.IsActionPressed(MegaInput.left) && _currentPage > 0)
        {
            ShowPage(_currentPage - 1, -1);
            GetViewport().SetInputAsHandled();
            return;
        }

        if (inputEvent.IsActionPressed(MegaInput.right) || inputEvent.IsActionPressed(GameApi.Confirm))
        {
            AdvanceOrClose();
            GetViewport().SetInputAsHandled();
        }
    }

    private void BuildUi()
    {
        _contentRoot = new Control
        {
            Name = "ContentRoot",
            Size = new Vector2(BaseWidth, BaseHeight),
            MouseFilter = MouseFilterEnum.Ignore
        };
        AddChild(_contentRoot);

        _visualRoot = new Control
        {
            Name = "VisualRoot",
            Position = new Vector2(292f, 252f),
            Size = new Vector2(671f, 512f),
            CustomMinimumSize = new Vector2(671f, 512f),
            ClipContents = true,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _contentRoot.AddChild(_visualRoot);
        _visualPosition = _visualRoot.Position;

        _image = new TextureRect
        {
            Name = "Image",
            Position = Vector2.Zero,
            Size = _visualRoot.Size,
            CustomMinimumSize = _visualRoot.Size,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _visualRoot.AddChild(_image);

        _resistanceExamples = BuildResistanceExamples();
        _visualRoot.AddChild(_resistanceExamples);

        _attackTypeMapping = BuildAttackTypeMapping();
        _visualRoot.AddChild(_attackTypeMapping);

        _specialGuestPortraits = BuildSpecialGuestPortraits();
        _visualRoot.AddChild(_specialGuestPortraits);

        _emotionTrack = BuildEmotionTrack();
        _visualRoot.AddChild(_emotionTrack);

        _bodyText = CreateRichTextLabel("Description", 28);
        _bodyText.Position = new Vector2(1005f, 271f);
        _bodyText.Size = new Vector2(623f, 483f);
        _bodyText.CustomMinimumSize = new Vector2(623f, 483f);
        _bodyText.VerticalAlignment = VerticalAlignment.Center;
        _bodyText.AddThemeConstantOverride("line_separation", -2);
        _contentRoot.AddChild(_bodyText);
        _textPosition = _bodyText.Position;

        _header = CreateLabel("Header", 28, new Color(0.937255f, 0.784314f, 0.317647f));
        _header.Position = new Vector2(360f, 821f);
        _header.Size = new Vector2(1200f, 60f);
        _header.CustomMinimumSize = new Vector2(1200f, 60f);
        _contentRoot.AddChild(_header);

        _pageCount = CreateLabel("PageCount", 24, new Color(0.529412f, 0.807843f, 0.921569f));
        _pageCount.Position = new Vector2(360f, 854f);
        _pageCount.Size = new Vector2(1200f, 60f);
        _pageCount.CustomMinimumSize = new Vector2(1200f, 60f);
        _contentRoot.AddChild(_pageCount);

        _prevButton = CreateArrowButton("LeftArrow", LibraryOfRuinaFtueAssets.SettingsTinyLeftArrowTexture);
        _prevButton.Position = new Vector2(40f, 476f);
        _contentRoot.AddChild(_prevButton);
        _prevButton.Pressed += () => ShowPage(_currentPage - 1, -1);

        _nextButton = CreateArrowButton("RightArrow", LibraryOfRuinaFtueAssets.SettingsTinyRightArrowTexture);
        _nextButton.Position = new Vector2(1752f, 476f);
        _contentRoot.AddChild(_nextButton);
        _nextButton.Pressed += AdvanceOrClose;
    }

    private void ShowPage(int pageIndex, int direction = 0)
    {
        pageIndex = Math.Clamp(pageIndex, 0, _totalPages - 1);
        _currentPage = pageIndex;

        LibraryOfRuinaCombatFtuePage page = _pages[pageIndex];

        _header!.Text = GetLocText(page.TitleTable, page.TitleKey);
        _bodyText!.Text = GetLocText(page.BodyTable, page.BodyKey);
        SetRichTextFontSize(_bodyText, ResolveBodyFontSize(page.Visual));
        _pageCount!.Text = $"({_currentPage + 1}/{_totalPages})";

        bool usesImage = page.Visual is LibraryOfRuinaCombatFtueVisual.Image
            or LibraryOfRuinaCombatFtueVisual.CenteredImage;
        _image!.Visible = usesImage;
        _resistanceExamples!.Visible = page.Visual == LibraryOfRuinaCombatFtueVisual.ResistanceExamples;
        _attackTypeMapping!.Visible = page.Visual == LibraryOfRuinaCombatFtueVisual.AttackTypeMapping;
        _specialGuestPortraits!.Visible = page.Visual == LibraryOfRuinaCombatFtueVisual.SpecialGuestPortraits;
        _emotionTrack!.Visible = page.Visual == LibraryOfRuinaCombatFtueVisual.EmotionTrack;
        if (usesImage)
        {
            string imagePath = page.ImagePath ?? LibraryOfRuinaFtueAssets.CombatFtue0Texture;
            _image.StretchMode = page.Visual == LibraryOfRuinaCombatFtueVisual.CenteredImage
                ? TextureRect.StretchModeEnum.KeepAspectCentered
                : TextureRect.StretchModeEnum.Scale;
            _image.Texture = ResourceLoader.Load<Texture2D>(
                imagePath);
        }

        _prevButton!.Visible = _currentPage > 0;
        _nextButton!.Visible = true;
        AnimatePage(direction);
    }

    private void AdvanceOrClose()
    {
        if (_currentPage >= _totalPages - 1)
        {
            _pageTurnTween?.Kill();
            CloseFtue();
            return;
        }

        ShowPage(_currentPage + 1, 1);
    }

    private void AnimatePage(int direction)
    {
        _pageTurnTween?.Kill();
        _pageTurnTween = CreateTween().SetParallel();

        if (_visualRoot != null)
        {
            _visualRoot.Position = _visualPosition;
            _visualRoot.Modulate = new Color(1f, 1f, 1f, 0f);
            _pageTurnTween.TweenProperty(_visualRoot, "modulate:a", 1f, 0.5)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Cubic);

            if (direction != 0)
            {
                _pageTurnTween.TweenProperty(_visualRoot, "position", _visualPosition, 0.5)
                    .SetEase(Tween.EaseType.Out)
                    .SetTrans(Tween.TransitionType.Expo)
                    .From(_visualPosition + ImageAnimOffset * direction);
            }
        }

        if (_bodyText != null)
        {
            _bodyText.Position = _textPosition;
            _bodyText.Modulate = new Color(1f, 1f, 1f, 0f);
            _pageTurnTween.TweenProperty(_bodyText, "modulate:a", 1f, 0.6)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Linear);
            _pageTurnTween.TweenProperty(_bodyText, "visible_ratio", 1f, 0.6)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Sine)
                .From(0f);

            if (direction != 0)
            {
                _pageTurnTween.TweenProperty(_bodyText, "position", _textPosition, 0.5)
                    .SetEase(Tween.EaseType.Out)
                    .SetTrans(Tween.TransitionType.Expo)
                    .From(_textPosition + ImageAnimOffset * direction);
            }
        }
    }

    private static Control BuildResistanceExamples()
    {
        Panel panel = CreateDiagramPanel("ResistanceExamples");

        MegaLabel title = CreateLabel("ResistanceTitle", 32, new Color(0.937255f, 0.784314f, 0.317647f));
        title.Position = new Vector2(35f, 25f);
        title.Size = new Vector2(601f, 55f);
        title.Text = GetLocText("LOR_COMBAT_FTUE_20260827_RESISTANCE_PANEL_TITLE");
        panel.AddChild(title);

        AddResistanceExample(
            panel,
            105f,
            LibraryOfRuinaFtueAssets.PierceNormalTexture,
            "LOR_COMBAT_FTUE_20260827_PIERCE_PHYSICAL_NORMAL");
        AddResistanceExample(
            panel,
            285f,
            LibraryOfRuinaFtueAssets.SlashChaosImmuneTexture,
            "LOR_COMBAT_FTUE_20260827_SLASH_CHAOS_IMMUNE");

        return panel;
    }

    private static void AddResistanceExample(
        Control parent,
        float y,
        string iconPath,
        string labelKey)
    {
        TextureRect icon = CreateResistanceIcon(iconPath, new Vector2(112f, 112f));
        icon.Position = new Vector2(65f, y);
        parent.AddChild(icon);

        MegaLabel label = CreateLabel("ResistanceExampleLabel", 26, new Color(1f, 0.964706f, 0.886275f));
        label.HorizontalAlignment = HorizontalAlignment.Left;
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.Position = new Vector2(205f, y - 4f);
        label.Size = new Vector2(410f, 120f);
        label.Text = GetLocText(labelKey);
        parent.AddChild(label);
    }

    private static Control BuildAttackTypeMapping()
    {
        Panel panel = CreateDiagramPanel("AttackTypeMapping");

        MegaLabel title = CreateLabel("MappingTitle", 32, new Color(0.937255f, 0.784314f, 0.317647f));
        title.Position = new Vector2(35f, 25f);
        title.Size = new Vector2(601f, 55f);
        title.Text = GetLocText("LOR_COMBAT_FTUE_20260827_MAPPING_PANEL_TITLE");
        panel.AddChild(title);

        AddMappingColumn(
            panel,
            15f,
            LibraryOfRuinaFtueAssets.BluntNormalTexture,
            "LOR_COMBAT_FTUE_20260827_SINGLE_TO_BLUNT");
        AddMappingColumn(
            panel,
            235f,
            LibraryOfRuinaFtueAssets.PierceNormalTexture,
            "LOR_COMBAT_FTUE_20260827_MULTI_TO_PIERCE");
        AddMappingColumn(
            panel,
            455f,
            LibraryOfRuinaFtueAssets.SlashNormalTexture,
            "LOR_COMBAT_FTUE_20260827_AOE_TO_SLASH");

        MegaLabel modes = CreateLabel("ResistanceModes", 20, new Color(0.529412f, 0.807843f, 0.921569f));
        modes.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        modes.Position = new Vector2(25f, 410f);
        modes.Size = new Vector2(621f, 60f);
        modes.Text = GetLocText("LOR_COMBAT_FTUE_20260827_RESISTANCE_MODES");
        panel.AddChild(modes);

        return panel;
    }

    private static Control BuildSpecialGuestPortraits()
    {
        Panel panel = CreateDiagramPanel("SpecialGuestPortraits");

        MegaLabel title = CreateLabel(
            "SpecialGuestPortraitsTitle",
            32,
            new Color(0.937255f, 0.784314f, 0.317647f));
        title.Position = new Vector2(35f, 20f);
        title.Size = new Vector2(601f, 55f);
        title.Text = GetLocText(
            LocTable,
            "LOR_NEOW_SPECIAL_GUEST_FTUE_PORTRAITS_PANEL_TITLE");
        panel.AddChild(title);

        AddSpecialGuestPortrait(
            panel,
            new Vector2(25f, 82f),
            LibraryOfRuinaFtueAssets.KaliSpecialGuestEventTexture);
        AddSpecialGuestPortrait(
            panel,
            new Vector2(348f, 82f),
            LibraryOfRuinaFtueAssets.XiaoSpecialGuestEventTexture);
        AddSpecialGuestPortrait(
            panel,
            new Vector2(25f, 286f),
            LibraryOfRuinaFtueAssets.RnfmabjSpecialGuestEventTexture);
        AddSpecialGuestPortrait(
            panel,
            new Vector2(348f, 286f),
            LibraryOfRuinaFtueAssets.IoriSpecialGuestEventTexture);

        return panel;
    }

    private static void AddSpecialGuestPortrait(
        Control parent,
        Vector2 position,
        string imagePath)
    {
        var frame = new Panel
        {
            Position = position,
            Size = new Vector2(298f, 180f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        frame.AddThemeStyleboxOverride(
            "panel",
            new StyleBoxFlat
            {
                BgColor = new Color(0.02f, 0.025f, 0.035f),
                BorderColor = new Color(0.55f, 0.28f, 0.75f, 0.9f),
                BorderWidthLeft = 3,
                BorderWidthTop = 3,
                BorderWidthRight = 3,
                BorderWidthBottom = 3,
                CornerRadiusTopLeft = 8,
                CornerRadiusTopRight = 8,
                CornerRadiusBottomLeft = 8,
                CornerRadiusBottomRight = 8
            });
        parent.AddChild(frame);

        var portrait = new TextureRect
        {
            Position = new Vector2(4f, 4f),
            Size = new Vector2(290f, 172f),
            Texture = ResourceLoader.Load<Texture2D>(
                imagePath),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            TextureFilter = TextureFilterEnum.LinearWithMipmaps,
            MouseFilter = MouseFilterEnum.Ignore
        };
        frame.AddChild(portrait);
    }

    private static Control BuildEmotionTrack()
    {
        Panel panel = CreateDiagramPanel("EmotionTrack");

        MegaLabel title = CreateLabel(
            "EmotionTrackTitle",
            34,
            new Color(0.72f, 0.42f, 0.96f));
        title.Position = new Vector2(35f, 30f);
        title.Size = new Vector2(601f, 70f);
        title.Text = GetLocText(
            "gameplay_ui",
            "SPECIAL_GUEST_EMOTION_HOVER.title");
        panel.AddChild(title);

        var track = new Panel
        {
            Position = new Vector2(70f, 237f),
            Size = new Vector2(531f, 22f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        track.AddThemeStyleboxOverride(
            "panel",
            new StyleBoxFlat
            {
                BgColor = new Color(0.35f, 0.12f, 0.48f),
                BorderColor = new Color(0.78f, 0.46f, 1f),
                BorderWidthLeft = 3,
                BorderWidthTop = 3,
                BorderWidthRight = 3,
                BorderWidthBottom = 3,
                CornerRadiusTopLeft = 11,
                CornerRadiusTopRight = 11,
                CornerRadiusBottomLeft = 11,
                CornerRadiusBottomRight = 11
            });
        panel.AddChild(track);

        string[] levels = ["I", "II", "III", "IV", "V"];
        for (int index = 0; index < levels.Length; index++)
        {
            float x = 66f + index * 121f;
            var marker = new Panel
            {
                Position = new Vector2(x, 188f),
                Size = new Vector2(76f, 76f),
                MouseFilter = MouseFilterEnum.Ignore
            };
            marker.AddThemeStyleboxOverride(
                "panel",
                new StyleBoxFlat
                {
                    BgColor = new Color(
                        0.25f + index * 0.055f,
                        0.08f + index * 0.035f,
                        0.36f + index * 0.07f),
                    BorderColor = new Color(0.86f, 0.63f, 1f),
                    BorderWidthLeft = 4,
                    BorderWidthTop = 4,
                    BorderWidthRight = 4,
                    BorderWidthBottom = 4,
                    CornerRadiusTopLeft = 38,
                    CornerRadiusTopRight = 38,
                    CornerRadiusBottomLeft = 38,
                    CornerRadiusBottomRight = 38
                });
            panel.AddChild(marker);

            MegaLabel level = CreateLabel(
                "EmotionLevel" + levels[index],
                28,
                new Color(1f, 0.92f, 1f));
            level.Position = Vector2.Zero;
            level.Size = marker.Size;
            level.Text = levels[index];
            marker.AddChild(level);
        }

        MegaLabel points = CreateLabel(
            "EmotionPoints",
            28,
            new Color(0.86f, 0.63f, 1f));
        points.Position = new Vector2(70f, 330f);
        points.Size = new Vector2(531f, 70f);
        points.Text = "0/3  →  3/3  →  +1";
        panel.AddChild(points);

        return panel;
    }

    private static void AddMappingColumn(
        Control parent,
        float x,
        string iconPath,
        string labelKey)
    {
        TextureRect icon = CreateResistanceIcon(iconPath, new Vector2(112f, 112f));
        icon.Position = new Vector2(x + 44f, 105f);
        parent.AddChild(icon);

        MegaLabel label = CreateLabel("MappingLabel", 24, new Color(1f, 0.964706f, 0.886275f));
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.Position = new Vector2(x, 235f);
        label.Size = new Vector2(200f, 125f);
        label.Text = GetLocText(labelKey);
        parent.AddChild(label);
    }

    private static Panel CreateDiagramPanel(string name)
    {
        var panel = new Panel
        {
            Name = name,
            Position = Vector2.Zero,
            Size = new Vector2(671f, 512f),
            MouseFilter = MouseFilterEnum.Ignore,
            Visible = false
        };

        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.035f, 0.055f, 0.065f, 0.94f),
            BorderColor = new Color(0.82f, 0.73f, 0.48f, 0.9f),
            BorderWidthLeft = 4,
            BorderWidthTop = 4,
            BorderWidthRight = 4,
            BorderWidthBottom = 4,
            CornerRadiusTopLeft = 18,
            CornerRadiusTopRight = 18,
            CornerRadiusBottomLeft = 18,
            CornerRadiusBottomRight = 18
        };
        panel.AddThemeStyleboxOverride("panel", style);
        return panel;
    }

    private static TextureRect CreateResistanceIcon(string path, Vector2 size)
    {
        return new TextureRect
        {
            Size = size,
            CustomMinimumSize = size,
            Texture = ResourceLoader.Load<Texture2D>(path),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = TextureFilterEnum.LinearWithMipmaps,
            MouseFilter = MouseFilterEnum.Ignore
        };
    }

    private static int ResolveBodyFontSize(
        LibraryOfRuinaCombatFtueVisual visual) => visual switch
    {
        LibraryOfRuinaCombatFtueVisual.Image => 28,
        LibraryOfRuinaCombatFtueVisual.CenteredImage => 27,
        LibraryOfRuinaCombatFtueVisual.EmotionTrack => 21,
        _ => 24
    };

    private static string GetLocText(string key)
    {
        return GetLocText(LocTable, key);
    }

    private static string GetLocText(string table, string key)
    {
        return LocString.GetIfExists(table, key)?.GetFormattedText() ?? key;
    }

    private static void SetRichTextFontSize(MegaRichTextLabel label, int fontSize)
    {
        string[] fontSizeNames =
        [
            "normal_font_size",
            "bold_font_size",
            "bold_italics_font_size",
            "italics_font_size",
            "mono_font_size"
        ];
        foreach (string fontSizeName in fontSizeNames)
            label.AddThemeFontSizeOverride(fontSizeName, fontSize);
    }

    private void UpdateLayoutScale()
    {
        if (_contentRoot == null)
            return;

        Vector2 viewportSize = GetViewportRect().Size;
        float scale = Mathf.Min(viewportSize.X / BaseWidth, viewportSize.Y / BaseHeight);
        _contentRoot.Scale = Vector2.One * scale;
        _contentRoot.Position = (viewportSize - new Vector2(BaseWidth, BaseHeight) * scale) * 0.5f;
    }

    private static MegaLabel CreateLabel(string name, int fontSize, Color fontColor)
    {
        var label = new MegaLabel
        {
            Name = name,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutoSizeEnabled = false,
            MouseFilter = MouseFilterEnum.Ignore
        };

        label.AddThemeFontOverride("font", PreloadManager.Cache.GetAsset<Font>(LibraryOfRuinaFtueAssets.KreonRegularGlyphSpaceOneResource));
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", fontColor);
        label.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.5f));
        label.AddThemeConstantOverride("shadow_offset_x", 3);
        label.AddThemeConstantOverride("shadow_offset_y", 2);
        return label;
    }

    private static MegaRichTextLabel CreateRichTextLabel(string name, int fontSize)
    {
        var label = new MegaRichTextLabel
        {
            Name = name,
            AutoSizeEnabled = false,
            BbcodeEnabled = true,
            ScrollActive = false,
            MouseFilter = MouseFilterEnum.Ignore,
            FocusMode = FocusModeEnum.None,
            VisibleCharactersBehavior = TextServer.VisibleCharactersBehavior.CharsAfterShaping
        };

        label.AddThemeFontOverride("normal_font", PreloadManager.Cache.GetAsset<Font>(LibraryOfRuinaFtueAssets.KreonRegularGlyphSpaceOneResource));
        label.AddThemeFontOverride("bold_font", PreloadManager.Cache.GetAsset<Font>(LibraryOfRuinaFtueAssets.KreonBoldGlyphSpaceOneResource));
        label.AddThemeColorOverride("default_color", new Color(1f, 0.964706f, 0.886275f));
        label.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.5f));
        label.AddThemeConstantOverride("shadow_offset_x", 3);
        label.AddThemeConstantOverride("shadow_offset_y", 2);

        string[] fontSizeNames =
        [
            "normal_font_size",
            "bold_font_size",
            "bold_italics_font_size",
            "italics_font_size",
            "mono_font_size"
        ];
        foreach (string fontSizeName in fontSizeNames)
            label.AddThemeFontSizeOverride(fontSizeName, fontSize);

        return label;
    }

    private static TextureButton CreateArrowButton(string name, string texturePath)
    {
        var button = new TextureButton
        {
            Name = name,
            Size = new Vector2(128f, 128f),
            CustomMinimumSize = new Vector2(128f, 128f),
            TextureNormal = PreloadManager.Cache.GetAsset<Texture2D>(texturePath),
            TextureHover = PreloadManager.Cache.GetAsset<Texture2D>(texturePath),
            TexturePressed = PreloadManager.Cache.GetAsset<Texture2D>(texturePath),
            TextureDisabled = PreloadManager.Cache.GetAsset<Texture2D>(texturePath),
            StretchMode = TextureButton.StretchModeEnum.KeepAspectCentered,
            FocusMode = FocusModeEnum.All,
            MouseFilter = MouseFilterEnum.Stop,
            MouseDefaultCursorShape = CursorShape.PointingHand
        };
        button.Modulate = new Color(1f, 0.92f, 0.38f);
        return button;
    }
}
