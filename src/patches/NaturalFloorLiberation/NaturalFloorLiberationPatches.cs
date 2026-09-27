using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using LibraryOfRuina.backgrounds.NaturalFloorLiberation;
using LibraryOfRuina.encounters.NaturalFloorLiberation;
using LibraryOfRuina.events.NaturalFloorLiberation;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.patches.NaturalFloorLiberation;

[HarmonyPatch(typeof(NCombatRoom), nameof(NCombatRoom.SetUpBackground))]
internal static class NaturalFloorAnimatedBackgroundPatch
{
    private static void Postfix(NCombatRoom __instance, ICombatRoomVisuals ____visuals)
    {
        if (____visuals.Encounter is not NaturalFloorLiberationEncounter encounter
            || __instance.Background is not { } background)
        {
            return;
        }

        // 原版 AddChildSafely 可能延迟挂载背景图层，等待整棵背景节点就绪再绑定阶段。
        if (background.IsNodeReady())
        {
            AttachPhaseBackground(background, encounter);
        }
        else
        {
            background.Connect(Node.SignalName.Ready,
                Callable.From(() => AttachPhaseBackground(background, encounter)),
                (uint)GodotObject.ConnectFlags.OneShot);
        }
    }

    private static void AttachPhaseBackground(
        NCombatBackground background,
        NaturalFloorLiberationEncounter encounter)
    {
        if (background.GetNodeOrNull<TextureRect>("Layer_00/A") is not { } layer)
        {
            Log.Warn($"[NaturalFloorBackground] Phase {encounter.CurrentPhase}: background layer Layer_00/A is missing.");
            return;
        }

        if (layer.GetNodeOrNull("AnimatedBackground") != null)
        {
            return;
        }

        // Construct the mod script directly; exported scenes retain a scriptless fallback.
        var animation = new NaturalFloorLiberationBackground
        {
            Name = "AnimatedBackground",
            Encounter = encounter,
            Texture = layer.Texture,
            ExpandMode = layer.ExpandMode,
            StretchMode = layer.StretchMode,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        layer.AddChild(animation);
        animation.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        animation.SetPhaseBackground(encounter.TransitionPending ? encounter.CurrentPhase - 1 : encounter.CurrentPhase);
        Log.Info($"[NaturalFloorBackground] Bound phase={encounter.CurrentPhase}, texture={animation.Texture?.ResourcePath}.");
    }
}

[HarmonyPatch(typeof(AncientEventModel), "BeforeEventStarted")]
internal static class NaturalFloorSettlementAncientHealPatch
{
    private static bool Prefix(AncientEventModel __instance, ref Task __result)
    {
        if (__instance is not NaturalFloorLiberationSettlementEvent) return true;
        __result = Task.CompletedTask;
        return false;
    }
}
[HarmonyPatch(typeof(Hook), nameof(Hook.ShouldAllowAncient))]
internal static class NaturalFloorSettlementAncientGatePatch
{
    private static bool Prefix(AncientEventModel ancient, ref bool __result)
    {
        if (ancient is not NaturalFloorLiberationSettlementEvent) return true;
        __result = true;
        return false;
    }
}
// Terminal phase victory can be detected before AfterDeath, so both paths commit the same idempotent result.
[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCombatVictory))]
internal static class NaturalFloorVictoryPatch
{
    private static void Prefix(CombatRoom room)
    {
        if (room.Encounter is NaturalFloorLiberationEncounter { SettlementTriggered: true } encounter)
            NaturalFloorLiberationSettlementStore.Record(encounter);
    }
}
[HarmonyPatch(typeof(RunManager), nameof(RunManager.ProceedFromTerminalRewardsScreen))]
internal static class NaturalFloorSettlementRedirectPatch
{
    private static bool Prefix(RunManager __instance, ref Task __result)
    {
        if (__instance.DebugOnlyGetState()?.CurrentRoom is not CombatRoom
            { Encounter: NaturalFloorLiberationEncounter encounter } || !encounter.SettlementTriggered) return true;
        LiberationSettlementProceedHelper.ClearLingeringCardPreviews();
        NaturalFloorLiberationSettlementStore.Record(encounter);
        if (!NaturalFloorLiberationSettlementStore.PendingSettlement) return true;
        NaturalFloorLiberationSettlementStore.Consume();
        __result = __instance.EnterRoom(new EventRoom(ModelDb.AncientEvent<NaturalFloorLiberationSettlementEvent>()));
        return false;
    }
}
[HarmonyPatch(typeof(NEventRoom), nameof(NEventRoom.Proceed))]
internal static class NaturalFloorSettlementProceedPatch
{
    private static bool Prefix(ref Task __result)
    {
        if (RunManager.Instance.DebugOnlyGetState()?.CurrentRoom is not EventRoom
            { CanonicalEvent: NaturalFloorLiberationSettlementEvent }) return true;
        __result = RunManager.Instance.EnterNextAct();
        return false;
    }
}
