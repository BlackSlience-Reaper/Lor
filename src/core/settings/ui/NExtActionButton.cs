using System;
using Godot;
using LibraryOfRuina.framework.assets;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;

namespace LibraryOfRuina.core.settings.ui;

internal partial class NExtActionButton : NSettingsButton
{
    private Action? _onPressedAction;
    private ShaderMaterial _colorShader;

    public NExtActionButton()
    {
        CustomMinimumSize = new Vector2(324, 64);
        SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        SizeFlagsVertical = SizeFlags.Fill;
        FocusMode = FocusModeEnum.All;

        _colorShader = new ShaderMaterial { Shader = ResourceLoader.Load<Shader>("res://shaders/hsv.gdshader") };

        var image = new TextureRect
        {
            Name = "Image",
            Material = _colorShader,
            CustomMinimumSize = new Vector2(64, 64),
            Texture = PreloadManager.Cache.GetAsset<Texture2D>("res://images/ui/reward_screen/reward_skip_button.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale
        };
        image.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(image);

        // 用 MegaLabel 加主题覆盖，不用裸 Label 和 LabelSettings：原版按语言换字体只改主题覆盖 font，且只在 MegaLabel._Ready 里做；
        // LabelSettings.Font 会盖住主题覆盖，中文按钮文字会落到没有中文字形的 Kreon 上。
        // 下面的覆盖逐项对应原来的 LabelSettings（含它的默认值：透明阴影、偏移 1、行距 3）。
        var label = new MegaLabel
        {
            Name = "Label",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutoSizeEnabled = false
        };
        label.AddThemeFontOverride(ThemeConstants.Label.Font,
            PreloadManager.Cache.GetAsset<FontVariation>(SharedAssets.KreonBoldGlyphSpaceTwoResource));
        label.AddThemeFontSizeOverride(ThemeConstants.Label.FontSize, 28);
        label.AddThemeColorOverride(ThemeConstants.Label.FontColor, new Color(0.91f, 0.86f, 0.74f));
        label.AddThemeConstantOverride(ThemeConstants.Label.OutlineSize, 12);
        label.AddThemeColorOverride(ThemeConstants.Label.FontOutlineColor, new Color(0.29f, 0.14f, 0.14f));
        label.AddThemeColorOverride(ThemeConstants.Label.FontShadowColor, new Color(0f, 0f, 0f, 0f));
        label.AddThemeConstantOverride("shadow_outline_size", 1);
        label.AddThemeConstantOverride("shadow_offset_x", 1);
        label.AddThemeConstantOverride("shadow_offset_y", 1);
        label.AddThemeConstantOverride(ThemeConstants.Label.LineSpacing, 3);
        label.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(label);

        var reticleScene = PreloadManager.Cache.GetScene(SceneHelper.GetScenePath("ui/selection_reticle"));
        var reticle = reticleScene.Instantiate<NSelectionReticle>();
        reticle.Name = "SelectionReticle";
        reticle.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(reticle);
    }

    public void SetColor(float h, float s, float v)
    {
        _colorShader.SetShaderParameter("h", h);
        _colorShader.SetShaderParameter("s", s);
        _colorShader.SetShaderParameter("v", v);
    }

    public override void _Ready()
    {
        ConnectSignals();
    }

    public void Initialize(string buttonText, Action onPressed)
    {
        _onPressedAction = onPressed;
        var label = GetNodeOrNull<Label>("Label");
        if (label != null) label.Text = buttonText;
        Connect(NClickableControl.SignalName.Released, Callable.From<NExtActionButton>(OnReleased));
    }

    private void OnReleased(NExtActionButton button)
    {
        _onPressedAction?.Invoke();
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        if (IsConnected(NClickableControl.SignalName.Released, Callable.From<NExtActionButton>(OnReleased)))
            Disconnect(NClickableControl.SignalName.Released, Callable.From<NExtActionButton>(OnReleased));
    }
}

