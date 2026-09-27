using HarmonyLib;
using LibraryOfRuina.encounters;

namespace LibraryOfRuina.monsters.ForsakenMurderer;

[HarmonyPatch(typeof(ForsakenMurderer), nameof(ForsakenMurderer.AfterAddedToRoom))]
internal static class ForsakenMurdererAfterAddedToRoomBgmPatch
{
    [HarmonyPostfix]
    private static void Postfix(ForsakenMurderer __instance)
    {
        EncounterBgmController.RegisterMonster(__instance.Creature);
    }
}

[HarmonyPatch(typeof(ForsakenMurderer), nameof(ForsakenMurderer.BeforeRemovedFromRoom))]
internal static class ForsakenMurdererBeforeRemovedFromRoomBgmPatch
{
    [HarmonyPrefix]
    private static void Prefix(ForsakenMurderer __instance)
    {
        EncounterBgmController.UnregisterMonster(__instance.Creature);
    }
}
