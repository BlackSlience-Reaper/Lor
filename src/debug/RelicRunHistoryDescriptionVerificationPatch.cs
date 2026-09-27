using System;
using System.Linq;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using Environment = System.Environment;

namespace LibraryOfRuina.debug;

[HarmonyPatch(typeof(NMainMenu), nameof(NMainMenu._Ready))]
internal static class RelicRunHistoryDescriptionVerificationPatch
{
    private const string VerifyArg = "lor-verify-page-relic-history";
    private const string LogPrefix = "[LibraryOfRuina.PageRelicHistory.Verify] ";

    private static bool _started;

    [HarmonyPostfix]
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
            int verified = 0;
            foreach (RelicModel canonical in ModelDb.AllRelics
                         .Where(relic => relic.GetType().Assembly == typeof(LibraryOfRuinaInitializer).Assembly))
            {
                PropertyInfo? modeProperty = AccessTools.Property(canonical.GetType(), "Mode");
                if (modeProperty?.PropertyType.IsEnum != true
                    || !canonical.DynamicVars.ContainsKey("Mode"))
                {
                    continue;
                }

                MethodInfo setter = modeProperty.GetSetMethod(nonPublic: true)
                    ?? throw new MissingMethodException(canonical.GetType().FullName, "set_Mode");
                object firstMode = Enum.ToObject(modeProperty.PropertyType, 1);
                RelicModel original = canonical.ToMutable();
                setter.Invoke(original, [firstMode]);

                RelicModel restored = RelicModel.FromSerializable(original.ToSerializable());
                int restoredMode = Convert.ToInt32(modeProperty.GetValue(restored));
                int descriptionMode = restored.DynamicVars["Mode"].IntValue;
                if (restoredMode != 1 || descriptionMode != restoredMode)
                {
                    throw new InvalidOperationException(
                        canonical.Id
                        + " restored Mode="
                        + restoredMode
                        + " but description Mode="
                        + descriptionMode
                        + ".");
                }

                string genericDescription = canonical.DynamicDescription.GetFormattedText();
                string selectedDescription = restored.DynamicDescription.GetFormattedText();
                if (string.Equals(genericDescription, selectedDescription, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        canonical.Id + " still displays its generic pickup description after restore.");
                }

                verified++;
            }

            if (verified == 0)
            {
                throw new InvalidOperationException("No mode-based page relics were discovered.");
            }

            Log.Info(LogPrefix + "PAGE_RELIC_HISTORY_OK count=" + verified);
            NGame.Instance?.GetTree().Quit();
        }
        catch (Exception ex)
        {
            Log.Error(LogPrefix + "PAGE_RELIC_HISTORY_FAILED: " + ex);
            NGame.Instance?.GetTree().Quit(1);
        }
    }
}
