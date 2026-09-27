using System;
using Godot;
using HarmonyLib;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.monsters.BlueStar;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.patches.BlueStar;

[HarmonyPatch(
    typeof(LibraryCreature),
    nameof(LibraryCreature.SaveAndSetStunResistance))]
internal static class BlueStarFollowerStunResistancePatch
{
    [HarmonyPrefix]
    private static void Prefix(
        LibraryCreature __instance,
        out LibraryCreatureResistanceData.Resistance? __state)
    {
        __state = __instance.Monster is BlueStarFollower
            ? new LibraryCreatureResistanceData.Resistance(
                __instance.ResistanceData.PhysicalResistance)
            : null;
    }

    [HarmonyPostfix]
    private static void Postfix(
        LibraryCreature __instance,
        LibraryCreatureResistanceData.Resistance? __state)
    {
        if (__state == null || __instance.Monster is not BlueStarFollower)
        {
            return;
        }

        __instance.ResistanceData.PhysicalResistance =
            new LibraryCreatureResistanceData.Resistance(__state);
        __instance.HealthBar?.RefreshValues();
    }
}

internal static class BlueStarChaoThresholdForecast
{
    private const string OverlayName = "BlueStarChaoThresholdForecast";
    private const float ThresholdRatio =
        BlueStarFollower.SelfDestructThresholdPercent / 100f;
    private static readonly Color ThresholdColor =
        new(8f / 255f, 42f / 255f, 120f / 255f, 0.90f);

    internal static void Refresh(NHealthBar healthBar, Creature creature)
    {
        Control? root = healthBar.HpBarContainer?.GetParent() as Control;
        Control? staggerBar = root?.GetNodeOrNull<Control>(
            "LibraryOfRuinaStaggerBarContainer");
        NinePatchRect? existing = staggerBar?.GetNodeOrNull<NinePatchRect>(
            "StaggerForegroundContainer/StaggerMask/" + OverlayName);

        bool shouldShow = creature.IsAlive
            && creature.Monster is BlueStarFollower
            && creature is LibraryCreature
            {
                MaxChaoValue: > 0
            };
        if (!shouldShow)
        {
            if (existing != null)
            {
                existing.Visible = false;
            }

            return;
        }

        Control? mask = staggerBar?.GetNodeOrNull<Control>(
            "StaggerForegroundContainer/StaggerMask");
        NinePatchRect? fill = mask?.GetNodeOrNull<NinePatchRect>(
            "StaggerFill");
        if (mask == null || fill == null)
        {
            return;
        }

        NinePatchRect overlay = existing ?? CreateOverlay(fill, mask);
        overlay.Visible = true;
        overlay.SelfModulate = ThresholdColor;
        overlay.AnchorLeft = 0f;
        overlay.AnchorRight = ThresholdRatio;
        overlay.AnchorTop = 0f;
        overlay.AnchorBottom = 1f;
        overlay.OffsetLeft = 0f;
        overlay.OffsetRight = 0f;
        overlay.OffsetTop = -4f;
        overlay.OffsetBottom = 4f;
        overlay.ZIndex = fill.ZIndex + 1;

        int targetIndex = Math.Min(
            mask.GetChildCount() - 1,
            fill.GetIndex() + 1);
        if (overlay.GetIndex() != targetIndex)
        {
            mask.MoveChild(overlay, targetIndex);
        }
    }

    private static NinePatchRect CreateOverlay(
        NinePatchRect fill,
        Control mask)
    {
        var overlay = (NinePatchRect)fill.Duplicate();
        overlay.Name = OverlayName;
        overlay.MouseFilter = Control.MouseFilterEnum.Ignore;
        overlay.Material = null;
        overlay.Modulate = Colors.White;
        foreach (Node child in overlay.GetChildren())
        {
            child.QueueFree();
        }

        mask.AddChild(overlay);
        return overlay;
    }
}

[HarmonyPatch(typeof(NHealthBar), "RefreshForeground")]
[HarmonyAfter("LibraryOfRuinaLib")]
internal static class BlueStarChaoThresholdForecastRefreshPatch
{
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(
        NHealthBar __instance,
        Creature ____creature)
    {
        BlueStarChaoThresholdForecast.Refresh(__instance, ____creature);
    }
}

[HarmonyPatch(
    typeof(NHealthBar),
    "SetHpBarContainerSizeWithOffsetsImmediately")]
[HarmonyAfter("LibraryOfRuinaLib")]
internal static class BlueStarChaoThresholdForecastResizePatch
{
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(
        NHealthBar __instance,
        Creature ____creature)
    {
        BlueStarChaoThresholdForecast.Refresh(__instance, ____creature);
    }
}
