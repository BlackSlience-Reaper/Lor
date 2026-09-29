using Godot;
using HarmonyLib;
using LibraryOfRuina.infra.patching;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.content.liberation.Literature;

[HarmonyPatch(typeof(EncounterModel), nameof(EncounterModel.CreateScene))]
[LibraryPatch(Reason = "原版 CreateScene 非虚且场景路径按遭遇 id 固定，无法按阶段换场景；只作用于文学层解放遭遇的第 2–5 阶段。")]
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
