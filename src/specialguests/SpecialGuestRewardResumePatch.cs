using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.specialguests;

/// <summary>
/// Keeps restored special-guest reward rooms loadable when another mod replaces
/// the native reward flow with one that awaits player selection.  A saved run
/// cannot fade in until CombatRoom.EnterInternal returns, so awaiting the
/// terminal rewards screen there deadlocks behind the still-opaque transition.
/// </summary>
[HarmonyPatch(typeof(CombatRoom), nameof(CombatRoom.OfferRoomEndRewards))]
[HarmonyPriority(Priority.First)]
internal static class SpecialGuestRewardResumePatch
{
    [HarmonyPrefix]
    private static bool Prefix(CombatRoom __instance, ref Task __result)
    {
        if (__instance is not
            {
                IsPreFinished: true,
                ParentEventId: not null,
                Encounter: ISpecialGuestEncounterStage,
            })
        {
            return true;
        }

        __result = OfferWithoutAwaitingPlayerSelection(__instance);
        return false;
    }

    private static async Task OfferWithoutAwaitingPlayerSelection(CombatRoom room)
    {
        var rewards = new List<RewardsSet>();
        foreach (Player player in room.CombatState.Players)
        {
            rewards.Add(await RewardsCmd.GenerateForRoomEnd(player, room));
        }

        foreach (RewardsSet reward in rewards)
        {
            await Hook.BeforeCombatRewardOffered(reward, room.CombatState.RunState, room);
            _ = TaskHelper.RunSafely(reward.Offer());
        }

        Log.Info(
            "[SpecialGuest] Restored terminal rewards without blocking the load transition: "
            + room.Encounter.Id.Entry);
    }
}
