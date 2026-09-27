using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.features.secondascension;

[HarmonyPatch(typeof(NGame), nameof(NGame.StartNewSingleplayerRun))]
internal static class LibrarySecondAscensionSingleplayerRunStartPatch
{
    [HarmonyPrefix]
    private static void Prefix(ref IReadOnlyList<ModifierModel> modifiers)
    {
        modifiers = LibrarySecondAscensionState.ResolveSingleplayerRunModifiers(modifiers);
    }
}

[HarmonyPatch(typeof(NGame), nameof(NGame.StartNewMultiplayerRun))]
internal static class LibrarySecondAscensionMultiplayerRunStartPatch
{
    [HarmonyPrefix]
    private static void Prefix(StartRunLobby lobby, ref IReadOnlyList<ModifierModel> modifiers)
    {
        modifiers = LibrarySecondAscensionState.ResolveMultiplayerRunModifiers(lobby, modifiers);
    }
}

[HarmonyPatch(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen.BeginRun))]
internal static class LibrarySecondAscensionCharacterSelectBeginRunPatch
{
    [HarmonyPrefix]
    private static void Prefix(NCharacterSelectScreen __instance, ref IReadOnlyList<ModifierModel> modifiers)
    {
        LibrarySecondAscensionState.CaptureStandardBeginRunModifiers(__instance.Lobby, ref modifiers);
    }
}

[HarmonyPatch(typeof(RunState), nameof(RunState.CreateForNewRun))]
internal static class LibrarySecondAscensionRunStateCreatePatch
{
    [HarmonyPostfix]
    private static void Postfix(RunState __result)
    {
        LibrarySecondAscensionState.EnsureStandardSingleplayerRunModifier(__result);
    }
}
