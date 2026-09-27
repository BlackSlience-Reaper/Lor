using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using LibraryOfRuina.encounters.TechnologyFloorLiberation;
using LibraryOfRuina.events.TechnologyFloorLiberation;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.patches.TechnologyFloorLiberation;

[HarmonyPatch(typeof(AncientEventModel), "BeforeEventStarted")]
internal static class TechnologyFloorLiberationSettlementAncientHealPatch
{
    private static bool Prefix(AncientEventModel __instance, ref Task __result)
    {
        if (__instance is not TechnologyFloorLiberationSettlementEvent)
        {
            return true;
        }

        __result = Task.CompletedTask;
        return false;
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.ShouldAllowAncient))]
internal static class TechnologyFloorLiberationSettlementAncientGatePatch
{
    private static bool Prefix(AncientEventModel ancient, ref bool __result)
    {
        if (ancient is not TechnologyFloorLiberationSettlementEvent)
        {
            return true;
        }

        __result = true;
        return false;
    }
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.ProceedFromTerminalRewardsScreen))]
internal static class TechnologyFloorLiberationSettlementRedirectPatch
{
    private static bool Prefix(RunManager __instance, ref Task __result)
    {
        if (__instance.DebugOnlyGetState()?.CurrentRoom is not CombatRoom { Encounter: TechnologyFloorLiberationEncounter encounter })
        {
            return true;
        }

        LiberationSettlementProceedHelper.ClearLingeringCardPreviews();

        if (TechnologyFloorLiberationSettlementStore
            .RequiresLegacySingleKillDefeatRecovery(encounter))
        {
            __result = RecoverLegacySingleKillDefeat(__instance);
            return false;
        }

        if (!TechnologyFloorLiberationSettlementStore.PendingSettlement
            && encounter.KilledBossCount
            >= TechnologyFloorLiberationSettlementStore.MinimumKilledBossCount)
        {
            TechnologyFloorLiberationSettlementStore.Record(encounter);
        }

        if (!TechnologyFloorLiberationSettlementStore.PendingSettlement)
        {
            return true;
        }

        TechnologyFloorLiberationSettlementStore.Consume();
        __result = __instance.EnterRoom(new EventRoom(ModelDb.AncientEvent<TechnologyFloorLiberationSettlementEvent>()));
        return false;
    }

    private static async Task RecoverLegacySingleKillDefeat(RunManager runManager)
    {
        var state = runManager.DebugOnlyGetState();
        if (state == null)
        {
            return;
        }

        Log.Warn(
            "[TechnologyFloorLiberation] Recovering a legacy single-kill lethal victory as a normal defeat.");

        var survivingPlayers = state.Players
            .Select(static player => player.Creature)
            .Where(static creature => creature.IsAlive)
            .ToArray();
        if (survivingPlayers.Length > 0)
        {
            await CreatureCmd.Kill(survivingPlayers, force: true);
        }

        if (!state.IsGameOver)
        {
            NRun.Instance?.ShowGameOverScreen(runManager.OnEnded(isVictory: false));
        }
    }
}

[HarmonyPatch(typeof(NEventRoom), nameof(NEventRoom.Proceed))]
internal static class TechnologyFloorLiberationProceedPatch
{
    private static bool Prefix(ref Task __result)
    {
        if (RunManager.Instance.DebugOnlyGetState()?.CurrentRoom is not EventRoom { CanonicalEvent: TechnologyFloorLiberationSettlementEvent })
        {
            return true;
        }

        __result = RunManager.Instance.EnterNextAct();
        return false;
    }
}
