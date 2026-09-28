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

/// <summary>由 <see cref="LibraryOfRuina.patches.dispatch.LiberationSettlementPatches"/> 在终局奖励界面继续时调用；返回 false 表示已接管。</summary>
internal static class LiteratureFloorLiberationSettlementRedirect
{
    internal static bool TryRedirect(
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

