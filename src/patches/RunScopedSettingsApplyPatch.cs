using HarmonyLib;
using LibraryOfRuina.features.settings;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.patches;

/// <summary>
/// Gameplay settings changed during a run are held until the run is cleaned up, so every client
/// keeps playing the run with the values it started with.
/// </summary>
[HarmonyPatch(typeof(RunManager), nameof(RunManager.CleanUp), typeof(bool))]
internal static class RunScopedSettingsApplyPatch
{
    private static void Postfix()
    {
        LibraryOfRuinaSettings.ApplyRunScopedSettings();
    }
}
