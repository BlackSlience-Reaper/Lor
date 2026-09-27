using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.relics;

[HarmonyPatch(typeof(CombatRoom), nameof(CombatRoom.OfferRoomEndRewards))]
internal static class AbnormalityPageRewardResumePatch
{
    private const string LogPrefix = "[LibraryOfRuina.PageRelicResume] ";
    private static readonly FieldInfo? RewardsSetField =
        AccessTools.Field(typeof(NRewardsScreen), "_rewardsSet");
    private static readonly ConditionalWeakTable<CombatRoom, RestoreMarker> RestoredRooms = new();

    internal static void MarkRestored(CombatRoom room)
    {
        RestoredRooms.GetValue(room, static _ => new RestoreMarker());
    }

    [HarmonyPostfix]
    private static void Postfix(CombatRoom __instance, ref Task __result)
    {
        if (!RestoredRooms.TryGetValue(__instance, out _)
            || !RunManager.Instance.IsSingleplayerOrFakeMultiplayer
            || !HasPendingPageReward(__instance))
        {
            return;
        }

        RestoredRooms.Remove(__instance);
        __result = ReleaseRestoreAfterRewardsScreenAppears(__instance, __result);
    }

    private static bool HasPendingPageReward(CombatRoom room)
    {
        return room.IsPreFinished
            && room.ExtraRewards.Values
                .SelectMany(static rewards => rewards)
                .OfType<RelicReward>()
                .Any(static reward =>
                    !reward.SuccessfullySelected
                    && AbnormalityPageRewardPreselection.IsPageRelic(reward.Relic));
    }

    private static async Task ReleaseRestoreAfterRewardsScreenAppears(
        CombatRoom room,
        Task originalTask)
    {
        while (!originalTask.IsCompleted)
        {
            if (IsRoomRewardsScreenReady(room))
            {
                _ = TaskHelper.RunSafely(originalTask);
                Log.Info(
                    LogPrefix
                    + "Restored page-relic rewards screen is ready; allowing the load transition to fade in.");
                return;
            }

            SceneTree tree = NGame.Instance?.GetTree()
                ?? (SceneTree)Engine.GetMainLoop();
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }

        await originalTask;
    }

    private static bool IsRoomRewardsScreenReady(CombatRoom room)
    {
        if (NOverlayStack.Instance?.Peek() is not NRewardsScreen rewardsScreen
            || RewardsSetField?.GetValue(rewardsScreen) is not RewardsSet rewardsSet
            || !ReferenceEquals(rewardsSet.Room, room))
        {
            return false;
        }

        return rewardsSet.Rewards
            .OfType<RelicReward>()
            .Any(static reward =>
                !reward.SuccessfullySelected
                && AbnormalityPageRewardPreselection.IsPageRelic(reward.Relic));
    }

    private sealed class RestoreMarker
    {
    }
}

[HarmonyPatch(typeof(CombatRoom), nameof(CombatRoom.FromSerializable))]
internal static class AbnormalityPageRewardRestoreMarkerPatch
{
    [HarmonyPostfix]
    private static void Postfix(CombatRoom __result)
    {
        if (__result.IsPreFinished)
        {
            AbnormalityPageRewardResumePatch.MarkRestored(__result);
        }
    }
}
