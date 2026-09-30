using System.Threading.Tasks;
using HarmonyLib;
using LibraryOfRuina.infra.lifecycle;
using LibraryOfRuina.patches;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.content.liberation.Art;

/// <summary>由 <see cref="LibraryOfRuina.patches.dispatch.LiberationSettlementPatches"/> 在终局奖励界面继续时调用；返回 false 表示已接管。</summary>
internal static class ArtFloorLiberationSettlementRedirect
{
    internal static bool TryRedirect(RunManager __instance, ref Task __result)
    {
        if (CurrentRun.Of(__instance)?.CurrentRoom is not CombatRoom { Encounter: ArtFloorLiberationEncounter encounter })
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

