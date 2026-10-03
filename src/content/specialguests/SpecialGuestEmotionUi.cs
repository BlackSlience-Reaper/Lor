using System;
using System.Reflection;
using Godot;
using HarmonyLib;
using LibraryLib.SpeedDice;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using LibraryOfRuina.interop;

namespace LibraryOfRuina.content.specialguests;

[HarmonyPatch(typeof(NHealthBar), "RefreshForeground")]
[HarmonyPriority(Priority.Last)]
internal static class SpecialGuestEmotionUiPatch
{

    [HarmonyPostfix]
    private static void Postfix(NHealthBar __instance)
    {
        if (VanillaPrivate.HealthBarCreature.Get(__instance) is not Creature
            {
                Monster: SpecialGuestMonsterBase guest,
            })
        {
            return;
        }

        RefreshEmotionUi();
        Callable.From(RefreshEmotionUi).CallDeferred();

        void RefreshEmotionUi()
        {
            if (!GodotObject.IsInstanceValid(__instance))
            {
                return;
            }

            LibraryEmotionBarUi.Refresh(__instance);
            SpecialGuestEmotionBarUi.Refresh(__instance, guest);
        }
    }
}

internal static class SpecialGuestEmotionBarUi
{
    private const string ContainerName = "LibraryEmotionBarContainer";
    private const string HoverAreaName = "SpecialGuestEmotionHoverArea";
    private const float EmotionBarHeight = 14f;
    // 情感条填充只保留一像素边框，满值时覆盖两端的内部区域。
    private const float ForegroundInset = 1f;
    private const float ForegroundInsetVertical = 1f;
    private const float ForegroundContainerInset = ForegroundInset * 2f;
    private const float MinFillWidth = 12f;

    public static void Refresh(
        NHealthBar healthBar,
        SpecialGuestMonsterBase guest)
    {
        Control hpBar = healthBar.HpBarContainer;
        Node? parent = hpBar.GetParent();
        if (parent == null)
        {
            return;
        }

        Control? bar =
            parent.GetNodeOrNull<Control>(ContainerName)
            ?? hpBar.GetNodeOrNull<Control>(ContainerName);
        if (bar == null)
        {
            return;
        }

        // Guests without an emotion track (attached body parts such as the
        // hands) still show the bar at 0/0 so the track area stays visible.
        bar.Visible = true;

        if (bar.GetParent() != parent)
        {
            bar.Reparent(parent, keepGlobalTransform: false);
        }

        SpecialGuestCombatBarLayout.ApplyEmotionBar(bar, hpBar, EmotionBarHeight);
        NormalizeTrackNodes(bar, hpBar);
        UpdateValues(bar, guest);
        HideEmotionLevelBadge(bar);

        var hoverArea =
            bar.GetNodeOrNull<SpecialGuestEmotionHoverArea>(HoverAreaName);
        if (hoverArea == null)
        {
            hoverArea = new SpecialGuestEmotionHoverArea
            {
                Name = HoverAreaName,
                MouseFilter = Control.MouseFilterEnum.Stop,
                AnchorLeft = 0f,
                AnchorTop = 0f,
                AnchorRight = 1f,
                AnchorBottom = 1f,
                OffsetLeft = 0f,
                OffsetTop = 0f,
                OffsetRight = 0f,
                OffsetBottom = 0f,
                ZIndex = 2,
                ZAsRelative = true,
            };
            bar.AddChild(hoverArea);
        }

        hoverArea.Initialize(healthBar, guest);
    }

    internal static EmotionProgress GetEmotionProgress(
        SpecialGuestMonsterBase guest)
    {
        IReadOnlyList<int> thresholds = guest.EmotionUnitThresholds;
        if (thresholds.Count == 0)
        {
            return new EmotionProgress(0, 0);
        }

        int level = Math.Clamp(
            guest.EmotionLevel,
            0,
            thresholds.Count - 1);
        int required = thresholds[level];
        if (guest.EmotionLevel >= thresholds.Count)
        {
            return new EmotionProgress(required, required);
        }

        int current = Math.Clamp(guest.EmotionUnits, 0, required);
        return new EmotionProgress(current, required);
    }

    private static void HideEmotionLevelBadge(Control bar)
    {
        TextureRect? badge =
            bar.GetNodeOrNull<TextureRect>("EmotionLevelBadge");
        if (badge == null)
        {
            return;
        }

        badge.Texture = null;
        badge.Modulate = Colors.Transparent;
        badge.MouseFilter = Control.MouseFilterEnum.Ignore;
        badge.Visible = false;
    }

    private static void UpdateValues(
        Control bar,
        SpecialGuestMonsterBase guest)
    {
        EmotionProgress progress = GetEmotionProgress(guest);
        NinePatchRect? fill =
            bar.GetNodeOrNull<NinePatchRect>(
                "EmotionForegroundContainer/Mask/EmotionFill");
        if (fill != null)
        {
            fill.Visible = progress.Current > 0;
            if (fill.Visible)
            {
                float maxFillWidth =
                    Math.Max(0f, bar.Size.X - ForegroundContainerInset);
                float width = Math.Max(
                    MinFillWidth,
                    (float)progress.Current
                    / progress.Required
                    * maxFillWidth);
                fill.OffsetRight = width - maxFillWidth;
            }
        }

        Label? valueLabel =
            bar.GetNodeOrNull<Label>("EmotionValueLabel");
        if (valueLabel != null)
        {
            valueLabel.Text =
                $"{progress.Current}/{progress.Required}"
                + $"（{guest.EmotionLevel}）";
        }
    }

    private static void NormalizeTrackNodes(Control bar, Control hpBar)
    {
        NinePatchRect? background =
            bar.GetNodeOrNull<NinePatchRect>("EmotionBackground");
        if (background != null)
        {
            background.AnchorLeft = 0f;
            background.AnchorTop = 0f;
            background.AnchorRight = 1f;
            background.AnchorBottom = 1f;
            background.OffsetLeft = 1f;
            background.OffsetTop = 0f;
            background.OffsetRight = -1f;
            background.OffsetBottom = 0f;
            background.AxisStretchVertical =
                NinePatchRect.AxisStretchMode.Stretch;
        }

        Control? foreground =
            bar.GetNodeOrNull<Control>("EmotionForegroundContainer");
        if (foreground != null)
        {
            foreground.ClipChildren = CanvasItem.ClipChildrenMode.Disabled;
            foreground.AnchorLeft = 0f;
            foreground.AnchorTop = 0f;
            foreground.AnchorRight = 1f;
            foreground.AnchorBottom = 1f;
            foreground.OffsetLeft = ForegroundInset;
            foreground.OffsetTop = ForegroundInsetVertical;
            foreground.OffsetRight = -ForegroundInset;
            foreground.OffsetBottom = -ForegroundInsetVertical;

            NinePatchRect? mask = foreground.GetNodeOrNull<NinePatchRect>("Mask");
            if (mask == null
                && hpBar.GetNodeOrNull<NinePatchRect>("HpForegroundContainer/Mask") is { } hpMask)
            {
                // 沿用血条的端部形状裁切填充，避免矩形填充在两端留下空隙。
                mask = new NinePatchRect
                {
                    Name = "Mask",
                    Texture = hpMask.Texture,
                    PatchMarginLeft = hpMask.PatchMarginLeft,
                    PatchMarginRight = hpMask.PatchMarginRight,
                    PatchMarginTop = hpMask.PatchMarginTop,
                    PatchMarginBottom = hpMask.PatchMarginBottom,
                    ClipChildren = CanvasItem.ClipChildrenMode.Only,
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                    AnchorRight = 1f,
                    AnchorBottom = 1f,
                };
                foreground.AddChild(mask);
            }

            if (mask != null
                && foreground.GetNodeOrNull<NinePatchRect>("EmotionFill") is { } originalFill)
            {
                originalFill.Reparent(mask, keepGlobalTransform: false);
            }
        }

        NinePatchRect? fill =
            bar.GetNodeOrNull<NinePatchRect>(
                "EmotionForegroundContainer/Mask/EmotionFill");
        if (fill != null)
        {
            fill.AxisStretchVertical =
                NinePatchRect.AxisStretchMode.Stretch;
            // 血条填充贴图上下带透明像素，沿用原版扩展量后由 Mask 裁切。
            fill.OffsetTop = -4f;
            fill.OffsetBottom = 4f;
        }
    }
}

internal static class SpecialGuestCombatBarLayout
{
    // 情感条与当前混乱条之间保留四像素间距。
    private const float EmotionBarGap = 4f;
    // 情感条整体右移三像素，对齐血条与混乱条的可见左端。
    private const float EmotionBarHorizontalOffset = 3f;
    // 右移前预留四像素，平移后与竖排抗性图标仍保留一像素间距。
    private const float ResistanceIconGap = 4f;
    // 混乱条尚未创建时，使用前置库默认的高度与血条间距。
    private const float DefaultStaggerOffset = 16f;

    public static void ApplyEmotionBar(
        Control bar,
        Control hpBar,
        float barHeight)
    {
        Node parent = hpBar.GetParent();
        Control? staggerBar = parent.GetNodeOrNull<Control>("LibraryOfRuinaStaggerBarContainer");
        float staggerY = staggerBar is { Visible: true }
            ? staggerBar.Position.Y
            : hpBar.Position.Y - DefaultStaggerOffset;
        float right = hpBar.Position.X + hpBar.Size.X;
        ReserveResistanceColumn("LibraryOfRuinaPhysicalResistIcons");
        ReserveResistanceColumn("LibraryOfRuinaChaosResistIcons");

        bar.AnchorLeft = 0f;
        bar.AnchorTop = 0f;
        bar.AnchorRight = 0f;
        bar.AnchorBottom = 0f;
        bar.Position = new Vector2(
            hpBar.Position.X + EmotionBarHorizontalOffset,
            staggerY - EmotionBarGap - barHeight);
        bar.Size = new Vector2(Math.Max(0f, right - hpBar.Position.X), barHeight);
        bar.ZIndex = hpBar.ZIndex;
        bar.ZAsRelative = hpBar.ZAsRelative;

        void ReserveResistanceColumn(string name)
        {
            if (parent.GetNodeOrNull<Control>(name) is { Visible: true } column)
            {
                right = Math.Min(right, column.Position.X - ResistanceIconGap);
            }
        }
    }
}

internal readonly record struct EmotionProgress(
    int Current,
    int Required);

internal sealed partial class SpecialGuestEmotionHoverArea : Control
{
    private NHealthBar? _healthBar;
    private SpecialGuestMonsterBase? _guest;
    private bool _isHovered;
    private bool _signalsConnected;

    public void Initialize(
        NHealthBar healthBar,
        SpecialGuestMonsterBase guest)
    {
        _healthBar = healthBar;
        if (_guest != guest)
        {
            if (_guest != null)
            {
                _guest.EmotionChanged -= OnStateChanged;
            }

            _guest = guest;
            _guest.EmotionChanged += OnStateChanged;
        }

        if (!_signalsConnected)
        {
            MouseEntered += OnMouseEntered;
            MouseExited += OnMouseExited;
            _signalsConnected = true;
        }
    }

    public override void _ExitTree()
    {
        HideHoverTip();
        if (_guest != null)
        {
            _guest.EmotionChanged -= OnStateChanged;
        }
        base._ExitTree();
    }

    private void OnStateChanged()
    {
        Callable.From(() =>
        {
            if (_healthBar != null
                && IsInstanceValid(_healthBar)
                && _guest != null)
            {
                SpecialGuestEmotionBarUi.Refresh(_healthBar, _guest);
            }

            if (_isHovered)
            {
                HideHoverTip();
                ShowHoverTip();
            }
        }).CallDeferred();
    }

    private void OnMouseEntered()
    {
        _isHovered = true;
        ShowHoverTip();
    }

    private void OnMouseExited()
    {
        _isHovered = false;
        HideHoverTip();
    }

    private void ShowHoverTip()
    {
        if (_guest == null)
        {
            return;
        }

        var title = new LocString(
            "gameplay_ui",
            "SPECIAL_GUEST_EMOTION_HOVER.title");
        var description = new LocString(
            "gameplay_ui",
            "SPECIAL_GUEST_EMOTION_HOVER.description");
        EmotionProgress progress =
            SpecialGuestEmotionBarUi.GetEmotionProgress(_guest);
        description.Add("Level", _guest.EmotionLevel);
        description.Add("Current", progress.Current);
        description.Add("Required", progress.Required);

        NHoverTipSet.Remove(this);
        NHoverTipSet.CreateAndShow(
            this,
            new HoverTip(title, description),
            HoverTip.GetHoverTipAlignment(this));
    }

    private void HideHoverTip()
    {
        NHoverTipSet.Remove(this);
    }
}
