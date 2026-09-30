using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using LibraryOfRuina.infra.lifecycle;
using LibraryOfRuina.patches;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.content.liberation.Technology;

/// <summary>由 <see cref="LibraryOfRuina.patches.dispatch.LiberationSettlementPatches"/> 在终局奖励界面继续时调用；返回 false 表示已接管。</summary>
internal static class TechnologyFloorLiberationSettlementRedirect
{
    internal static bool TryRedirect(RunManager __instance, ref Task __result)
    {
        if (CurrentRun.Of(__instance)?.CurrentRoom is not CombatRoom { Encounter: TechnologyFloorLiberationEncounter encounter })
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
        var state = CurrentRun.Of(runManager);
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

