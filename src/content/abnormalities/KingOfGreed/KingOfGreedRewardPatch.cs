using HarmonyLib;
using MegaCrit.Sts2.Core.Rewards;

namespace LibraryOfRuina.content.abnormalities.KingOfGreed;

[HarmonyPatch(typeof(RewardsSet), nameof(RewardsSet.WithRewardsFromRoom))]
internal static class KingOfGreedRewardPatch
{
    private static void Postfix(RewardsSet __instance)
    {
        KingOfGreed.TryAddKingOfGreedPageReward(
            __instance.Room,
            __instance.Player,
            __instance.Rewards);
    }
}
