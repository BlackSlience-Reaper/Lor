using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Debug;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using Environment = System.Environment;
using GodotNode = Godot.Node;

namespace LibraryOfRuina.framework.combat;

internal readonly record struct CombatSafetyContext(
    string Surface,
    CombatStateLike? CombatState = null,
    Creature? Creature = null,
    MonsterModel? Monster = null,
    GameAction? Action = null);

internal static class CombatSafetyNet
{
    private const string LogTag = "LibraryOfRuina.CombatSafety";


    public static void EnsureReplayInitializedBeforeNestedCombat(AbstractRoom room)
    {
        if (room is not CombatRoom)
        {
            return;
        }

        RunManager runManager = RunManager.Instance;
        if (!runManager.IsInProgress
            || !runManager.CombatReplayWriter.IsEnabled
            || runManager.CombatReplayWriter.IsRecordingReplay)
        {
            return;
        }

        runManager.CombatReplayWriter.RecordInitialState(runManager.ToSave(null));
        Log.Warn(
            "[" + LogTag + "] restored missing combat replay initial state before nested combat"
            + " encounter=" + (room.ModelId?.Entry ?? "unknown"));
    }

    public static Task WrapTask(Task task, CombatSafetyContext context)
    {
        if (task.IsCompletedSuccessfully)
        {
            return task;
        }

        return AwaitWithGuard(task, context);
    }

    public static Task<AttackCommand> WrapMonsterAttackTask(Task<AttackCommand> task, AttackCommand command)
    {
        // A synchronously completed attack (instant fast mode, non-interactive waits, an attacker
        // that was already dead) must get the same death check: completion timing differs between
        // clients and must not decide whether the rest of the move runs.
        if (task.IsCompletedSuccessfully)
        {
            Creature? attacker = task.Result?.Attacker ?? command.Attacker;
            return attacker != null && IsDeadPerformingMonster(attacker)
                ? Task.FromException<AttackCommand>(new DeadMonsterAttackAbortedException(attacker))
                : task;
        }

        return AwaitMonsterAttackWithDeadAttackerGuard(task, command);
    }

    // Only the abort signal this class raises itself is handled. Anything else thrown inside a move
    // can come from any mod's hook listener or from local presentation, so it propagates as in
    // vanilla instead of being "recovered" on the one client that threw.
    private static async Task AwaitWithGuard(Task task, CombatSafetyContext context)
    {
        try
        {
            await task;
        }
        catch (DeadMonsterAttackAbortedException exception)
        {
            await RecoverFromDeadMonsterAttackAbort(context, exception);
        }
    }

    private static async Task<AttackCommand> AwaitMonsterAttackWithDeadAttackerGuard(
        Task<AttackCommand> task,
        AttackCommand command)
    {
        AttackCommand? result = await task;
        AttackCommand resolved = result ?? command;
        Creature? attacker = resolved.Attacker ?? command.Attacker;
        if (attacker != null && IsDeadPerformingMonster(attacker))
        {
            throw new DeadMonsterAttackAbortedException(attacker);
        }

        return resolved;
    }

    private static async Task RecoverFromDeadMonsterAttackAbort(
        CombatSafetyContext context,
        DeadMonsterAttackAbortedException exception)
    {
        CombatSafetyContext resolvedContext = context with
        {
            CombatState = ResolveCombatState(context),
            Creature = context.Creature ?? exception.Attacker,
            Monster = context.Monster ?? exception.Attacker.Monster
        };
        Log.Info(
            "[" + LogTag + "] 中止死亡敌人的剩余招式：" + DescribeCaughtError(exception)
            + "；context=" + DescribeContext(resolvedContext));

        // Deterministic state work on every client (the death that triggered the abort is
        // synchronized), so nothing here is caught: a failure must surface like in vanilla.
        // Mirrors the part of vanilla MonsterModel.PerformMove that the abort skipped.
        if (resolvedContext.Monster is { } monster)
        {
            VanillaPrivate.MonsterModelIsPerformingMove.Set(monster, false);
            monster.MoveStateMachine?.OnMovePerformed(monster.NextMove);

            Creature creature = monster.Creature;
            CombatStateLike? combatState = creature.CombatState ?? resolvedContext.CombatState;
            if (combatState != null
                && creature.IsDead
                && combatState.Enemies.Contains(creature)
                && Hook.ShouldCreatureBeRemovedFromCombatAfterDeath(combatState, creature))
            {
                combatState.RemoveCreature(creature);
            }
        }

        CombatManager.Instance.Unpause();
        RunManager.Instance.ActionExecutor.Unpause();
        if (CombatManager.Instance.IsInProgress)
        {
            await CombatManager.Instance.CheckWinCondition();
        }
    }

    private static bool IsDeadPerformingMonster(Creature? attacker)
    {
        try
        {
            return attacker is { IsMonster: true, IsDead: true, Monster.IsPerformingMove: true };
        }
        catch
        {
            return false;
        }
    }

    private static string DescribeCaughtError(Exception exception)
    {
        string message = string.IsNullOrWhiteSpace(exception.Message)
            ? "没有错误消息"
            : exception.Message.Replace(Environment.NewLine, " ");
        return exception.GetType().Name + " - " + message;
    }

    private static CombatStateLike? ResolveCombatState(CombatSafetyContext context)
    {
        if (context.CombatState != null)
        {
            return context.CombatState;
        }

        if (context.Creature?.CombatState != null)
        {
            return context.Creature.CombatState;
        }

        if (context.Monster?.Creature?.CombatState != null)
        {
            return context.Monster.Creature.CombatState;
        }

        try
        {
            return CombatManager.Instance.DebugOnlyGetState();
        }
        catch
        {
            return null;
        }
    }

    private static string DescribeContext(CombatSafetyContext context)
    {
        CombatStateLike? combatState = ResolveCombatState(context);
        string encounter = combatState?.Encounter?.Id.Entry ?? "none";
        string creature = context.Creature?.Name ?? context.Monster?.Creature?.Name ?? "none";
        string monster = context.Monster?.Id.Entry ?? context.Creature?.Monster?.Id.Entry ?? "none";
        string action = context.Action?.ToString() ?? "none";
        string multiplayer = ResolveMultiplayerLabel();

        return "surface=" + context.Surface
            + ",encounter=" + encounter
            + ",round=" + (combatState?.RoundNumber.ToString() ?? "none")
            + ",side=" + (combatState?.CurrentSide.ToString() ?? "none")
            + ",creature=" + creature
            + ",monster=" + monster
            + ",action=" + action
            + ",multiplayer=" + multiplayer;
    }

    private static string ResolveMultiplayerLabel()
    {
        try
        {
            NetGameType type = RunManager.Instance.NetService.Type;
            return type == NetGameType.Singleplayer ? "singleplayer" : type.ToString();
        }
        catch
        {
            return "unknown";
        }
    }

    private sealed class DeadMonsterAttackAbortedException(Creature attacker) : Exception
    {
        public Creature Attacker { get; } = attacker;
    }
}
