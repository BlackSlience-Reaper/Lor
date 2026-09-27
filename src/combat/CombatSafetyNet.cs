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

    private static readonly List<CombatSafetyGuard> Guards = [];
    private static readonly Dictionary<GuardCounterKey, GuardCounterState> GuardCounters = [];
    private static readonly FieldInfo? MonsterIsPerformingMoveField =
        AccessTools.Field(typeof(MonsterModel), "_isPerformingMove");
    private static readonly PropertyInfo? CardCombatStateProperty =
        AccessTools.Property(typeof(CardModel), "CombatState");
    private static readonly FieldInfo? OverlayStackOverlaysField =
        AccessTools.Field(typeof(NOverlayStack), "_overlays");
    private static readonly FieldInfo? SavedPropertyNameToNetIdMapField =
        AccessTools.DeclaredField(typeof(ModelIdSerializationCache), "_propertyNameToNetIdMap");
    private static readonly FieldInfo? SavedPropertyNetIdToNameMapField =
        AccessTools.DeclaredField(typeof(ModelIdSerializationCache), "_netIdToPropertyNameMap");
    private static readonly PropertyInfo? SavedPropertyIdBitSizeProperty =
        AccessTools.Property(typeof(ModelIdSerializationCache), "PropertyIdBitSize");

    private static bool _initialized;
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

    public static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        RegisterRepeatedCombatStateGuard(
            id: "GALAXY_CHILD_DOUBLE_FAKE_DEATH",
            reason: "two Galaxy Friends stayed fake-dead across repeated combat checks",
            consecutiveDetections: 1,
            isProblemState: GalaxyFriend.ShouldTriggerPartingTears,
            recoverAsync: GalaxyFriend.TriggerPartingTearsVictory);
    }

    public static void RegisterRecoveryGuard(
        string id,
        string reason,
        int priority,
        Func<CombatSafetyContext, Exception?, Task<bool>> recoverAsync)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Guard id is required.", nameof(id));
        }

        Guards.Add(new CombatSafetyGuard(id, reason, priority, recoverAsync));
        Guards.Sort(static (left, right) => left.Priority.CompareTo(right.Priority));
    }

    public static void RegisterRepeatedCombatStateGuard(
        string id,
        string reason,
        int consecutiveDetections,
        Func<CombatStateLike?, bool> isProblemState,
        Func<CombatStateLike?, Task> recoverAsync,
        int priority = 100)
    {
        if (consecutiveDetections <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(consecutiveDetections));
        }

        RegisterRecoveryGuard(
            id,
            reason,
            priority,
            async (context, _) =>
            {
                CombatStateLike? combatState = ResolveCombatState(context);
                if (combatState == null || !isProblemState(combatState))
                {
                    ResetCounter(id, combatState);
                    return false;
                }

                GuardCounterState counter = IncrementCounter(id, combatState);
                Log.Warn(
                    "[" + LogTag + "] guard detected id=" + id
                    + " count=" + counter.Count + "/" + consecutiveDetections
                    + " beat=" + counter.LastBeat
                    + " context=" + DescribeContext(context with { CombatState = combatState }));

                if (counter.Count < consecutiveDetections)
                {
                    return false;
                }

                ResetCounter(id, combatState);
                Log.Error(
                    "[" + LogTag + "] guard recovering id=" + id
                    + " reason=" + reason
                    + " context=" + DescribeContext(context with { CombatState = combatState }));
                await recoverAsync(combatState);
                return true;
            });
    }

    public static Task WrapTask(Task task, CombatSafetyContext context)
    {
        if (task.IsCompletedSuccessfully || IsRecovering)
        {
            return task;
        }

        return AwaitWithGuard(task, context);
    }

    public static Task RunTaskHelperSafely(Task? task)
    {
        CombatSafetyContext context = new("TaskHelper.RunSafely");
        if (task == null)
        {
            Log.Warn("[" + LogTag + "] TaskHelper.RunSafely received null task; skipped wrapper and ran guard point.");
            return RunGuardPoint(context);
        }

        return AwaitTaskHelperWithGuard(task, context);
    }

    public static Task WrapHookTask(Task? task, string surface, object?[]? args)
    {
        object?[] resolvedArgs = args ?? Array.Empty<object?>();
        if (task == null)
        {
            CombatSafetyContext context = ResolveContextFromArgs(surface, resolvedArgs);
            Log.Warn(
                "[" + LogTag + "] hook returned null task; skipped wrapper surface=" + surface
                + " context=" + DescribeContext(context)
                + TryDescribeHookNullTaskDetails(surface, resolvedArgs));
            return Task.CompletedTask;
        }

        if (task.IsCompletedSuccessfully || IsRecovering)
        {
            return task;
        }

        return AwaitWithGuard(task, ResolveContextFromArgs(surface, resolvedArgs));
    }

    public static Task<bool> WrapCheckWinConditionTask(Task<bool> task, CombatSafetyContext context)
    {
        if (IsRecovering)
        {
            return task;
        }

        return AwaitCheckWinConditionWithGuard(task, context);
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

    public static async Task RunGuardPoint(CombatSafetyContext context)
    {
        if (!IsCombatContext(context) || IsRecovering)
        {
            return;
        }

        await RecoverAsync(context, exception: null, checkWinAfterRecovery: true);
    }

    /// <summary>
    /// Synchronous convergence point that runs right before every multiplayer checksum
    /// snapshot (patched into ChecksumTracker.GenerateChecksum). If an enemy's death
    /// pipeline stalled mid-hook on this machine, the creature sits at 0 HP inside the
    /// combat state while peers have already removed it, which desyncs the checksum and
    /// kicks clients. Finalizing the stuck death here mirrors the removal block of
    /// CreatureCmd.KillWithoutCheckingWinCondition, so every machine snapshots the same
    /// post-death state. Fake-death / corpse-keeping powers are untouched because the
    /// check goes through Hook.ShouldCreatureBeRemovedFromCombatAfterDeath.
    /// </summary>
    public static void FinalizeStuckDeadEnemiesBeforeChecksum()
    {
        if (IsRecovering)
        {
            return;
        }

        CombatStateLike? combatState;
        try
        {
            if (!CombatManager.Instance.IsInProgress)
            {
                return;
            }

            combatState = CombatManager.Instance.DebugOnlyGetState();
        }
        catch
        {
            return;
        }

        if (combatState == null)
        {
            return;
        }

        try
        {
            Creature[] stuckEnemies = combatState.Enemies
                .Where(enemy =>
                    enemy.IsDead
                    && Hook.ShouldCreatureBeRemovedFromCombatAfterDeath(combatState, enemy))
                .ToArray();
            if (stuckEnemies.Length == 0)
            {
                return;
            }

            foreach (Creature enemy in stuckEnemies)
            {
                CombatManager.Instance.RemoveCreature(enemy);
                if (enemy.Monster is not { IsPerformingMove: true })
                {
                    combatState.RemoveCreature(enemy);
                }

                Log.Warn(
                    "[" + LogTag + "] checksum guard finalized stuck dead enemy"
                    + " monster=" + (enemy.Monster?.Id.Entry ?? "none")
                    + " hp=" + enemy.CurrentHp
                    + " encounter=" + (combatState.Encounter?.Id.Entry ?? "none"));
            }
        }
        catch (Exception exception)
        {
            Log.Error(
                "[" + LogTag + "] failed to finalize stuck dead enemies before checksum: "
                + exception);
        }
    }

    private static async Task AwaitWithGuard(Task task, CombatSafetyContext context)
    {
        try
        {
            await task;
            await RunGuardPoint(context);
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
        catch (Exception exception) when (IsRunAbandonTaskHelperException(context, exception))
        {
            await RecoverFromRunAbandonTaskFailure(context, exception);
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

    private static async Task AwaitTaskHelperWithGuard(Task task, CombatSafetyContext context)
    {
        try
        {
            await task;
            await RunGuardPoint(context);
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
        catch (Exception exception) when (IsRunAbandonTaskHelperException(context, exception))
        {
            await RecoverFromRunAbandonTaskFailure(context, exception);
        }
        catch (Exception exception) when (IsMultiplayerNonRecoverableException(exception))
        {
            LogNonRecoverableMultiplayerException(context, exception);
        }
        catch (Exception exception) when (ShouldSuppress(exception, context))
        {
            await RecoverAsync(context, exception, checkWinAfterRecovery: true);
        }
        catch (Exception exception)
        {
            Log.Error(exception.ToString());
            SentryService.CaptureException(exception);
            throw;
        }
    }

    private static async Task<bool> AwaitCheckWinConditionWithGuard(Task<bool> task, CombatSafetyContext context)
    {
        try
        {
            bool result = await task;
            if (!result)
            {
                bool recovered = await RecoverAsync(context, exception: null, checkWinAfterRecovery: false);
                if (recovered)
                {
                    return true;
                }
            }

            return result;
        }
        catch (DeadMonsterAttackAbortedException exception)
        {
            await RecoverFromDeadMonsterAttackAbort(context, exception);
            return !CombatManager.Instance.IsInProgress;
        }
        catch (Exception exception) when (ShouldIgnore(exception))
        {
            return false;
        }
        catch (Exception exception) when (ShouldTreatAsNonFatalPresentationException(exception, context))
        {
            SuppressNonFatalPresentationException(context, exception);
            return false;
        }
        catch (Exception exception) when (IsMultiplayerNonRecoverableException(exception))
        {
            LogNonRecoverableMultiplayerException(context, exception);
            return false;
        }
        catch (Exception exception) when (ShouldSuppress(exception, context))
        {
            await RecoverAsync(context, exception, checkWinAfterRecovery: true);
            return !CombatManager.Instance.IsInProgress;
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

    private static async Task RecoverFromRunAbandonTaskFailure(
        CombatSafetyContext context,
        Exception exception)
    {
        CombatSafetyContext resolvedContext = context with { CombatState = ResolveCombatState(context) };
        var recoveryReport = new CombatRecoveryReport();

        bool endedAbandonedRun = await TryEndInterruptedAbandonedRun(resolvedContext, recoveryReport);
        if (!endedAbandonedRun)
        {
            TryUnpauseCombat(recoveryReport);
            await TryCheckWinCondition(recoveryReport);
        }

        Log.Warn(
            "[" + LogTag + "] recovered interrupted run abandon: "
            + DescribeCaughtError(exception)
            + "; recovery=" + recoveryReport.Summary
            + "; context=" + DescribeContext(resolvedContext)
            + "; details=" + exception);
    }

    private static async Task<bool> TryEndInterruptedAbandonedRun(
        CombatSafetyContext context,
        CombatRecoveryReport recoveryReport)
    {
        try
        {
            CombatStateLike? combatState = ResolveCombatState(context);
            if (combatState == null || !RunManager.Instance.IsAbandoned)
            {
                return false;
            }

            if (CombatManager.Instance.IsInProgress)
            {
                CombatManager.Instance.LoseCombat();
                recoveryReport.Add("forced combat loss after interrupted run abandon");
                await CombatManager.Instance.CheckWinCondition();
            }

            TryShowGameOverForInterruptedAbandon(recoveryReport);
            return true;
        }
        catch (Exception recoveryException)
        {
            Log.Error(
                "[" + LogTag + "] failed to recover interrupted run abandon context="
                + DescribeContext(context)
                + " exception=" + recoveryException);
            return false;
        }
    }

    private static void TryShowGameOverForInterruptedAbandon(CombatRecoveryReport recoveryReport)
    {
        NRun? nRun = NRun.Instance;
        if (!TestMode.IsOff || !RunManager.Instance.IsInProgress || nRun == null)
        {
            return;
        }

        try
        {
            nRun.RunMusicController.StopMusic();
            NAudioManager.Instance?.PlayMusic("event:/temp/sfx/game_over");
            var serializableRun = RunManager.Instance.OnEnded(isVictory: false);
            nRun.ShowGameOverScreen(serializableRun);
            recoveryReport.Add("completed game-over cleanup for interrupted run abandon");
        }
        catch (Exception exception)
        {
            recoveryReport.Add("combat ended but game-over cleanup failed: " + exception.GetType().Name);
            Log.Error("[" + LogTag + "] game-over cleanup failed after interrupted run abandon: " + exception);
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
                TryRecoverMissingSavedPropertyNetIds(exception, recoveryReport);
            }

            bool recovered = false;
            foreach (CombatSafetyGuard guard in Guards)
            {
                try
                {
                    if (await guard.RecoverAsync(resolvedContext, exception))
                    {
                        recovered = true;
                        recoveryReport.Add("执行保护规则 " + guard.Id + "（" + guard.Reason + "）");
                    }
                }
                catch (Exception guardException)
                {
                    Log.Error(
                        "[" + LogTag + "] guard failed id=" + guard.Id
                        + " context=" + DescribeContext(resolvedContext)
                        + " exception=" + guardException);
                }
            }

            if ((recovered || exception != null) && checkWinAfterRecovery && !isDeathHookRecovery)
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

    private static bool IsRunAbandonTaskHelperException(CombatSafetyContext context, Exception exception) =>
        string.Equals(context.Surface, "TaskHelper.RunSafely", StringComparison.Ordinal)
        && IsRunAbandonStack(exception);

    private static bool IsRunAbandonStack(Exception exception)
    {
        string stackTrace = exception.StackTrace ?? string.Empty;
        return stackTrace.Contains("MegaCrit.Sts2.Core.Runs.RunManager.AbandonInternal", StringComparison.Ordinal)
            || stackTrace.Contains("MegaCrit.Sts2.Core.Runs.RunManager.GuaranteeKillAllPlayers", StringComparison.Ordinal);
    }

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

        // 独立重击特效失败只影响画面，不能解除正在等待结算的战斗队列。
        if (context.Surface == "TaskHelper.RunSafely"
            && (exception is NullReferenceException || exception is ObjectDisposedException)
            && (exception.StackTrace ?? string.Empty).Contains(
                "MegaCrit.Sts2.Core.Nodes.Vfx.NHeavyBluntVfx.PlaySequence",
                StringComparison.Ordinal))
        {
            return true;
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

        if (IsKnownNonCombatTaskHelperException(context, exception, out string nonCombatArea))
        {
            Log.Warn(
                "[" + LogTag + "] caught TaskHelper exception outside combat seam: "
                + DescribeCaughtError(exception)
                + "; kept recovery diagnostics only; area=" + nonCombatArea
                + "; context=" + DescribeContext(context)
                + "; details=" + exception);
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

    private static bool IsKnownNonCombatTaskHelperException(
        CombatSafetyContext context,
        Exception exception,
        out string area)
    {
        area = string.Empty;

        if (!string.Equals(context.Surface, "TaskHelper.RunSafely", StringComparison.Ordinal))
        {
            return false;
        }

        string stackTrace = exception.StackTrace ?? string.Empty;
        if (stackTrace.Contains("MegaCrit.Sts2.Core.Runs.RunManager.AbandonInternal", StringComparison.Ordinal))
        {
            area = "run abandon";
            return true;
        }

        if (stackTrace.Contains("MegaCrit.Sts2.Core.GameActions.MoveToMapCoordAction.GoToMapCoord", StringComparison.Ordinal)
            || stackTrace.Contains("MegaCrit.Sts2.Core.Nodes.Screens.Map.NMapScreen.TravelToMapCoord", StringComparison.Ordinal)
            || stackTrace.Contains("MegaCrit.Sts2.Core.Runs.RunManager.EnterMapPointInternal", StringComparison.Ordinal)
            || stackTrace.Contains("MegaCrit.Sts2.Core.Runs.RunManager.EnterRoomInternal", StringComparison.Ordinal))
        {
            area = "map travel";
            return true;
        }

        if (stackTrace.Contains("MegaCrit.Sts2.Core.Nodes.Screens.DailyRun.NDailyRunLeaderboard", StringComparison.Ordinal))
        {
            area = "daily run leaderboard";
            return true;
        }

        if (stackTrace.Contains("MegaCrit.Sts2.Core.Nodes.Screens.MainMenu.NMainMenu", StringComparison.Ordinal)
            || stackTrace.Contains("MegaCrit.Sts2.Core.Nodes.Screens.Settings.NSettingsScreen", StringComparison.Ordinal))
        {
            area = "main menu/settings";
            return true;
        }

        return false;
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

    private static CombatSafetyContext ResolveContextFromArgs(string surface, object?[] args)
    {
        CombatStateLike? combatState = null;
        Creature? creature = null;
        MonsterModel? monster = null;

        foreach (object? arg in args)
        {
            switch (arg)
            {
                case CombatStateLike state:
                    combatState ??= state;
                    break;
                case Creature argCreature:
                    creature ??= argCreature;
                    combatState ??= argCreature.CombatState;
                    monster ??= argCreature.Monster;
                    break;
                case Player player:
                    creature ??= player.Creature;
                    combatState ??= player.Creature?.CombatState;
                    monster ??= player.Creature?.Monster;
                    break;
                case MonsterModel argMonster:
                    monster ??= argMonster;
                    creature ??= argMonster.Creature;
                    combatState ??= argMonster.Creature?.CombatState;
                    break;
                case CardModel card:
                    combatState ??= TryResolveCardCombatState(card);
                    break;
                case CardPlay cardPlay:
                    creature ??= cardPlay.Card.Owner?.Creature;
                    monster ??= cardPlay.Target?.Monster;
                    combatState ??= TryResolveCardCombatState(cardPlay.Card);
                    break;
                case PowerModel power:
                    creature ??= power.Owner;
                    combatState ??= power.Owner?.CombatState;
                    monster ??= power.Owner?.Monster;
                    break;
            }
        }

        return new CombatSafetyContext(surface, combatState, creature, monster);
    }

    private static CombatStateLike? TryResolveCardCombatState(CardModel card)
    {
        try
        {
            return CardCombatStateProperty?.GetValue(card) as CombatStateLike;
        }
        catch
        {
            return null;
        }
    }

    private static GuardCounterState IncrementCounter(string id, CombatStateLike combatState)
    {
        var key = new GuardCounterKey(RuntimeHelpers.GetHashCode(combatState), id);
        string beat = DescribeBeat(combatState);

        if (!GuardCounters.TryGetValue(key, out GuardCounterState state))
        {
            state = new GuardCounterState(0, string.Empty);
        }

        if (state.LastBeat != beat)
        {
            state = new GuardCounterState(state.Count + 1, beat);
            GuardCounters[key] = state;
        }

        return state;
    }

    private static void ResetCounter(string id, CombatStateLike? combatState)
    {
        if (combatState == null)
        {
            return;
        }

        GuardCounters.Remove(new GuardCounterKey(RuntimeHelpers.GetHashCode(combatState), id));
    }

    private static string DescribeBeat(CombatStateLike combatState) =>
        "round=" + combatState.RoundNumber + ",side=" + combatState.CurrentSide;

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

    private static string TryDescribeHookNullTaskDetails(string surface, object?[] args)
    {
        List<string> details = [];

        string patchOwners = TryDescribeHookPatchOwners(surface);
        if (!string.IsNullOrEmpty(patchOwners))
        {
            details.Add("patchOwners=" + patchOwners);
        }

        string cardPlay = TryDescribeCardPlay(args);
        if (!string.IsNullOrEmpty(cardPlay))
        {
            details.Add("cardPlay=" + cardPlay);
        }

        string choiceModel = TryDescribeChoiceContextModel(args);
        if (!string.IsNullOrEmpty(choiceModel))
        {
            details.Add("choiceModel=" + choiceModel);
        }

        return details.Count == 0 ? string.Empty : "," + string.Join(",", details);
    }

    private static string TryDescribeHookPatchOwners(string surface)
    {
        if (!surface.StartsWith("Hook.", StringComparison.Ordinal))
        {
            return string.Empty;
        }

        try
        {
            string methodName = surface.Substring("Hook.".Length);
            MethodBase? method = typeof(Hook)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(candidate => string.Equals(candidate.Name, methodName, StringComparison.Ordinal));
            if (method == null)
            {
                return "method_not_found";
            }

            Patches? patchInfo = Harmony.GetPatchInfo(method);
            if (patchInfo == null || patchInfo.Owners.Count == 0)
            {
                return "none";
            }

            IReadOnlyList<string> owners = patchInfo.Owners
                .Distinct(StringComparer.Ordinal)
                .Take(12)
                .ToList();
            return string.Join("|", owners) + (patchInfo.Owners.Count > owners.Count ? "|..." : string.Empty);
        }
        catch (Exception exception)
        {
            return "lookup_failed:" + exception.GetType().Name;
        }
    }

    private static string TryDescribeCardPlay(object?[] args)
    {
        CardPlay? cardPlay = args.OfType<CardPlay>().FirstOrDefault();
        if (cardPlay == null)
        {
            return string.Empty;
        }

        string card = SafeDescribeModelId(cardPlay.Card);
        string owner = cardPlay.Card.Owner?.NetId.ToString() ?? "none";
        string target = cardPlay.Target?.Monster?.Id.Entry ?? cardPlay.Target?.Name ?? "none";
        return card
            + "@owner=" + owner
            + "@target=" + target
            + "@play=" + cardPlay.PlayIndex + "/" + cardPlay.PlayCount;
    }

    private static string TryDescribeChoiceContextModel(object?[] args)
    {
        PlayerChoiceContext? choiceContext = args.OfType<PlayerChoiceContext>().FirstOrDefault();
        AbstractModel? model = choiceContext?.LastInvolvedModel;
        if (model == null)
        {
            return string.Empty;
        }

        return (model.GetType().FullName ?? model.GetType().Name) + "[" + SafeDescribeModelId(model) + "]";
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

    /// <summary>
    /// Registers a missing SavedProperty name into the game's ModelIdSerializationCache so that
    /// replay / network serialization of a card that carries a property from a currently-unloaded
    /// mod does not abort combat-end (WriteReplay) or other serialization paths.
    /// Safe to call repeatedly; no-op when the name is already known or the cache is unavailable.
    /// </summary>
    public static void RegisterSavedPropertyNameIfMissing(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        try
        {
            var map = SavedPropertyNameToNetIdMapField?.GetValue(null) as Dictionary<string, int>;
            var list = SavedPropertyNetIdToNameMapField?.GetValue(null) as List<string>;
            if (map == null || list == null || map.ContainsKey(name))
            {
                return;
            }

            map[name] = list.Count;
            list.Add(name);

            int bitSize = list.Count > 1
                ? Mathf.CeilToInt(Mathf.Log(list.Count) / Mathf.Log(2f))
                : 0;
            SavedPropertyIdBitSizeProperty?.SetValue(null, bitSize);

            Log.Warn(
                "[" + LogTag + "] registered missing SavedProperty name into ModelIdSerializationCache: "
                + name + " (netId=" + (list.Count - 1) + ", bitSize=" + bitSize + ")");
        }
        catch (Exception exception)
        {
            Log.Warn(
                "[" + LogTag + "] failed to register missing SavedProperty name "
                + name + " exception=" + exception.GetType().Name + ": " + exception.Message);
        }
    }

    private static bool IsSavedPropertyNetIdMappingException(Exception exception) =>
        exception is ArgumentException
        && exception.Message?.Contains(
            "could not be mapped to any net ID",
            StringComparison.Ordinal) == true
        && exception.StackTrace?.Contains(
            "ModelIdSerializationCache.GetNetIdForPropertyName",
            StringComparison.Ordinal) == true;

    private static string? TryResolveMissingSavedPropertyName(Exception exception)
    {
        const string prefix = "SavedProperty name ";
        const string suffix = " could not be mapped to any net ID";

        string message = exception.Message ?? string.Empty;
        int start = message.IndexOf(prefix, StringComparison.Ordinal);
        if (start < 0)
        {
            return null;
        }

        start += prefix.Length;
        int end = message.IndexOf(suffix, start, StringComparison.Ordinal);
        if (end < 0)
        {
            end = message.Length;
        }

        string name = message.Substring(start, end - start).Trim();
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }

    /// <summary>
    /// Defensive fallback for the rare case a SavedProperty net-ID mapping failure still escapes
    /// (e.g. serialization path not covered by the WritePropertyName prefix). Registers the missing
    /// name so later serialization (save/replay) succeeds and keeps a clear recovery summary.
    /// </summary>
    private static void TryRecoverMissingSavedPropertyNetIds(
        Exception exception,
        CombatRecoveryReport recoveryReport)
    {
        if (!IsSavedPropertyNetIdMappingException(exception))
        {
            return;
        }

        string? name = TryResolveMissingSavedPropertyName(exception);
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        RegisterSavedPropertyNameIfMissing(name);
        recoveryReport.Add("注册缺失的 SavedProperty 属性名到序列化缓存：" + name);
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

    private sealed record CombatSafetyGuard(
        string Id,
        string Reason,
        int Priority,
        Func<CombatSafetyContext, Exception?, Task<bool>> RecoverAsync);

    private readonly record struct GuardCounterKey(int CombatStateHash, string Id);

    private readonly record struct GuardCounterState(int Count, string LastBeat);

    private sealed class DeadMonsterAttackAbortedException(Creature attacker) : Exception
    {
        public Creature Attacker { get; } = attacker;
    }
}
