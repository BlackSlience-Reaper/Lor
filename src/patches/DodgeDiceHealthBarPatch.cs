using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using LibraryOfRuina.addons.mega_text;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.infra.patching;
using LibraryOfRuina.interop;
using LibraryOfRuina.powers;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.patches;

[HarmonyPatch(typeof(NHealthBar), "_Ready")]
internal static class DodgeDiceHealthBarReadyPatch
{
    internal sealed class State
    {
        public Texture2D? DefaultBlockIconTexture { get; set; }

        public Texture2D? DodgeBlockIconTexture { get; set; }
    }

    private const string BlockIconNodeName = "BlockIcon";

    private const string DodgeBlockIconPath = "res://images/ui/combat/dodge_dice_block.png";



    internal static readonly ConditionalWeakTable<NHealthBar, State> States = new();

    [HarmonyPostfix]
    private static void Postfix(NHealthBar __instance)
    {
        try
        {
            States.Remove(__instance);
            State state = new();
            States.Add(__instance, state);
            CaptureDefaultBlockIconTexture(__instance, state);
        }
        catch (Exception exception)
        {
            PatchFailureLog.Warn(
                "DodgeDiceHealthBar.Ready",
                exception);
        }
    }

    internal static void Apply(NHealthBar healthBar, Creature? creature, bool useDodgeIcon)
    {
        try
        {
            State state = States.GetValue(healthBar, static _ => new State());
            if (!TryGetBlockNodes(healthBar, out Control? blockContainer, out MegaLabel? blockLabel))
            {
                return;
            }

            TextureRect? blockIcon = TryGetBlockIcon(blockContainer!);
            if (blockIcon == null)
            {
                return;
            }

            if (!GodotTextureSafety.IsValid(state.DefaultBlockIconTexture)
                && GodotTextureSafety.IsValid(blockIcon.Texture))
            {
                state.DefaultBlockIconTexture = blockIcon.Texture;
            }

            bool hasBlock = creature is { Block: > 0 };
            if (useDodgeIcon)
            {
                GodotTextureSafety.TrySetTexture(blockIcon, GetDodgeBlockIconTexture(state));
                blockIcon.Visible = hasBlock;
                if (hasBlock)
                {
                    blockContainer!.Visible = true;
                    if (blockLabel != null)
                    {
                        blockLabel.Visible = true;
                        blockLabel.SetTextAutoSize(creature!.Block.ToString());
                    }
                }

                return;
            }

            if (GodotTextureSafety.IsValid(state.DefaultBlockIconTexture))
            {
                GodotTextureSafety.TrySetTexture(blockIcon, state.DefaultBlockIconTexture);
            }

            blockIcon.Visible = blockContainer!.Visible;
        }
        catch (Exception exception)
        {
            PatchFailureLog.Warn(
                "DodgeDiceHealthBar.Apply",
                exception);
        }
    }

    private static void CaptureDefaultBlockIconTexture(NHealthBar healthBar, State state)
    {
        if (!TryGetBlockNodes(healthBar, out Control? blockContainer, out _))
        {
            return;
        }

        Texture2D? defaultTexture = TryGetBlockIcon(blockContainer!)?.Texture;
        if (!GodotTextureSafety.IsValid(state.DefaultBlockIconTexture)
            && GodotTextureSafety.IsValid(defaultTexture))
        {
            state.DefaultBlockIconTexture = defaultTexture;
        }
    }

    private static TextureRect? TryGetBlockIcon(Control blockContainer)
    {
        return blockContainer.GetNodeOrNull<TextureRect>(BlockIconNodeName);
    }

    private static Texture2D? GetDodgeBlockIconTexture(State state)
    {
        if (!GodotTextureSafety.IsValid(state.DodgeBlockIconTexture))
        {
            state.DodgeBlockIconTexture = ResourceLoader.Load<Texture2D>(DodgeBlockIconPath);
        }

        return state.DodgeBlockIconTexture;
    }

    private static bool TryGetBlockNodes(
        NHealthBar healthBar,
        out Control? blockContainer,
        out MegaLabel? blockLabel)
    {
        blockContainer = VanillaPrivate.HealthBarBlockContainer.Get(healthBar) as Control;
        blockLabel = VanillaPrivate.HealthBarBlockLabel.Get(healthBar) as MegaLabel;
        return blockContainer != null;
    }
}

[HarmonyPatch(typeof(NHealthBar), "RefreshBlockUi")]
internal static class DodgeDiceHealthBarRefreshPatch
{
    [HarmonyPostfix]
    private static void Postfix(NHealthBar __instance, Creature ____creature)
    {
        DodgeDiceHealthBarReadyPatch.Apply(
            __instance,
            ____creature,
            LibraryOfRuinaDodgeDicePower.ShouldUseDodgeBlockIcon(____creature));
    }
}

[HarmonyPatch(typeof(NHealthBar), nameof(NHealthBar.AnimateInBlock))]
[LibraryPatch(Reason = "原版格挡淡入由 BlockChanged 事件直接驱动血条，没有 Hook；只消耗本模组闪避骰获得格挡时入队的计数，改用闪避图标显示。")]
internal static class DodgeDiceHealthBarAnimateInBlockPatch
{
    [HarmonyPrefix]
    private static bool Prefix(NHealthBar __instance, Creature ____creature)
    {
        if (!DodgeDiceCombatFeedback.TryConsumeDodgeBlockGain(____creature))
        {
            return true;
        }

        DodgeDiceHealthBarReadyPatch.Apply(
            __instance,
            ____creature,
            LibraryOfRuinaDodgeDicePower.ShouldUseDodgeBlockIcon(____creature));
        return false;
    }
}
