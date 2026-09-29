using System;
using System.Reflection;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.core.compat;

internal static class RunManagerCompat
{
    private static readonly PropertyInfo? IsSingleplayerOrFakeMultiplayerProperty =
        typeof(RunManager).GetProperty("IsSingleplayerOrFakeMultiplayer")
        ?? typeof(RunManager).GetProperty("IsSinglePlayerOrFakeMultiplayer");

    private static readonly PropertyInfo? RunStateGameModeProperty =
        typeof(IRunState).GetProperty("GameMode")
        ?? typeof(RunState).GetProperty("GameMode");

    private static readonly MethodInfo? FadeInMethod =
        typeof(RunManager).GetMethod(
            "FadeIn",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            [typeof(bool)],
            modifiers: null);

    public static bool IsSinglePlayerOrFakeMultiplayer(RunManager runManager)
    {
        if (IsSingleplayerOrFakeMultiplayerProperty?.GetValue(runManager) is bool value)
        {
            return value;
        }

        throw new MissingMemberException(
            typeof(RunManager).FullName,
            "IsSingleplayerOrFakeMultiplayer/IsSinglePlayerOrFakeMultiplayer");
    }

    public static bool IsStandardGameMode(IRunState runState)
    {
        object? gameMode = RunStateGameModeProperty?.GetValue(runState);
        return gameMode == null || string.Equals(gameMode.ToString(), "Standard", StringComparison.Ordinal);
    }

    public static async Task FadeIn(RunManager runManager, bool showTransition)
    {
        if (FadeInMethod?.Invoke(runManager, [showTransition]) is Task task)
        {
            await task;
            return;
        }

        throw new MissingMethodException(typeof(RunManager).FullName, "FadeIn(bool)");
    }
}
