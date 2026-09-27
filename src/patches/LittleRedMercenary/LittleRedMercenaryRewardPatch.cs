using HarmonyLib;
using LibraryOfRuina.encounters.LittleRedMercenary;
using MegaCrit.Sts2.Core.Rewards;

namespace LibraryOfRuina.patches.LittleRedMercenary;

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
