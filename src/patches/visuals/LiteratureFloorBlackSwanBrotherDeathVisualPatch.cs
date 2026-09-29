using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Combat;
using LiteratureFloorBlackSwanBrotherBase = LibraryOfRuina.content.liberation.Literature.LiteratureFloorBlackSwanBrotherBase;
using LiteratureFloorBlackSwanBrotherCreatureVisuals = LibraryOfRuina.content.liberation.Literature.LiteratureFloorBlackSwanBrotherCreatureVisuals;

namespace LibraryOfRuina.patches.visuals;

[HarmonyPatch(typeof(NCreature), nameof(NCreature.StartDeathAnim))]
internal static class LiteratureFloorBlackSwanBrotherDeathVisualPatch
{
    private static void Prefix(NCreature __instance)
    {
        if (__instance.Entity.Monster
                is LiteratureFloorBlackSwanBrotherBase brother
            && __instance.Visuals
                is LiteratureFloorBlackSwanBrotherCreatureVisuals visuals)
        {
            visuals.ShowInactiveOrDeadIdle(brother);
        }
    }
}
