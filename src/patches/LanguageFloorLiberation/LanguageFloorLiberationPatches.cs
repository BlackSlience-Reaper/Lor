using System.Threading.Tasks;
using HarmonyLib;
using LibraryOfRuina.encounters.LanguageFloorLiberation;
using LibraryOfRuina.events.LanguageFloorLiberation;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.patches.LanguageFloorLiberation;

[HarmonyPatch(typeof(AncientEventModel), "BeforeEventStarted")]
internal static class LanguageFloorLiberationSettlementAncientHealPatch
{
    private static bool Prefix(
        AncientEventModel __instance,
        ref Task __result)
    {
        if (__instance is not LanguageFloorLiberationSettlementEvent)
        {
            return true;
        }

        __result = Task.CompletedTask;
        return false;
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.ShouldAllowAncient))]
internal static class LanguageFloorLiberationSettlementAncientGatePatch
{
    private static bool Prefix(
        AncientEventModel ancient,
        ref bool __result)
    {
        if (ancient is not LanguageFloorLiberationSettlementEvent)
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
internal static class LanguageFloorLiberationSettlementRedirectPatch
{
    private static bool Prefix(
        RunManager __instance,
        ref Task __result)
    {
        if (__instance.DebugOnlyGetState()?.CurrentRoom
            is not CombatRoom
            {
                Encounter: LanguageFloorLiberationEncounter encounter
            })
        {
            return true;
        }

        LiberationSettlementProceedHelper.ClearLingeringCardPreviews();

        if (!LanguageFloorLiberationSettlementStore.PendingSettlement
            && encounter.KilledBossCount >= 2)
        {
            LanguageFloorLiberationSettlementStore.Record(encounter);
        }

        if (!LanguageFloorLiberationSettlementStore.PendingSettlement)
        {
            return true;
        }

        LanguageFloorLiberationSettlementStore.Consume();
        __result = __instance.EnterRoom(
            new EventRoom(
                ModelDb.AncientEvent<
                    LanguageFloorLiberationSettlementEvent>()));
        return false;
    }
}

[HarmonyPatch(typeof(NEventRoom), nameof(NEventRoom.Proceed))]
internal static class LanguageFloorLiberationProceedPatch
{
    private static bool Prefix(ref Task __result)
    {
        if (RunManager.Instance.DebugOnlyGetState()?.CurrentRoom
            is not EventRoom
            {
                CanonicalEvent:
                    LanguageFloorLiberationSettlementEvent
            })
        {
            return true;
        }

        __result = RunManager.Instance.EnterNextAct();
        return false;
    }
}
