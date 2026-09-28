using System;
using System.Collections;
using System.Reflection;
using Godot;
using HarmonyLib;
using LibraryOfRuina.addons.mega_text;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using ISecondaryDisplayAmountPower = LibraryOfRuina.powers.ISecondaryDisplayAmountPower;
using LibraryOfRuina.interop;

namespace LibraryOfRuina.patches;

internal static class PowerSecondaryCounterUi
{
    private const string SecondaryAmountLabelName = "LibraryOfRuinaSecondaryAmountLabel";
    private const string FallbackLabelFontPath = "res://themes/kreon_bold_glyph_space_one.tres";


    public static void EnsureAndRefresh(NPower powerNode)
    {
        if (!IsUsablePowerNode(powerNode))
        {
            return;
        }

        EnsureSecondaryLabel(powerNode);
        SyncPowerNodeVisibility(powerNode);
        RefreshSecondaryLabel(powerNode);
        RefreshHoveredCreaturePowerTipsSafely(powerNode);
    }

    public static void RefreshSecondaryLabel(NPower powerNode)
    {
        MegaLabel? label = GetSecondaryLabel(powerNode);
        if (label == null)
        {
            return;
        }

        PowerModel? model = GetModel(powerNode);
        if (model is not ISecondaryDisplayAmountPower secondaryPower
            || !secondaryPower.ShowSecondaryDisplayAmount
            || !model.IsVisible)
        {
            label.Visible = false;
            label.SetTextAutoSize(string.Empty);
            return;
        }

        label.Visible = true;
        label.AddThemeColorOverride(
            ThemeConstants.Label.fontColor,
            secondaryPower.SecondaryDisplayAmountLabelColor);
        label.SetTextAutoSize(secondaryPower.SecondaryDisplayAmount.ToString());
    }

    public static void SyncPowerNodeVisibility(NPower powerNode)
    {
        PowerModel? model = GetModel(powerNode);
        bool isVisible = model?.IsVisible ?? false;

        powerNode.Visible = isVisible;
        powerNode.MouseFilter = isVisible
            ? Control.MouseFilterEnum.Stop
            : Control.MouseFilterEnum.Ignore;

        if (!isVisible && model?.Owner != null)
        {
            NCombatRoom.Instance?.GetCreatureNode(model.Owner)?.HideHoverTips();
        }
    }

    private static MegaLabel? EnsureSecondaryLabel(NPower powerNode)
    {
        MegaLabel? existing = GetSecondaryLabel(powerNode);
        if (existing != null)
        {
            return existing;
        }

        MegaLabel? amountLabel = powerNode.GetNodeOrNull<MegaLabel>("%AmountLabel");
        MegaLabel label = amountLabel?.Duplicate() as MegaLabel ?? CreateFallbackLabel();
        EnsureThemeFontOverride(label, amountLabel);
        EnsureFallbackThemeFontOverride(label);
        label.Name = SecondaryAmountLabelName;
        label.UniqueNameInOwner = false;
        label.Visible = false;
        label.MouseFilter = Control.MouseFilterEnum.Ignore;
        label.AutoSizeEnabled = false;
        label.OffsetLeft = -56f;
        label.OffsetTop = -4f;
        label.OffsetRight = 44f;
        label.OffsetBottom = 19f;
        label.HorizontalAlignment = HorizontalAlignment.Right;
        label.VerticalAlignment = VerticalAlignment.Top;
        label.SetTextAutoSize(string.Empty);

        powerNode.AddChild(label);
        label.Owner = powerNode;
        return label;
    }

    private static MegaLabel CreateFallbackLabel()
    {
        MegaLabel label = new MegaLabel();
        EnsureFallbackThemeFontOverride(label);

        label.AddThemeFontSizeOverride(ThemeConstants.Label.fontSize, 18);
        label.AddThemeColorOverride(
            ThemeConstants.Label.fontOutlineColor,
            new Color(0.12f, 0.10208f, 0.0816f));
        label.AddThemeColorOverride(
            ThemeConstants.Label.fontShadowColor,
            new Color(0f, 0f, 0f, 0.1882353f));
        label.AddThemeConstantOverride("shadow_offset_x", 3);
        label.AddThemeConstantOverride("shadow_offset_y", 3);
        label.AddThemeConstantOverride("outline_size", 10);
        return label;
    }

    private static void EnsureThemeFontOverride(MegaLabel targetLabel, MegaLabel? sourceLabel)
    {
        if (targetLabel.HasThemeFontOverride(ThemeConstants.Label.font))
        {
            return;
        }

        Font? font = null;
        if (sourceLabel != null)
        {
            font = sourceLabel.GetThemeFont(ThemeConstants.Label.font, "Label");
        }

        if (font == null)
        {
            font = targetLabel.GetThemeFont(ThemeConstants.Label.font, "Label");
        }

        if (font != null)
        {
            targetLabel.AddThemeFontOverride(ThemeConstants.Label.font, font);
        }
    }

    private static void EnsureFallbackThemeFontOverride(MegaLabel label)
    {
        if (label.HasThemeFontOverride(ThemeConstants.Label.font))
        {
            return;
        }

        Font? fallbackFont = label.GetThemeFont(ThemeConstants.Label.font, "Label");
        if (fallbackFont == null)
        {
            fallbackFont = ResourceLoader.Load<Font>(FallbackLabelFontPath);
        }

        if (fallbackFont != null)
        {
            label.AddThemeFontOverride(ThemeConstants.Label.font, fallbackFont);
        }
    }

    private static PowerModel? GetModel(NPower powerNode)
    {
        return VanillaPrivate.PowerNodeModel.Get(powerNode) as PowerModel;
    }

    private static bool IsUsablePowerNode(NPower powerNode)
    {
        return GodotObject.IsInstanceValid(powerNode) && powerNode.IsInsideTree();
    }

    public static void LogPatchFailure(string patchName, NPower powerNode, Exception ex)
    {
        PowerModel? model = GodotObject.IsInstanceValid(powerNode)
            ? GetModel(powerNode)
            : null;
        string path = SafeNodePath(powerNode);
        bool insideTree = GodotObject.IsInstanceValid(powerNode) && powerNode.IsInsideTree();
        string modelId = model?.Id.ToString() ?? "none";
        string ownerType = model?.Owner?.GetType().Name ?? "none";

        GD.PushError(
            $"[LibraryOfRuina] {patchName} failed: {ex.GetType().Name} - {ex.Message}; " +
            $"insideTree={insideTree}; path={path}; model={modelId}; ownerType={ownerType}");
    }

    private static string SafeNodePath(Node node)
    {
        if (!GodotObject.IsInstanceValid(node))
        {
            return "invalid";
        }

        try
        {
            return node.IsInsideTree()
                ? node.GetPath().ToString()
                : node.Name.ToString();
        }
        catch
        {
            return "unknown";
        }
    }

    private static void RefreshHoveredCreaturePowerTipsSafely(NPower powerNode)
    {
        try
        {
            RefreshHoveredCreaturePowerTips(powerNode);
        }
        catch (Exception ex)
        {
            LogHoverTipRefreshFailure(powerNode, ex);
        }
    }

    private static void RefreshHoveredCreaturePowerTips(NPower powerNode)
    {
        PowerModel? model = GetModel(powerNode);
        if (model?.Owner == null)
        {
            return;
        }

        NCreature? creatureNode = NCombatRoom.Instance?.GetCreatureNode(model.Owner);
        Control? hitbox = creatureNode?.Hitbox;
        if (hitbox == null || !HasActiveHoverTips(hitbox))
        {
            return;
        }

        creatureNode!.ShowHoverTips(model.Owner.HoverTips);
    }

    private static void LogHoverTipRefreshFailure(NPower powerNode, Exception ex)
    {
        PowerModel? model = GodotObject.IsInstanceValid(powerNode)
            ? GetModel(powerNode)
            : null;
        string path = SafeNodePath(powerNode);
        string modelId = model?.Id.ToString() ?? "none";
        string ownerType = model?.Owner?.GetType().Name ?? "none";

        GD.PushWarning(
            $"[LibraryOfRuina] PowerSecondaryCounter hover tip refresh skipped: {ex.GetType().Name} - {ex.Message}; " +
            $"path={path}; model={modelId}; ownerType={ownerType}");
    }

    private static bool HasActiveHoverTips(Control owner)
    {
        object? activeObj = VanillaPrivate.HoverTipSetActiveHoverTips.Get();
        if (activeObj is IDictionary<Control, NHoverTipSet> activeTyped)
        {
            return activeTyped.ContainsKey(owner);
        }

        return activeObj is IDictionary active && active.Contains(owner);
    }

    private static MegaLabel? GetSecondaryLabel(NPower powerNode)
    {
        return powerNode.GetNodeOrNull<MegaLabel>(SecondaryAmountLabelName);
    }
}

[HarmonyPatch(typeof(NPower), nameof(NPower._Ready))]
public static class PowerSecondaryCounterReadyPatch
{
    [HarmonyPostfix]
    public static void Postfix(NPower __instance)
    {
        try
        {
            PowerSecondaryCounterUi.EnsureAndRefresh(__instance);
        }
        catch (Exception ex)
        {
            PowerSecondaryCounterUi.LogPatchFailure(nameof(PowerSecondaryCounterReadyPatch), __instance, ex);
        }
    }
}

[HarmonyPatch(typeof(NPower), "RefreshAmount")]
public static class PowerSecondaryCounterRefreshPatch
{
    [HarmonyPostfix]
    public static void Postfix(NPower __instance)
    {
        try
        {
            PowerSecondaryCounterUi.EnsureAndRefresh(__instance);
        }
        catch (Exception ex)
        {
            PowerSecondaryCounterUi.LogPatchFailure(nameof(PowerSecondaryCounterRefreshPatch), __instance, ex);
        }
    }
}
