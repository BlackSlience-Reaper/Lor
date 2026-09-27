using System;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.features.ftue;

/// <summary>
/// Tracks whether the current/last combat was against a Library of Ruina encounter.
/// This flag is used by downstream patches (rewards FTUE) that can't directly
/// inspect the encounter model from their context.
/// </summary>
public static class LibraryOfRuinaFtueTracker
{
    /// <summary>
    /// True if the most recent combat was against an LoR encounter.
    /// Reset at the start of each new combat.
    /// </summary>
    public static bool LastEncounterWasLoR { get; private set; }

    internal static void SetLastEncounterWasLoR(bool value)
    {
        LastEncounterWasLoR = value;
    }
}

/// <summary>
/// Patches CombatManager.SetUpCombat to record whether the current encounter
/// is a Library of Ruina encounter and queue its current combat tutorial.
/// </summary>
[HarmonyPatch]
public static class LibraryOfRuinaEncounterTrackerPatch
{
    private static MethodBase? _targetMethod;

    [HarmonyPrepare]
    public static bool Prepare()
    {
        _targetMethod = AccessTools.Method(
            typeof(CombatManager),
            "SetUpCombat",
            [typeof(CombatState)]);
        if (_targetMethod != null)
        {
            return true;
        }

        Log.Warn(
            "LibraryOfRuina FTUE: Could not find "
            + "CombatManager.SetUpCombat(CombatState); encounter tracking disabled.");
        return false;
    }

    [HarmonyTargetMethod]
    public static MethodBase TargetMethod()
    {
        return _targetMethod
            ?? throw new MissingMethodException(
                nameof(CombatManager),
                "SetUpCombat(CombatState)");
    }

    [HarmonyPostfix]
    public static void Postfix(CombatManager __instance, CombatState __0)
    {
        EncounterModel? encounter = __0.Encounter;
        var isLoR = FtueGuard.IsLibraryOfRuinaEncounter(encounter);
        LibraryOfRuinaFtueTracker.SetLastEncounterWasLoR(isLoR);

        if (isLoR && encounter != null)
        {
            Log.Info("LibraryOfRuina: Detected LoR encounter for FTUE tracking.");
            LibraryOfRuinaCombatFtuePatch.QueueForCombat(__instance, __0, encounter);
        }
    }
}
