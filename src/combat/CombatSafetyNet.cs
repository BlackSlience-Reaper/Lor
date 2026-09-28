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

    private static readonly FieldInfo? MonsterIsPerformingMoveField =
        AccessTools.Field(typeof(MonsterModel), "_isPerformingMove");
    private static readonly FieldInfo? OverlayStackOverlaysField =
        AccessTools.Field(typeof(NOverlayStack), "_overlays");

    private static int _recoveryDepth;

    public static bool IsRecovering => _recoveryDepth > 0;

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
        if (task.IsCompletedSuccessfully || IsRecovering)
        {
            return task;
        }

        return AwaitWithGuard(task, context);
    }

    public static Task<AttackCommand> WrapMonsterAttackTask(Task<AttackCommand> task, AttackCommand command)
    {
        if (task.IsCompletedSuccessfully || IsRecovering)
        {
            return task;
        }

        return AwaitMonsterAttackWithDeadAttackerGuard(task, command);
    }

    public static bool ShouldSkipDeadMonsterAttackFollowup(Creature? attacker) =>
        IsDeadPerformingMonster(attacker);

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
        catch (Exception exception) when (ShouldIgnore(exception))
        {
        }
        catch (Exception exception) when (ShouldTreatAsNonFatalPresentationException(exception, context))
        {
            SuppressNonFatalPresentationException(context, exception);
        }
        catch (Exception exception) when (IsMultiplayerNonRecoverableException(exception))
        {
            LogNonRecoverableMultiplayerException(context, exception);
        }
        catch (Exception exception) when (ShouldSuppress(exception, context))
        {
            await RecoverAsync(context, exception, checkWinAfterRecovery: true);
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
        var recoveryReport = new CombatRecoveryReport();
        CombatSafetyContext resolvedContext = context with
        {
            CombatState = ResolveCombatState(context),
            Creature = context.Creature ?? exception.Attacker,
            Monster = context.Monster ?? exception.Attacker.Monster
        };

        TryFinalizeFailedMonsterMoves(resolvedContext, recoveryReport);
        TryUnpauseCombat(recoveryReport);
        await TryCheckWinCondition(recoveryReport);
        Log.Warn(
            "[" + LogTag + "] 消除死亡敌人的攻击后续："
            + DescribeCaughtError(exception)
            + "；恢复为：" + recoveryReport.Summary
            + "；context=" + DescribeContext(resolvedContext));
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

    private static async Task<bool> RecoverAsync(
        CombatSafetyContext context,
        Exception? exception,
        bool checkWinAfterRecovery)
    {
        if (Interlocked.CompareExchange(ref _recoveryDepth, 1, 0) != 0)
        {
            Log.Warn(
                "[" + LogTag + "] nested recovery skipped context="
                + DescribeContext(context)
                + (exception == null ? "" : " exception=" + exception));
            return false;
        }

        try
        {
            CombatSafetyContext resolvedContext = context with { CombatState = ResolveCombatState(context) };
            var recoveryReport = new CombatRecoveryReport();
            bool isDeathHookRecovery = IsDeathHookRecoveryContext(resolvedContext);
            if (exception != null)
            {
                if (!isDeathHookRecovery)
                {
                    TryFinalizeFailedMonsterMoves(resolvedContext, recoveryReport);
                }
                TryUnpauseCombat(recoveryReport);
            }

            bool recovered = false;
            if (exception != null && checkWinAfterRecovery && !isDeathHookRecovery)
            {
                await TryCheckWinCondition(recoveryReport);
            }

            if (exception != null)
            {
                LogCaughtException(resolvedContext, exception, recoveryReport);
            }

            return recovered;
        }
        finally
        {
            Interlocked.Exchange(ref _recoveryDepth, 0);
        }
    }

    private static bool ShouldSuppress(Exception exception, CombatSafetyContext context) =>
        !ShouldIgnore(exception) && IsCombatContext(context);

    /// <summary>
    /// 真实多人（Host/Client）下不能靠单边修改战斗状态恢复的异常：
    /// - 玩家选择层（PlayerChoiceResult / PlayerChoiceSynchronizer）的类型不匹配异常：这类异常意味着
    ///   双端 choice ID 已发生错位，主机单边"解暂停/重查胜负/杀敌"只会把错位固化成分叉（历史上 checksum
    ///   224 分叉踢人即由此放大）。
    /// - 栈中不含 LibraryOfRuina 的 UI ObjectDisposedException：属于其他模组的 UI 生命周期噪音，
    ///   不应触发战斗状态恢复。
    /// 单人、Replay 与 fake multiplayer 仍走原有恢复路径。
    /// </summary>
    private static bool IsMultiplayerNonRecoverableException(Exception exception)
    {
        if (!IsRealMultiplayerNetGame())
        {
            return false;
        }

        return IsMultiplayerChoiceLayerException(exception)
            || IsForeignUiDisposedException(exception);
    }

    private static bool IsMultiplayerChoiceLayerException(Exception exception)
    {
        if (exception is not InvalidOperationException)
        {
            return false;
        }

        string stackTrace = exception.StackTrace ?? string.Empty;
        return stackTrace.Contains("PlayerChoiceResult.As", StringComparison.Ordinal)
            || stackTrace.Contains(
                "MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceSynchronizer",
                StringComparison.Ordinal);
    }

    private static bool IsForeignUiDisposedException(Exception exception)
    {
        if (exception is not ObjectDisposedException)
        {
            return false;
        }

        return !(exception.StackTrace ?? string.Empty).Contains("LibraryOfRuina", StringComparison.Ordinal);
    }

    private static bool IsRealMultiplayerNetGame()
    {
        try
        {
            NetGameType type = RunManager.Instance.NetService.Type;
            return type is NetGameType.Host or NetGameType.Client;
        }
        catch
        {
            return false;
        }
    }

    private static void LogNonRecoverableMultiplayerException(
        CombatSafetyContext context,
        Exception exception)
    {
        string message =
            "[" + LogTag + "] multiplayer non-recoverable exception / 多人下不可恢复异常：跳过状态性恢复："
            + DescribeCaughtError(exception)
            + "；context=" + DescribeContext(context);
        if (IsMultiplayerChoiceLayerException(exception))
        {
            Log.Error(message + "；details=" + exception);
        }
        else
        {
            Log.Warn(message);
        }
    }

    private static bool IsDeathHookRecoveryContext(CombatSafetyContext context) =>
        string.Equals(context.Surface, "Hook.BeforeDeath", StringComparison.Ordinal)
        || string.Equals(context.Surface, "Hook.AfterDeath", StringComparison.Ordinal);

    private static bool ShouldIgnore(Exception exception) =>
        exception is OperationCanceledException;

    private static bool ShouldTreatAsNonFatalPresentationException(Exception exception, CombatSafetyContext context)
    {
        if (!IsCombatContext(context))
        {
            return false;
        }

        if (exception is LocException)
        {
            return IsPowerVfxPresentationException(exception);
        }

        if (exception is InvalidOperationException && IsBbcodePresentationException(exception))
        {
            return true;
        }

        if (exception is InvalidCastException && IsIntentRefreshPresentationException(exception))
        {
            return true;
        }

        if (IsChooseCardOverlayLifecycleException(exception))
        {
            return true;
        }

        return false;
    }

    private static bool IsPowerVfxPresentationException(Exception exception)
    {
        string stackTrace = exception.StackTrace ?? string.Empty;
        return stackTrace.Contains("MegaCrit.Sts2.Core.Nodes.Vfx.NPowerAppliedVfx.StartVfx", StringComparison.Ordinal)
            || stackTrace.Contains("MegaCrit.Sts2.Core.Nodes.Vfx.NPowerRemovedVfx.StartVfx", StringComparison.Ordinal)
            || stackTrace.Contains("MegaCrit.Sts2.Core.Nodes.Vfx.NPowerFlashVfx.StartVfx", StringComparison.Ordinal)
            || stackTrace.Contains("MegaCrit.Sts2.Core.Nodes.Combat.NCreature.OnPowerIncreased", StringComparison.Ordinal);
    }

    private static bool IsBbcodePresentationException(Exception exception)
    {
        string stackTrace = exception.StackTrace ?? string.Empty;
        if (!stackTrace.Contains("MegaCrit.Sts2.addons.mega_text.MegaLabelHelper.ParseBbcode", StringComparison.Ordinal)
            && !stackTrace.Contains("MegaCrit.Sts2.addons.mega_text.MegaRichTextLabel.AdjustFontSize", StringComparison.Ordinal))
        {
            return false;
        }

        return exception.Message.Contains("Found end tag", StringComparison.Ordinal)
            || exception.Message.Contains("expected", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsIntentRefreshPresentationException(Exception exception)
    {
        string stackTrace = exception.StackTrace ?? string.Empty;
        return stackTrace.Contains("MegaCrit.Sts2.Core.Nodes.Combat.NIntent.Create", StringComparison.Ordinal)
            && (stackTrace.Contains("MegaCrit.Sts2.Core.Nodes.Combat.NCreature.UpdateIntent", StringComparison.Ordinal)
                || stackTrace.Contains("MegaCrit.Sts2.Core.Nodes.Combat.NCreature.RefreshIntents", StringComparison.Ordinal));
    }

    private static void SuppressNonFatalPresentationException(CombatSafetyContext context, Exception exception)
    {
        CombatSafetyContext resolvedContext = context with { CombatState = ResolveCombatState(context) };
        bool removedBrokenChooseCardOverlay = TryDismissBrokenChooseCardOverlay(exception);
        Log.Warn(
            "[" + LogTag + "] suppressed non-fatal combat presentation exception / 消除战斗表现层错误："
            + DescribeCaughtError(exception)
            + (removedBrokenChooseCardOverlay ? "；已清理损坏的选卡界面覆盖层" : string.Empty)
            + "；恢复为：跳过本次表现刷新，保留战斗逻辑继续运行"
            + "；context=" + DescribeContext(resolvedContext));
    }

    private static bool IsChooseCardOverlayLifecycleException(Exception exception)
    {
        if (exception is not NullReferenceException && exception is not ObjectDisposedException)
        {
            return false;
        }

        string stackTrace = exception.StackTrace ?? string.Empty;
        if (!stackTrace.Contains("MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NChooseACardSelectionScreen", StringComparison.Ordinal)
            && !stackTrace.Contains("MegaCrit.Sts2.Core.Nodes.Screens.Overlays.NOverlayStack", StringComparison.Ordinal))
        {
            return false;
        }

        return stackTrace.Contains("NChooseACardSelectionScreen._Ready", StringComparison.Ordinal)
            || stackTrace.Contains("NChooseACardSelectionScreen.ShowScreen", StringComparison.Ordinal)
            || stackTrace.Contains("NChooseACardSelectionScreen.AfterOverlayShown", StringComparison.Ordinal)
            || stackTrace.Contains("NChooseACardSelectionScreen.AfterOverlayHidden", StringComparison.Ordinal)
            || stackTrace.Contains("NChooseACardSelectionScreen.get_DefaultFocusedControl", StringComparison.Ordinal)
            || stackTrace.Contains("NOverlayStack.Push", StringComparison.Ordinal)
            || stackTrace.Contains("NOverlayStack.Remove", StringComparison.Ordinal)
            || stackTrace.Contains("NOverlayStack.Clear", StringComparison.Ordinal);
    }

    private static bool TryDismissBrokenChooseCardOverlay(Exception exception)
    {
        if (!IsChooseCardOverlayLifecycleException(exception))
        {
            return false;
        }

        NOverlayStack? overlayStack = NOverlayStack.Instance;
        if (overlayStack?.Peek() is not NChooseACardSelectionScreen brokenScreen)
        {
            return false;
        }

        try
        {
            overlayStack.Remove(brokenScreen);
            return true;
        }
        catch (Exception removeException)
        {
            Log.Warn(
                "[" + LogTag + "] normal choose-card overlay removal failed after lifecycle exception: "
                + removeException.GetType().Name
                + ": "
                + removeException.Message);
        }

        try
        {
            if (OverlayStackOverlaysField?.GetValue(overlayStack) is List<IOverlayScreen> overlays)
            {
                overlays.Remove(brokenScreen);
            }

            GodotNode? parent = brokenScreen.GetParent();
            if (parent != null)
            {
                parent.RemoveChild(brokenScreen);
            }

            brokenScreen.QueueFree();

            if (overlayStack.Peek() != null)
            {
                overlayStack.ShowOverlays();
            }
            else
            {
                overlayStack.HideBackstop();
            }

            ActiveScreenContext.Instance.Update();
            return true;
        }
        catch (Exception cleanupException)
        {
            Log.Warn(
                "[" + LogTag + "] forced choose-card overlay cleanup failed after lifecycle exception: "
                + cleanupException.GetType().Name
                + ": "
                + cleanupException.Message);
            return false;
        }
    }

    private static void LogCaughtException(
        CombatSafetyContext context,
        Exception exception,
        CombatRecoveryReport recoveryReport)
    {
        if (IsKnownDisposedUiResourceException(exception))
        {
            Log.Warn(
                "[" + LogTag + "] caught disposed UI resource / 消除已释放的战斗UI资源错误："
                + DescribeCaughtError(exception)
                + "；尝试恢复为：" + recoveryReport.Summary
                + "；context=" + DescribeContext(context)
                + "；details=" + exception);
            return;
        }

        Log.Error(
            "[" + LogTag + "] caught combat exception / 发现战斗异常："
            + DescribeCaughtError(exception)
            + "；尝试恢复为：" + recoveryReport.Summary
            + "；上下文=" + DescribeContext(context)
            + "；错误细节=" + exception);
    }

    private static string DescribeCaughtError(Exception exception)
    {
        string message = string.IsNullOrWhiteSpace(exception.Message)
            ? "没有错误消息"
            : exception.Message.Replace(Environment.NewLine, " ");
        return exception.GetType().Name + " - " + message;
    }

    private static bool IsKnownDisposedUiResourceException(Exception exception)
    {
        if (exception is not ObjectDisposedException objectDisposedException)
        {
            return false;
        }

        return objectDisposedException.ObjectName?.StartsWith("Godot.", StringComparison.Ordinal) == true
            || exception.StackTrace?.Contains("MegaCrit.Sts2.Core.Nodes.Cards.NCardGrid", StringComparison.Ordinal) == true
            || exception.StackTrace?.Contains("MegaCrit.Sts2.Core.Nodes.Relics.NRelicInventoryHolder", StringComparison.Ordinal) == true;
    }

    private static bool IsCombatContext(CombatSafetyContext context)
    {
        CombatStateLike? combatState = ResolveCombatState(context);
        if (combatState != null)
        {
            return true;
        }

        try
        {
            return CombatManager.Instance.IsInProgress;
        }
        catch
        {
            return false;
        }
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

    private static void TryFinalizeFailedMonsterMoves(
        CombatSafetyContext context,
        CombatRecoveryReport recoveryReport)
    {
        List<MonsterModel> failedMonsters = ResolveFailedMoveMonsters(context).Distinct().ToList();
        foreach (MonsterModel monster in failedMonsters)
        {
            TryFinalizeFailedMonsterMove(context, monster, recoveryReport);
            TryRemoveDeadFailedMonster(context, monster, recoveryReport);
        }
    }

    private static IEnumerable<MonsterModel> ResolveFailedMoveMonsters(CombatSafetyContext context)
    {
        MonsterModel? contextMonster = context.Monster ?? context.Creature?.Monster;
        if (contextMonster != null)
        {
            yield return contextMonster;
        }

        CombatStateLike? combatState = ResolveCombatState(context);
        if (combatState == null)
        {
            yield break;
        }

        foreach (Creature enemy in combatState.Enemies)
        {
            if (enemy.Monster is { IsPerformingMove: true } monster)
            {
                yield return monster;
            }
        }
    }

    private static void TryFinalizeFailedMonsterMove(
        CombatSafetyContext context,
        MonsterModel monster,
        CombatRecoveryReport recoveryReport)
    {
        try
        {
            MonsterIsPerformingMoveField?.SetValue(monster, false);
            monster.MoveStateMachine?.OnMovePerformed(monster.NextMove);
            recoveryReport.Add("结束卡住的敌方行动 " + DescribeMonster(monster));
        }
        catch (Exception exception)
        {
            Log.Warn(
                "[" + LogTag + "] failed to finalize monster move context="
                + DescribeContext(context)
                + " exception=" + exception.Message);
        }
    }

    private static void TryRemoveDeadFailedMonster(
        CombatSafetyContext context,
        MonsterModel monster,
        CombatRecoveryReport recoveryReport)
    {
        try
        {
            Creature creature = monster.Creature;
            CombatStateLike? combatState = creature.CombatState ?? ResolveCombatState(context);
            if (combatState == null
                || creature.IsAlive
                || !combatState.Enemies.Contains(creature)
                || !Hook.ShouldCreatureBeRemovedFromCombatAfterDeath(combatState, creature))
            {
                return;
            }

            combatState.RemoveCreature(creature);
            recoveryReport.Add("从战斗状态移除已死亡敌人 " + DescribeMonster(monster));
            Log.Warn(
                "[" + LogTag + "] removed dead failed monster from combat state context="
                + DescribeContext(context with { CombatState = combatState, Creature = creature, Monster = monster }));
        }
        catch (Exception exception)
        {
            Log.Warn(
                "[" + LogTag + "] failed to remove dead failed monster context="
                + DescribeContext(context)
                + " exception=" + exception.Message);
        }
    }

    private static string DescribeMonster(MonsterModel monster) =>
        monster.Id.Entry + "/" + (monster.Creature?.Name ?? "unknown");

    private static void TryUnpauseCombat(CombatRecoveryReport recoveryReport)
    {
        try
        {
            CombatManager.Instance.Unpause();
            RunManager.Instance.ActionExecutor.Unpause();
            recoveryReport.Add("确保战斗和行动队列解除暂停");
        }
        catch (Exception exception)
        {
            Log.Warn("[" + LogTag + "] failed to unpause combat: " + exception.Message);
        }
    }

    private static async Task TryCheckWinCondition(CombatRecoveryReport recoveryReport)
    {
        try
        {
            if (CombatManager.Instance.IsInProgress)
            {
                await CombatManager.Instance.CheckWinCondition();
                recoveryReport.Add("重新检查战斗胜负条件");
            }
        }
        catch (Exception exception)
        {
            Log.Error("[" + LogTag + "] CheckWinCondition failed during recovery: " + exception);
        }
    }

    private sealed class CombatRecoveryReport
    {
        private readonly List<string> _actions = [];

        public string Summary => _actions.Count == 0
            ? "阻止异常继续打断战斗，未发现需要额外修复的战斗状态"
            : string.Join("；", _actions);

        public void Add(string action)
        {
            if (!string.IsNullOrWhiteSpace(action) && !_actions.Contains(action))
            {
                _actions.Add(action);
            }
        }
    }

    private sealed class DeadMonsterAttackAbortedException(Creature attacker) : Exception
    {
        public Creature Attacker { get; } = attacker;
    }
}
