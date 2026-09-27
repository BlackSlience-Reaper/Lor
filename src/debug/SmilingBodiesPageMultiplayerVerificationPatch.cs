using System;
using System.Linq;
using Godot;
using HarmonyLib;
using LibraryOfRuina.patches.SmilingBodies;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using Environment = System.Environment;

namespace LibraryOfRuina.debug;

[HarmonyPatch(typeof(NMainMenu), nameof(NMainMenu._Ready))]
internal static class SmilingBodiesPageMultiplayerVerificationPatch
{
    private const string VerifyArg = "lor-verify-smiling-bodies-page-multiplayer";
    private const string LogPrefix =
        "[LibraryOfRuina.SmilingBodiesPageMultiplayer.Verify] ";

    private static bool _started;

    private static void Postfix()
    {
        if (_started || !HasVerifyArg())
        {
            return;
        }

        _started = true;
        Callable.From(Run).CallDeferred();
    }

    private static bool HasVerifyArg() =>
        CommandLineHelper.HasArg(VerifyArg)
        || Environment.GetCommandLineArgs().Any(arg => string.Equals(
            arg.TrimStart('-'),
            VerifyArg,
            StringComparison.OrdinalIgnoreCase));

    private static void Run()
    {
        try
        {
            Require(
                SmilingBodiesAbsorptionHealPatch.CalculateTrackableHeal(
                    hpBefore: 177,
                    wasDeadBefore: false,
                    hpAfter: 178) == 1,
                "A normal one-HP heal was not tracked exactly once.");
            Require(
                SmilingBodiesAbsorptionHealPatch.CalculateTrackableHeal(
                    hpBefore: 0,
                    wasDeadBefore: true,
                    hpAfter: 178) == 0,
                "A dead-to-full reconstruction was counted as healing.");
            Require(
                SmilingBodiesAbsorptionHealPatch.CalculateTrackableHeal(
                    hpBefore: 0,
                    wasDeadBefore: false,
                    hpAfter: 178) == 0,
                "A transient zero-to-full reconstruction was counted as healing.");
            Require(
                SmilingBodiesAbsorptionHealPatch.CalculateTrackableHeal(
                    hpBefore: 178,
                    wasDeadBefore: false,
                    hpAfter: 120) == 0,
                "HP loss was counted as healing.");

            Log.Info(LogPrefix + "SMILING_BODIES_PAGE_MULTIPLAYER_OK");
            NGame.Instance?.GetTree().Quit();
        }
        catch (Exception exception)
        {
            Log.Error(
                LogPrefix
                + "SMILING_BODIES_PAGE_MULTIPLAYER_FAILED: "
                + exception);
            NGame.Instance?.GetTree().Quit(1);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
