using Godot;
using HarmonyLib;
using LibraryOfRuina.encounters.LiteratureFloorLiberation;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.patches.LiteratureFloorLiberation;

[HarmonyPatch(typeof(EncounterModel), nameof(EncounterModel.CreateScene))]
internal static class LiteratureFloorLiberationCreateScenePatch
{
    private static bool Prefix(
        EncounterModel __instance,
        ref Control __result)
    {
        if (__instance is not LiteratureFloorLiberationEncounter encounter
            || encounter.PhaseComplete
            || encounter.CurrentPhase is not (2 or 3 or 4 or 5))
        {
            return true;
        }

        __result = encounter.CurrentPhase switch
        {
            5 => LiteratureFloorLiberationEncounter
                .InstantiatePhaseFiveEncounterScene(),
            4 => LiteratureFloorLiberationEncounter
                .InstantiatePhaseFourEncounterScene(),
            3 => LiteratureFloorLiberationEncounter
                .InstantiatePhaseThreeEncounterScene(),
            _ => LiteratureFloorLiberationEncounter
                .InstantiatePhaseTwoEncounterScene()
        };
        return false;
    }
}
