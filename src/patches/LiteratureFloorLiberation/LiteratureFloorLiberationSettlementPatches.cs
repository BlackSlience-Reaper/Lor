using System.Threading.Tasks;
using HarmonyLib;
using LibraryOfRuina.encounters.LiteratureFloorLiberation;
using LibraryOfRuina.events.LiteratureFloorLiberation;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.patches.LiteratureFloorLiberation;

[HarmonyPatch(typeof(AncientEventModel), "BeforeEventStarted")]
internal static class LiteratureFloorLiberationSettlementAncientHealPatch
{
    private static bool Prefix(
        AncientEventModel __instance,
        ref Task __result)
    {
        if (__instance is not LiteratureFloorLiberationSettlementEvent)
        {
            return true;
        }

        __result = Task.CompletedTask;
        return false;
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.ShouldAllowAncient))]
internal static class LiteratureFloorLiberationSettlementAncientGatePatch
{
    private static bool Prefix(
        AncientEventModel ancient,
        ref bool __result)
    {
        if (ancient is not LiteratureFloorLiberationSettlementEvent)
        {
            return true;
        }

        __result = true;
        return false;
    }
}

[HarmonyPatch(
    typeof(RunManager),
    nameof(RunManager.ProceedFromTerminalRewardsScreen))]
internal static class LiteratureFloorLiberationSettlementRedirectPatch
{
    private static bool Prefix(
        RunManager __instance,
        ref Task __result)
    {
        if (__instance.DebugOnlyGetState()?.CurrentRoom
            is not CombatRoom
            {
                Encounter: LiteratureFloorLiberationEncounter encounter
            })
        {
            return true;
        }

        LiberationSettlementProceedHelper.ClearLingeringCardPreviews();

        if (!LiteratureFloorLiberationSettlementStore.PendingSettlement
            && encounter.KilledBossCount
            >= LiteratureFloorLiberationSettlementStore
                .MinimumKilledBossCount)
        {
            LiteratureFloorLiberationSettlementStore.Record(encounter);
        }

        if (!LiteratureFloorLiberationSettlementStore.PendingSettlement)
        {
            return true;
        }

        LiteratureFloorLiberationSettlementStore.Consume();
        __result = __instance.EnterRoom(
            new EventRoom(
                ModelDb.AncientEvent<
                    LiteratureFloorLiberationSettlementEvent>()));
        return false;
    }
}

[HarmonyPatch(typeof(NEventRoom), nameof(NEventRoom.Proceed))]
internal static class LiteratureFloorLiberationProceedPatch
{
    private static bool Prefix(ref Task __result)
    {
        if (RunManager.Instance.DebugOnlyGetState()?.CurrentRoom
            is not EventRoom
            {
                CanonicalEvent:
                    LiteratureFloorLiberationSettlementEvent
            })
        {
            return true;
        }

        __result = RunManager.Instance.EnterNextAct();
        return false;
    }
}
