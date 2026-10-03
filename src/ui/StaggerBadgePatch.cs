using System.Collections.Generic;
using System.Linq;
using Godot;
using HarmonyLib;
using LibraryOfRuina.core.settings;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.ui;

/// <summary>混乱抗性条的画法，见模组设置“战斗界面”。存档里按名字保存，只能追加新值、不能改名。</summary>
internal enum StaggerBarStyle
{
    /// <summary>前置库原样：体力条上方一条等宽的混乱条，数字居中。</summary>
    Default,

    /// <summary>贴在体力条上沿的细条，数字收进体力条右端的徽章。</summary>
    Badge,
}

/// <summary>
/// 前置库在原版体力条正上方另画一条与体力条等宽的混乱值条，数字居中，和体力数字上下压在一起。
/// 这里把它改成贴在体力条上沿的一道细条，数字收进体力条右端的徽章。徽章照原版格挡徽章
/// （health_bar.tscn 的 BlockContainer：60×60、kreon 24 号字、描边 14）镜像放在右端，
/// 图标是同画风的金色横向六边形（tools/make_stagger_badge_icon.py 生成）；物理、混乱两组抗性图标从血条右侧挪到徽章正上方竖排。体力条与能力图标的位置都不动。
/// 只在设置“混乱抗性条样式”选了徽章（默认）时生效，选“默认”项则保持前置库原样。
/// 前置库每次刷新都会重设混乱条与抗性图标的位置、重写数字，所以挂在它的刷新入口后面跟着重排。
/// </summary>
internal static class StaggerBadge
{
    private const string StaggerContainerName = "LibraryOfRuinaStaggerBarContainer";
    private const string StaggerLabelName = "StaggerValueLabel";
    private const string BadgeName = "LorStaggerBadge";
    private const string IconPath = "res://images/ui/combat/stagger_badge.png";
    private const string PhysicalColumnName = "LibraryOfRuinaPhysicalResistIcons";
    private const string ChaosColumnName = "LibraryOfRuinaChaosResistIcons";

    // 前置库混乱条高度为 14；徽章样式由原来的 7 增厚 50% 至 10.5，底边继续贴合体力条。
    private const float StripScale = 0.75f;
    private const float StripOverlap = 1f;
    // 以下取自原版格挡徽章：容器 60×60，中心落在体力条端点往里 12 像素、条顶往下 4 像素处。
    private const float BadgeSize = 60f;
    private const float BadgeInset = 12f;
    private const float BadgeCenterBelowBarTop = 4f;
    private const int FontSize = 24;
    private const int OutlineSize = 14;
    private static readonly Color OutlineColor = new(0.42f, 0.27f, 0.0f, 1f);
    // 前置库的抗性图标每组 3 个、28 像素见方、间距 1，竖排成一列；默认物理组在上、混乱组紧接在下，整体贴在体力条右端外侧。
    // 有徽章时整列居中挪到徽章正上方，底端离六边形上沿（贴图 128 像素里第 26 行）留一点空。
    private const float ResistIconSize = 28f;
    private const float ResistColumnHeight = 3 * 29f - 1f;
    private const float ResistGroupGap = 1f;
    private const float BadgeShapeTop = BadgeSize * 26f / 128f;
    private const float ResistAboveBadgeGap = 3f;

    internal static void Apply(NHealthBar healthBar)
    {
        Control? hpBar = healthBar.HpBarContainer;
        Node? parent = hpBar?.GetParent();
        if (hpBar == null || parent == null)
        {
            return;
        }

        Control? strip = parent.GetNodeOrNull<Control>(StaggerContainerName);
        Control? badge = parent.GetNodeOrNull<Control>(BadgeName);
        if (strip is not { Visible: true } || strip.GetNodeOrNull<Label>(StaggerLabelName) is not { } libLabel)
        {
            if (badge != null)
            {
                badge.Visible = false;
            }

            return;
        }

        if (LibraryOfRuinaSettings.StaggerBarStyle != StaggerBarStyle.Badge)
        {
            // 默认样式就是前置库原样；从徽章样式切回来时把改过的地方还原，位置与尺寸由前置库每次刷新自己重设。
            strip.Scale = Vector2.One;
            libLabel.Visible = true;
            if (badge != null)
            {
                badge.Visible = false;
            }

            return;
        }

        float stripHeight = strip.Size.Y * StripScale;
        strip.Scale = new Vector2(1f, StripScale);
        strip.Position = new Vector2(
            hpBar.Position.X,
            hpBar.Position.Y - stripHeight + StripOverlap);
        libLabel.Visible = false;

        badge ??= CreateBadge(parent, healthBar);
        badge.Visible = true;
        float barRight = hpBar.Position.X + hpBar.Size.X;
        badge.Position = new Vector2(
            barRight - BadgeInset - BadgeSize * 0.5f,
            hpBar.Position.Y + BadgeCenterBelowBarTop - BadgeSize * 0.5f);

        string text = libLabel.Text;
        int slash = text.IndexOf('/');
        var value = badge.GetNode<Label>("Value");
        value.Text = slash > 0 ? text[..slash] : text;
        // 前置库在混乱状态下把数字描边换成暗红，徽章跟着换。
        Color libOutline = libLabel.GetThemeColor("font_outline_color");
        bool stunned = libOutline.R > libOutline.G * 2f;
        value.AddThemeColorOverride("font_outline_color", stunned ? libOutline : OutlineColor);
        badge.GetNode<CanvasItem>("Icon").SelfModulate = stunned ? new Color(1f, 0.45f, 0.4f) : Colors.White;

        MoveResistColumns(healthBar);
    }

    /// <summary>
    /// 徽章显示时把两组抗性图标挪到徽章正上方：混乱组贴着徽章，物理组叠在它上面；混乱组隐藏时物理组直接贴着徽章。
    /// 前置库只在抗性图标各自的 Refresh 里重设它们的位置（血条刷新、瞄准预览等都会走到），
    /// 那里也挂了补丁调用这里，所以哪条路径最后执行都能重新摆好。
    /// </summary>
    internal static void MoveResistColumns(NHealthBar healthBar)
    {
        if (healthBar.HpBarContainer?.GetParent() is not { } parent
            || parent.GetNodeOrNull<Control>(BadgeName) is not { Visible: true } badge)
        {
            return;
        }

        float x = badge.Position.X + (BadgeSize - ResistIconSize) * 0.5f;
        float bottom = badge.Position.Y + BadgeShapeTop - ResistAboveBadgeGap;
        if (parent.GetNodeOrNull<Control>(ChaosColumnName) is { Visible: true } chaos)
        {
            chaos.Position = new Vector2(x, bottom - ResistColumnHeight);
            bottom -= ResistColumnHeight + ResistGroupGap;
        }

        if (parent.GetNodeOrNull<Control>(PhysicalColumnName) is { } physical)
        {
            physical.Position = new Vector2(x, bottom - ResistColumnHeight);
        }
    }

    private static Control CreateBadge(Node parent, NHealthBar healthBar)
    {
        var badge = new Control
        {
            Name = BadgeName,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Size = new Vector2(BadgeSize, BadgeSize),
        };

        var icon = new TextureRect
        {
            Name = "Icon",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Texture = ResourceLoader.Load<Texture2D>(IconPath),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            AnchorRight = 1f,
            AnchorBottom = 1f,
        };
        badge.AddChild(icon);

        // 与原版 BlockLabel 相同的锚点与偏移。
        var value = new Label
        {
            Name = "Value",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AnchorRight = 1f,
            AnchorBottom = 1f,
            OffsetLeft = 10f,
            OffsetTop = -5f,
            OffsetRight = -9f,
            OffsetBottom = 11f,
        };
        if (healthBar.GetNodeOrNull<Label>("%HpLabel") is { } hpLabel)
        {
            value.AddThemeFontOverride("font", hpLabel.GetThemeFont("font"));
            value.AddThemeColorOverride("font_color", hpLabel.GetThemeColor("font_color"));
        }

        value.AddThemeFontSizeOverride("font_size", FontSize);
        value.AddThemeConstantOverride("outline_size", OutlineSize);
        value.AddThemeColorOverride("font_outline_color", OutlineColor);
        badge.AddChild(value);

        parent.AddChild(badge);
        return badge;
    }
}

/// <summary>
/// 挂在前置库自己的刷新入口后面：它既在 NHealthBar.RefreshForeground 之后调用，也在混乱值变化时单独调用。
/// 该类型在前置库里是 internal，只能按名字解析。
/// </summary>
[HarmonyPatch]
internal static class StaggerBadgeRefreshPatch
{
    private const string LibRefreshType = "LibraryLib.Patches.LibraryStaggerResistanceBarUi";

    private static bool Prepare() => TargetMethod() != null;

    private static System.Reflection.MethodBase? TargetMethod() =>
        AccessTools.TypeByName(LibRefreshType) is { } type
            ? AccessTools.Method(type, "Refresh", [typeof(NHealthBar)])
            : null;

    [HarmonyPostfix]
    private static void Postfix(NHealthBar? healthBar)
    {
        if (healthBar != null)
        {
            StaggerBadge.Apply(healthBar);
        }
    }
}

/// <summary>
/// 挂在前置库两列抗性图标各自的刷新入口后面：瞄准预览等路径只刷新抗性图标、不刷新混乱条，
/// 会把图标列的横坐标复位，这里跟着重新让位。两个类型在前置库里都是 internal，只能按名字解析。
/// </summary>
[HarmonyPatch]
internal static class StaggerBadgeResistIconsPatch
{
    private static readonly string[] LibIconTypes =
    [
        "LibraryLib.Patches.LibraryPhysicalResistanceIconsUi",
        "LibraryLib.Patches.LibraryChaosResistanceIconsUi",
    ];

    private static bool Prepare() => TargetMethods().Any();

    private static IEnumerable<System.Reflection.MethodBase> TargetMethods() =>
        LibIconTypes
            .Select(static name => AccessTools.TypeByName(name))
            .Where(static type => type != null)
            .Select(static type => (System.Reflection.MethodBase?)AccessTools.Method(type, "Refresh", [typeof(NHealthBar)]))
            .Where(static method => method != null)
            .Select(static method => method!);

    [HarmonyPostfix]
    private static void Postfix(NHealthBar? healthBar)
    {
        if (healthBar != null)
        {
            StaggerBadge.MoveResistColumns(healthBar);
        }
    }
}
