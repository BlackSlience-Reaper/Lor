using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using LibraryOfRuina.infra.patching;

namespace LibraryOfRuina.content.specialguests;

/// <summary>
/// Keeps restored special-guest reward rooms loadable when another mod replaces
/// the native reward flow with one that awaits player selection.  A saved run
/// cannot fade in until CombatRoom.EnterInternal returns, so awaiting the
/// terminal rewards screen there deadlocks behind the still-opaque transition.
/// </summary>
[HarmonyPatch(typeof(CombatRoom), nameof(CombatRoom.OfferRoomEndRewards))]
[HarmonyPriority(Priority.First)]
[LibraryPatch(Reason = "0.107.1 没有奖励前置 Hook，嘉宾战按原版先生成全部奖励、再展示的顺序追加奖励；0.111.0 保留既有事件嘉宾房间恢复分支及 Priority.First，对手模组尚未确认，仍属于设计哲学 §3 的例外。")]
internal static partial class SpecialGuestRewardResumePatch
{
    [HarmonyPrefix]
    private static bool Prefix(CombatRoom __instance, ref Task __result)
    {
        if (!ShouldReplaceOffer(__instance))
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
            await BeforeRewards(reward, room);
            _ = TaskHelper.RunSafely(reward.Offer());
        }

        Log.Info(
            "[SpecialGuest] Restored terminal rewards without blocking the load transition: "
            + room.Encounter.Id.Entry);
    }
}
