using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using LibraryOfRuina.combat;
using LibraryOfRuina.compat;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace LibraryOfRuina.patches;

[HarmonyPatch(typeof(TaskHelper), nameof(TaskHelper.RunSafely))]
internal static class CombatSafetyTaskHelperPatch
{
    private static bool Prefix(Task task, ref Task __result)
    {
        __result = CombatSafetyNet.RunTaskHelperSafely(task);
        return false;
    }
}

[HarmonyPatch]
internal static class CombatSafetyHookTaskPatch
{
    private static IEnumerable<MethodBase> TargetMethods() =>
        AccessTools.GetDeclaredMethods(typeof(Hook))
            .Where(static method => method.ReturnType == typeof(Task));

    private static bool IsCombatRelevantParameter(ParameterInfo parameter)
    {
        Type type = parameter.ParameterType;
        return typeof(CombatStateLike).IsAssignableFrom(type)
            || type.FullName == "MegaCrit.Sts2.Core.Combat.CombatState"
            || typeof(Creature).IsAssignableFrom(type)
            || typeof(Player).IsAssignableFrom(type)
            || typeof(MonsterModel).IsAssignableFrom(type)
            || typeof(CardModel).IsAssignableFrom(type)
            || typeof(PowerModel).IsAssignableFrom(type);
    }

    private static void Postfix(MethodBase __originalMethod, object?[] __args, ref Task __result)
    {
        __result = CombatSafetyNet.WrapHookTask(
            __result,
            "Hook." + __originalMethod.Name,
            __args);
    }
}

[HarmonyPatch(typeof(GameAction), nameof(GameAction.Execute))]
internal static class CombatSafetyGameActionExecutePatch
{
    private static void Postfix(GameAction __instance, ref Task __result)
    {
        __result = CombatSafetyNet.WrapTask(
            __result,
            new CombatSafetyContext("GameAction.Execute", Action: __instance));
    }
}

[HarmonyPatch(typeof(ActionExecutor), "ExecuteActions")]
internal static class CombatSafetyActionExecutorPatch
{
    private static void Postfix(ActionExecutor __instance, ref Task __result)
    {
        __result = CombatSafetyNet.WrapTask(
            __result,
            new CombatSafetyContext(
                "ActionExecutor.ExecuteActions",
                Action: __instance.CurrentlyRunningAction));
    }
}

[HarmonyPatch(typeof(CombatManager), "StartCombatInternal")]
internal static class CombatSafetyStartCombatPatch
{
    private static void Postfix(ref Task __result)
    {
        __result = CombatSafetyNet.WrapTask(
            __result,
            new CombatSafetyContext("CombatManager.StartCombatInternal"));
    }
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.EnterRoomWithoutExitingCurrentRoom))]
internal static class CombatSafetyNestedCombatReplayPatch
{
    private static void Prefix(AbstractRoom room)
    {
        CombatSafetyNet.EnsureReplayInitializedBeforeNestedCombat(room);
    }
}

[HarmonyPatch(
    typeof(CombatManager),
    nameof(CombatManager.AfterCreatureAdded), typeof(Creature))]
internal static class CombatSafetyAfterCreatureAddedPatch
{
    private static void Postfix(Creature creature, ref Task __result)
    {
        __result = CombatSafetyNet.WrapTask(
            __result,
            new CombatSafetyContext(
                "CombatManager.AfterCreatureAdded",
                creature.CombatState,
                creature,
                creature.Monster));
    }
}

[HarmonyPatch(
    typeof(CombatManager),
    nameof(CombatManager.CheckWinCondition),
    new Type[] { })]
internal static class CombatSafetyCheckWinConditionPatch
{
    private static void Postfix(ref Task<bool> __result)
    {
        __result = CombatSafetyNet.WrapCheckWinConditionTask(
            __result,
            new CombatSafetyContext("CombatManager.CheckWinCondition"));
    }
}

[HarmonyPatch]
internal static class CombatSafetyEndPlayerTurnPhaseOnePatch
{
    private static MethodBase TargetMethod() =>
        CombatManagerTurnMethodCompat.Resolve("EndPlayerTurnPhaseOneInternal");

    private static void Postfix(ref Task __result)
    {
        __result = CombatSafetyNet.WrapTask(
            __result,
            new CombatSafetyContext("CombatManager.EndPlayerTurnPhaseOneInternal"));
    }
}

[HarmonyPatch]
internal static class CombatSafetyEndPlayerTurnPhaseTwoPatch
{
    private static MethodBase TargetMethod() =>
        CombatManagerTurnMethodCompat.Resolve("EndPlayerTurnPhaseTwoInternal");

    private static void Postfix(ref Task __result)
    {
        __result = CombatSafetyNet.WrapTask(
            __result,
            new CombatSafetyContext("CombatManager.EndPlayerTurnPhaseTwoInternal"));
    }
}

[HarmonyPatch]
internal static class CombatSafetySwitchFromPlayerToEnemySidePatch
{
    private static MethodBase TargetMethod() =>
        CombatManagerTurnMethodCompat.Resolve("SwitchFromPlayerToEnemySide");

    private static void Postfix(ref Task __result)
    {
        __result = CombatSafetyNet.WrapTask(
            __result,
            new CombatSafetyContext("CombatManager.SwitchFromPlayerToEnemySide"));
    }
}

[HarmonyPatch(typeof(Creature), nameof(Creature.TakeTurn))]
internal static class CombatSafetyCreatureTakeTurnPatch
{
    private static void Postfix(Creature __instance, ref Task __result)
    {
        __result = CombatSafetyNet.WrapTask(
            __result,
            new CombatSafetyContext(
                "Creature.TakeTurn",
                __instance.CombatState,
                __instance,
                __instance.Monster));
    }
}

[HarmonyPatch(typeof(MonsterModel), nameof(MonsterModel.PerformMove))]
internal static class CombatSafetyMonsterPerformMovePatch
{
    private static void Postfix(MonsterModel __instance, ref Task __result)
    {
        __result = CombatSafetyNet.WrapTask(
            __result,
            new CombatSafetyContext(
                "MonsterModel.PerformMove",
                __instance.Creature.CombatState,
                __instance.Creature,
                __instance));
    }
}

[HarmonyPatch(typeof(NCreature), nameof(NCreature.PerformIntent))]
internal static class CombatSafetyCreatureIntentPatch
{
    private static void Postfix(NCreature __instance, ref Task __result)
    {
        Creature creature = __instance.Entity;
        __result = CombatSafetyNet.WrapTask(
            __result,
            new CombatSafetyContext(
                "NCreature.PerformIntent",
                creature.CombatState,
                creature,
                creature.Monster));
    }
}

[HarmonyPatch(typeof(AttackCommand), nameof(AttackCommand.Execute))]
internal static class CombatSafetyAttackCommandExecutePatch
{
    private static void Postfix(AttackCommand __instance, ref Task<AttackCommand> __result)
    {
        __result = CombatSafetyNet.WrapMonsterAttackTask(__result, __instance);
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterDamageGiven))]
internal static class CombatSafetyAfterDamageGivenPatch
{
    private static bool Prefix(Creature? dealer, ref Task __result)
    {
        if (!CombatSafetyNet.ShouldSkipDeadMonsterAttackFollowup(dealer))
        {
            return true;
        }

        __result = Task.CompletedTask;
        return false;
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterAttack))]
internal static class CombatSafetyAfterAttackPatch
{
    private static bool Prefix(AttackCommand command, ref Task __result)
    {
        if (!CombatSafetyNet.ShouldSkipDeadMonsterAttackFollowup(command.Attacker))
        {
            return true;
        }

        __result = Task.CompletedTask;
        return false;
    }
}

/// <summary>
/// Prevents combat-end (or any replay/network) serialization from aborting when a card carries a
/// SavedProperty whose name belongs to a currently-unloaded mod (e.g. a run saved while GuZhenRen
/// was enabled, then GuZhenRen disabled). Registers the missing name before WritePropertyName runs,
/// so SavedProperties.Serialize no longer throws ArgumentException and EndCombatInternal completes.
/// </summary>
[HarmonyPatch(typeof(SavedProperties), "WritePropertyName")]
internal static class CombatSafetySavedPropertyWritePatch
{
    private static void Prefix(string propertyName)
    {
        CombatSafetyNet.RegisterSavedPropertyNameIfMissing(propertyName);
    }
}

/// <summary>
/// Runs right before every game-state checksum is generated (the only snapshot entry point:
/// RunManager.SendPostActionChecksum and the CombatManager turn-boundary call sites all flow
/// through ChecksumTracker.GenerateChecksum(string, GameAction)). If this machine's death
/// pipeline stalled mid-hook, a killed enemy can still sit at 0 HP inside the combat state
/// while peers have already removed it; the snapshot would then diverge and clients get
/// disconnected. Finalizing the stuck death before the hash makes every machine snapshot the
/// same post-death state. See CombatSafetyNet.FinalizeStuckDeadEnemiesBeforeChecksum.
/// </summary>
[HarmonyPatch(
    typeof(ChecksumTracker),
    nameof(ChecksumTracker.GenerateChecksum), typeof(string), typeof(GameAction))]
internal static class CombatSafetyChecksumFinalizationPatch
{
    private static void Prefix()
    {
        CombatSafetyNet.FinalizeStuckDeadEnemiesBeforeChecksum();
    }
}
