using HarmonyLib;
using LibraryOfRuina.combat;
using LibraryOfRuina.encounters;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.patches.LittleRedMercenary;

[HarmonyPatch(typeof(Hook), nameof(Hook.ShouldClearBlock))]
internal static class LittleRedMercenaryBlockClearPatch
{
    [HarmonyPrefix]
    private static bool Prefix(CombatStateLike combatState, ref bool __result, ref AbstractModel? preventer)
    {
        if (combatState == null)
        {
            __result = true;
            preventer = null;
            return false;
        }

        return true;
    }

    [HarmonyPostfix]
    private static void Postfix(CombatStateLike combatState, Creature creature, ref bool __result, ref AbstractModel? preventer)
    {
        if (!__result || !AllyTurnRegistry.ShouldPreventVanillaBlockClearing(creature))
        {
            if (__result && BlockTransferEncounterTargetHelper.IsBlockTransferPartner(combatState, creature))
            {
                __result = false;
                preventer = creature.Monster;
            }

            return;
        }

        __result = false;
        preventer = creature.Monster;
    }
}
