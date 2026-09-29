using HarmonyLib;
using MegaCrit.Sts2.Core.Rewards;

namespace LibraryOfRuina.content.abnormalities.LittleRedMercenary;

[HarmonyPatch(typeof(RewardsSet), nameof(RewardsSet.WithRewardsFromRoom))]
internal static class LittleRedMercenaryRewardPatch
{
    private static void Postfix(RewardsSet __instance)
    {
        LittleRedMercenaryEncounterHelper.TryAddLittleRedPageReward(
            __instance.Room,
            __instance.Player,
            __instance.Rewards);
    }
}
