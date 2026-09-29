using HarmonyLib;

namespace LibraryOfRuina.content.abnormalities.QueenBee;

/// <summary>
/// After the Queen Bee uses Warlike Enhancement, exactly one alive worker bee is
/// guaranteed to roll Promote Growth (3-hit multiattack) on the following enemy turn.
/// The claim runs in slot order (left to right) during Creature.PrepareForNextTurn,
/// so a stunned worker is skipped and the token passes to the next worker.
/// </summary>
[HarmonyPatch(typeof(Creature), nameof(Creature.PrepareForNextTurn))]
internal static class QueenBeeWorkerForcedPromoteGrowthPatch
{
    private static void Postfix(
        Creature __instance,
        bool rollNewMove)
    {
        if (!rollNewMove
            || __instance.Monster is not QueenBeeWorker worker
            || __instance.IsStunned)
        {
            return;
        }

        worker.ForcePromoteGrowthNextTurn();
    }
}
