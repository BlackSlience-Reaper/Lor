using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using LibraryOfRuina.monsters.BigBadWolf;
using LibraryOfRuina.monsters.GalaxyChild;
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
using LibraryOfRuina.interop;

namespace LibraryOfRuina.combat;

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
        string beforeDeathListeners = TryDescribeBeforeDeathListenersForContext(context);

        return "surface=" + context.Surface
            + ",encounter=" + encounter
            + ",round=" + (combatState?.RoundNumber.ToString() ?? "none")
            + ",side=" + (combatState?.CurrentSide.ToString() ?? "none")
            + ",creature=" + creature
            + ",monster=" + monster
            + ",action=" + action
            + ",multiplayer=" + multiplayer
            + beforeDeathListeners;
    }

    private static string TryDescribeBeforeDeathListenersForContext(CombatSafetyContext context)
    {
        if (!string.Equals(context.Surface, "Hook.BeforeDeath", StringComparison.Ordinal))
        {
            return string.Empty;
        }

        CombatStateLike? combatState = ResolveCombatState(context);
        IRunState? runState = combatState?.RunState;
        Creature? target = context.Creature;
        if (runState == null || target == null)
        {
            return string.Empty;
        }

        try
        {
            List<string> listeners = runState
                .IterateHookListeners(combatState)
                .Where(OverridesBeforeDeath)
                .Select(model => DescribeBeforeDeathListener(model, target))
                .Distinct()
                .Take(24)
                .ToList();

            return listeners.Count == 0
                ? ",beforeDeathListeners=none"
                : ",beforeDeathListeners=" + string.Join(" | ", listeners) + (listeners.Count >= 24 ? " | ..." : string.Empty);
        }
        catch (Exception exception)
        {
            return ",beforeDeathListeners=enumeration_failed:" + exception.GetType().Name;
        }
    }

    private static bool OverridesBeforeDeath(AbstractModel model)
    {
        try
        {
            MethodInfo? method = model.GetType().GetMethod(
                nameof(AbstractModel.BeforeDeath),
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                types: [typeof(Creature)],
                modifiers: null);
            return method?.DeclaringType != null && method.DeclaringType != typeof(AbstractModel);
        }
        catch
        {
            return false;
        }
    }

    private static string DescribeBeforeDeathListener(AbstractModel model, Creature target)
    {
        string modelType = model.GetType().FullName ?? model.GetType().Name;
        string modelId = SafeDescribeModelId(model);
        string scope = model switch
        {
            MonsterModel monsterModel when ReferenceEquals(monsterModel.Creature, target) =>
                DescribeTargetMonsterScope(monsterModel),
            MonsterModel monsterModel => "monster-owner=" + monsterModel.Id.Entry,
            PowerModel powerModel => DescribePowerScope(powerModel, target),
            CardModel cardModel => "card-owner=" + (cardModel.Owner?.NetId.ToString() ?? "none"),
            _ => string.Empty
        };

        return string.IsNullOrEmpty(scope)
            ? modelType + "[" + modelId + "]"
            : modelType + "[" + modelId + "]@" + scope;
    }

    private static string DescribeTargetMonsterScope(MonsterModel monsterModel)
    {
        if (monsterModel is BigBadWolf bigBadWolf)
        {
            return "target-monster,pendingCard=" + bigBadWolf.HasPendingCard.ToString().ToLowerInvariant();
        }

        return "target-monster";
    }

    private static string DescribePowerScope(PowerModel powerModel, Creature target)
    {
        List<string> parts = [];

        if (ReferenceEquals(powerModel.Owner, target))
        {
            parts.Add("power-owner-is-target");
        }
        else
        {
            parts.Add("power-owner=" + DescribeCreature(powerModel.Owner));
        }

        parts.Add("power-target=" + DescribeCreature(powerModel.Target));

        if (powerModel is SwipePower swipePower)
        {
            parts.Add("stolenCard=" + (swipePower.StolenCard?.Id.Entry ?? "none"));
        }

        return string.Join(",", parts);
    }

    private static string DescribeCreature(Creature? creature)
    {
        if (creature == null)
        {
            return "none";
        }

        if (creature.Monster != null)
        {
            return creature.Monster.Id.Entry;
        }

        if (creature.Player != null)
        {
            return "player-" + creature.Player.NetId;
        }

        if (creature.PetOwner != null)
        {
            return "pet-" + creature.PetOwner.NetId;
        }

        return creature.Name;
    }

    private static string SafeDescribeModelId(AbstractModel model)
    {
        try
        {
            return model.Id.Entry;
        }
        catch
        {
            return "unknown";
        }
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
