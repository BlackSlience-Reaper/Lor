using System;
using HarmonyLib;
using LibraryOfRuina.features.intentgraph;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Logging;

namespace LibraryOfRuina.patches;

[HarmonyPatch(
    typeof(CombatManager),
    nameof(CombatManager.AfterCreatureAdded), typeof(Creature))]
internal static class MonsterIntentGraphPrecomputePatch
{
    [HarmonyPostfix]
    private static void Postfix(Creature creature)
    {
        if (!creature.IsMonster)
        {
            return;
        }

        try
        {
            MonsterIntentGraphRuntimeCache.Warmup(creature);
        }
        catch (Exception exception)
        {
            Log.Warn("[LibraryOfRuina.IntentGraph] Precompute patch failed: " + exception.Message);
        }
    }
}
