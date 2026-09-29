using System.Threading.Tasks;
using HarmonyLib;
using LibraryOfRuina.patches;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.content.liberation.Language;

/// <summary>由 <see cref="LibraryOfRuina.patches.dispatch.LiberationSettlementPatches"/> 在终局奖励界面继续时调用；返回 false 表示已接管。</summary>
internal static class LanguageFloorLiberationSettlementRedirect
{
    internal static bool TryRedirect(
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

