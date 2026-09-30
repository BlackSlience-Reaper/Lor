using HarmonyLib;

namespace LibraryOfRuina.content.liberation.Language;

[HarmonyPatch(typeof(Creature), nameof(Creature.PrepareForNextTurn))]
internal static class LanguageFloorCobaltScarPostRollPatch
{
    [HarmonyPostfix]
    private static void Postfix(Creature __instance)
    {
        if (__instance.Monster is LanguageFloorCobaltScar cobalt)
        {
            cobalt.RefreshAfterMoveRoll();
        }
    }
}
