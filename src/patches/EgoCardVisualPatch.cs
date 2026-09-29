using Godot;
using HarmonyLib;
using LibraryOfRuina.cards;
using LibraryOfRuina.framework.cards;
using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.infra.patching;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace LibraryOfRuina.patches;

[HarmonyPatch(typeof(NCard), "Reload")]
internal static class EgoCardVisualPatch
{
    private const string BackgroundNodeName = "LibraryOfRuinaEgoCardBackground";
    private const string BackgroundPath = "ui/cards/ego/background_attack.png";
    private const string PortraitFramePath = "ui/cards/ego/attack_frame.png";
    internal const string EnergyPath = "ui/cards/ego/energy.png";
    private const string CharacterEgoRoot = "ui/cards/character_ego/";
    internal const string CharacterEgoEnergyPath =
        CharacterEgoRoot + "energy.png";

    // D:\sls2-resource\LOR.jar.src\lor\patches\ColorfulCardRender.java
    // applies this exact render color to E.G.O card backgrounds and energy.
    private static readonly Color LorEgoRenderTint = new("DC2828FF");

    // Keep this aligned with RolandMod's RolandCardFramePatch.FrameOutset.
    // The source card frame is 300x422, so the final layout is 324x456.
    private static readonly Vector2 CharacterEgoFrameOutset =
        new(12f, 17f);

    private static readonly Dictionary<string, Texture2D?> TextureCache = [];

    [HarmonyPostfix]
    private static void Postfix(NCard __instance)
    {
        if (!__instance.IsNodeReady())
        {
            return;
        }

        TextureRect? background = GetExistingBackground(__instance);
        bool isAbnormalityEgo =
            __instance.Model?.VisualCardPool is LibraryOfRuinaEgoCardPool;
        bool isCharacterEgo =
            __instance.Model?.VisualCardPool
                is LibraryOfRuinaCharacterEgoCardPool;
        if (!isAbnormalityEgo && !isCharacterEgo)
        {
            if (background != null)
            {
                background.Visible = false;
                background.SelfModulate = Colors.White;
            }

            if (__instance.GetNodeOrNull<TextureRect>("%Frame") is { } nativeFrame)
            {
                nativeFrame.Visible =
                    __instance.Model?.Rarity != CardRarity.Ancient;
            }
            if (__instance.GetNodeOrNull<TextureRect>("%PortraitBorder")
                is { } nativePortraitBorder)
            {
                nativePortraitBorder.Visible =
                    __instance.Model?.Rarity != CardRarity.Ancient;
            }
            if (__instance.GetNodeOrNull<TextureRect>("%EnergyIcon")
                is { } nativeEnergyIcon)
            {
                nativeEnergyIcon.SelfModulate = Colors.White;
            }

            return;
        }

        TextureRect? frame = __instance.GetNodeOrNull<TextureRect>("%Frame");
        TextureRect? portraitBorder = __instance.GetNodeOrNull<TextureRect>("%PortraitBorder");
        TextureRect? energyIcon = __instance.GetNodeOrNull<TextureRect>("%EnergyIcon");
        if (frame == null || portraitBorder == null || energyIcon == null)
        {
            return;
        }

        string backgroundPath = isCharacterEgo
            ? ResolveCharacterEgoBackgroundPath(__instance.Model!.Type)
            : BackgroundPath;
        string energyPath = isCharacterEgo
            ? CharacterEgoEnergyPath
            : EnergyPath;

        background = EnsureBackground(__instance, frame);
        bool hasCustomBackground = false;
        if (background != null)
        {
            CopyFrameLayout(frame, background);
            if (isCharacterEgo)
            {
                ExpandLayout(background, CharacterEgoFrameOutset);
            }
            hasCustomBackground = GodotTextureSafety.TrySetTexture(
                background,
                LoadTexture(backgroundPath));
            background.Visible = hasCustomBackground;
            background.SelfModulate = isCharacterEgo
                ? LorEgoRenderTint
                : Colors.White;
        }

        // Keep the native frame as a safe fallback if a packaged E.G.O
        // background is ever missing or invalid.
        frame.Visible = !hasCustomBackground;
        portraitBorder.Visible = true;
        if (isAbnormalityEgo)
        {
            if (GodotTextureSafety.TrySetTexture(
                portraitBorder,
                LoadTexture(PortraitFramePath)))
            {
                portraitBorder.Material = null;
            }
        }
        GodotTextureSafety.TrySetTexture(
            energyIcon,
            LoadTexture(energyPath));
        energyIcon.SelfModulate = isCharacterEgo
            ? LorEgoRenderTint
            : Colors.White;
    }

    private static string ResolveCharacterEgoBackgroundPath(CardType cardType)
    {
        string typeName = cardType switch
        {
            CardType.Attack => "attack",
            CardType.Skill => "skill",
            CardType.Power => "power",
            _ => "skill",
        };
        return CharacterEgoRoot + "background_" + typeName + ".png";
    }

    private static TextureRect? GetExistingBackground(NCard card)
    {
        return card.GetNodeOrNull<TextureRect>("CardContainer/" + BackgroundNodeName);
    }

    private static TextureRect? EnsureBackground(NCard card, TextureRect frame)
    {
        TextureRect? background = GetExistingBackground(card);
        if (background != null)
        {
            return background;
        }

        if (frame.GetParent() is not Control parent)
        {
            return null;
        }

        background = new TextureRect
        {
            Name = BackgroundNodeName,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale
        };

        parent.AddChild(background);
        Node? portraitCanvas = card.GetNodeOrNull("%PortraitCanvasGroup");
        int targetIndex = portraitCanvas?.GetIndex() ?? frame.GetIndex();
        parent.MoveChild(background, targetIndex);
        return background;
    }

    private static void CopyFrameLayout(TextureRect frame, TextureRect target)
    {
        target.SetAnchorsPreset(Control.LayoutPreset.Center);
        target.OffsetLeft = frame.OffsetLeft;
        target.OffsetTop = frame.OffsetTop;
        target.OffsetRight = frame.OffsetRight;
        target.OffsetBottom = frame.OffsetBottom;
        target.GrowHorizontal = frame.GrowHorizontal;
        target.GrowVertical = frame.GrowVertical;
        target.PivotOffset = frame.PivotOffset;
    }

    private static void ExpandLayout(Control target, Vector2 outset)
    {
        target.OffsetLeft -= outset.X;
        target.OffsetTop -= outset.Y;
        target.OffsetRight += outset.X;
        target.OffsetBottom += outset.Y;
        target.PivotOffset += outset;
    }

    private static Texture2D? LoadTexture(string imagePath)
    {
        if (!TextureCache.TryGetValue(imagePath, out Texture2D? cached)
            || !GodotTextureSafety.IsValid(cached))
        {
            cached = ResourceLoader.Load<Texture2D>(ImageHelper.GetImagePath(imagePath));
            TextureCache[imagePath] = cached;
        }

        return cached;
    }
}

[HarmonyPatch(typeof(EnergyIconHelper), nameof(EnergyIconHelper.GetPath), typeof(string))]
[LibraryPatch(Reason = "EnergyIconHelper.GetPath 是静态方法；只处理本模组两个 E.G.O. 卡池的能量颜色名。可改为卡池实现 RitsuLib 的 IModBigEnergyIconPool，暂缓。")]
internal static class EgoEnergyIconPathPatch
{
    [HarmonyPrefix]
    private static bool Prefix(string prefix, ref string __result)
    {
        string? imagePath = prefix.ToLowerInvariant() switch
        {
            "ego" => EgoCardVisualPatch.EnergyPath,
            "character_ego" => EgoCardVisualPatch.CharacterEgoEnergyPath,
            _ => null,
        };
        if (imagePath == null)
        {
            return true;
        }

        __result = ImageHelper.GetImagePath(imagePath);
        return false;
    }
}
