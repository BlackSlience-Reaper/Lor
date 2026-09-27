using System.Threading.Tasks;
using HarmonyLib;
using LibraryOfRuina.encounters.ArtFloorLiberation;
using LibraryOfRuina.events.ArtFloorLiberation;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.patches.ArtFloorLiberation;

[HarmonyPatch(typeof(AncientEventModel), "BeforeEventStarted")]
internal static class ArtFloorLiberationSettlementAncientHealPatch
{
    private static bool Prefix(AncientEventModel __instance, ref Task __result)
    {
        if (__instance is not ArtFloorLiberationSettlementEvent)
        {
            return true;
        }

        __result = Task.CompletedTask;
        return false;
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.ShouldAllowAncient))]
internal static class ArtFloorLiberationSettlementAncientGatePatch
{
    private static bool Prefix(AncientEventModel ancient, ref bool __result)
    {
        if (ancient is not ArtFloorLiberationSettlementEvent)
        {
            return true;
        }

        __result = true;
        return false;
    }
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.ProceedFromTerminalRewardsScreen))]
internal static class ArtFloorLiberationSettlementRedirectPatch
{
    private static bool Prefix(RunManager __instance, ref Task __result)
    {
        if (__instance.DebugOnlyGetState()?.CurrentRoom is not CombatRoom { Encounter: ArtFloorLiberationEncounter encounter })
        {
            return true;
        }

        LiberationSettlementProceedHelper.ClearLingeringCardPreviews();

        if (!ArtFloorLiberationSettlementStore.PendingSettlement && encounter.KilledBossCount >= 2)
        {
            ArtFloorLiberationSettlementStore.Record(encounter);
        }

        if (!ArtFloorLiberationSettlementStore.PendingSettlement)
        {
            return true;
        }

        ArtFloorLiberationSettlementStore.Consume();
        __result = __instance.EnterRoom(new EventRoom(ModelDb.AncientEvent<ArtFloorLiberationSettlementEvent>()));
        return false;
    }
}

[HarmonyPatch(typeof(NEventRoom), nameof(NEventRoom.Proceed))]
internal static class ArtFloorLiberationProceedPatch
{
    private static bool Prefix(ref Task __result)
    {
        if (RunManager.Instance.DebugOnlyGetState()?.CurrentRoom is not EventRoom { CanonicalEvent: ArtFloorLiberationSettlementEvent })
        {
            return true;
        }

        __result = RunManager.Instance.EnterNextAct();
        return false;
    }
}
