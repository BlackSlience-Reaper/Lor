using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using LibraryOfRuina.content.specialguests.Iori;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.core.settings;
using LibraryOfRuina.infra.lifecycle;
using LibraryOfRuina.infra.patching;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace LibraryOfRuina.content.specialguests;

internal static class SpecialGuestRunCleanup
{
    /// <summary>由 <see cref="LibraryOfRuina.patches.dispatch.RunLifecycle"/> 在局结束清理时调用。</summary>
    internal static void OnRunCleaningUp()
    {
        SpecialGuestStoryPlayer.AbortActiveStory();
        StoryReadySynchronizer.Reset();
        SpecialGuestTerminalRewardsProceedPatch.Reset();
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyNextEvent))]
[HarmonyPriority(Priority.Last)]
[LibraryPatch(Reason = "第一次抽中嘉宾时载体还不存在（在这里创建），ModifyNextEvent 也没有 runState 参数，载体覆写接不住；必须以 Last 看到含其他模组修改的最终事件，否则消耗标记与实际事件脱节。只在怪物扩展开启时按种子确定性替换。")]
internal static class SpecialGuestModifyNextEventPatch
{
    [HarmonyPostfix]
    private static void Postfix(IRunState runState, ref EventModel __result)
    {
        if (LibraryRunSettings.IsMonsterExtensionEnabled(runState))
        {
            __result = SpecialGuestRegistry.TryReplaceNextEvent(runState, __result);
        }
    }
}

[HarmonyPatch(typeof(RunState), nameof(RunState.FromSerializable))]
[HarmonyPriority(Priority.First)]
internal static class SpecialGuestRunStateLoadPatch
{
    private static string? _rollbackGuestId;

    [HarmonyPrefix]
    private static void Prefix(SerializableRun save)
    {
        if (!LibraryRunSettings.IsMonsterExtensionEnabled(save))
        {
            return;
        }

        SpecialGuestRoomPersistence.RepairMissingActiveEventRoom(save);
        _rollbackGuestId = TryRollbackMidReceptionSave(save);
    }

    [HarmonyPostfix]
    private static void Postfix(RunState __result)
    {
        if (!LibraryRunSettings.IsMonsterExtensionEnabled(__result))
        {
            return;
        }

        string? rollbackGuestId = Interlocked.Exchange(ref _rollbackGuestId, null);
        SpecialGuestRunStateModifier? state =
            SpecialGuestRunStateModifier.TryGet(__result);
        if (rollbackGuestId != null)
        {
            state ??= SpecialGuestRunStateModifier.GetOrCreate(__result);
            state.ClearActiveGuest();
            for (int stage = 0; stage < 8; stage++)
            {
                state.SetValue(
                    $"stage-proceed.{rollbackGuestId}.{stage}",
                    null);
            }

            Log.Info(
                "[SpecialGuest] Reset in-flight reception state for "
                + rollbackGuestId
                + "; the loaded run returns to the event start.");
        }

        SpecialGuestRegistry.EnsureUnlocks(__result, state);
    }

    /// <summary>
    /// A save written while the Iori reception is in flight can restore into a
    /// synthetic finished-combat replay whose automatic proceed chain can hang,
    /// or carry no checkpoint room at all when a third-party save overwrote it
    /// mid-combat.  Rewrite both shapes to the parent EventRoom so loading
    /// returns to the event start.
    /// Iori-gated: Xiao and Kali keep the terminal-reward resume flow.
    /// </summary>
    private static string? TryRollbackMidReceptionSave(SerializableRun save)
    {
        ISpecialGuestEncounterStage? stage;
        ModelId? eventId;

        if (save.PreFinishedRoom is
            {
                IsPreFinished: true,
                ParentEventId: { } parentEventId,
            })
        {
            stage = TryResolveStageEncounter(save.PreFinishedRoom.EncounterId);
            eventId = parentEventId;
            // A pre-finished final-stage room is the legitimate post-victory
            // checkpoint; keep its resume flow.
            if (stage is not
                {
                    SpecialGuestId: IoriSpecialGuestIds.Guest,
                }
                || !SpecialGuestRegistry.TryGet(
                    stage.SpecialGuestId,
                    out SpecialGuestDefinition? guest)
                || stage.SpecialGuestStageIndex + 1 >= guest.Stages.Count)
            {
                return null;
            }
        }
        else if (save.PreFinishedRoom == null)
        {
            // No checkpoint room at all: rebuild the parent event from the
            // latest map-history room.  Post-victory saves always carry a
            // pre-finished room, so this branch only matches in-flight
            // receptions and needs no final-stage guard.
            if (SpecialGuestRoomPersistence.TryResolveLatestStageRoom(save)
                    is not ({ } historyStage, { } historyEventId)
                || historyStage.SpecialGuestId != IoriSpecialGuestIds.Guest)
            {
                return null;
            }

            stage = historyStage;
            eventId = historyEventId;
        }
        else
        {
            return null;
        }

        save.PreFinishedRoom = new SerializableRoom
        {
            RoomType = RoomType.Event,
            EventId = eventId,
            IsPreFinished = false,
        };
        Log.Warn(
            "[SpecialGuest] Mid-reception checkpoint rolled back to event "
            + eventId.Entry
            + "; the loaded run returns to the event start.");
        return stage.SpecialGuestId;
    }

    private static ISpecialGuestEncounterStage? TryResolveStageEncounter(
        ModelId? encounterId)
    {
        if (encounterId == null)
        {
            return null;
        }

        try
        {
            return ModelDb.GetByIdOrNull<EncounterModel>(encounterId)
                as ISpecialGuestEncounterStage;
        }
        catch (Exception)
        {
            return null;
        }
    }
}

/// <summary>
/// Let the native terminal-reward logic pop stage 1, resume its parent event,
/// and reset EventCombatSynchronizer first.  Only then start the next stage.
/// A persisted transition token makes duplicate UI/controller calls harmless.
/// </summary>
[HarmonyPatch(typeof(RunManager), nameof(RunManager.ProceedFromTerminalRewardsScreen))]
[HarmonyPriority(Priority.Last)]
[LibraryPatch(Reason = "ProceedFromTerminalRewardsScreen 无 Hook；仅在本模组特邀嘉宾阶段衔接中吞掉重复/过期的继续调用，其余放行原版以复用其出栈与父事件恢复。")]
internal static class SpecialGuestTerminalRewardsProceedPatch
{
    private static readonly ConditionalWeakTable<CombatRoom, ProceedGate> Gates = new();
    private static readonly object ActiveGateSync = new();
    private static Task? _activeProceedTask;
    private static int _suppressParentFadeIn;

    internal static bool ShouldSuppressParentFadeIn =>
        Volatile.Read(ref _suppressParentFadeIn) > 0;

    internal static void Reset()
    {
        lock (ActiveGateSync)
        {
            _activeProceedTask = null;
        }

        Volatile.Write(ref _suppressParentFadeIn, 0);
        Gates.Clear();
    }

    [HarmonyPrefix]
    private static bool Prefix(RunManager __instance, ref Task __result, out ProceedGate? __state)
    {
        __state = null;
        lock (ActiveGateSync)
        {
            if (_activeProceedTask is { IsCompleted: false } active)
            {
                __result = active;
                return false;
            }

            _activeProceedTask = null;
        }

        if (CurrentRun.Of(__instance) is RunState eventRunState
            && eventRunState.CurrentRoom is EventRoom { CanonicalEvent: SpecialGuestEventBase }
            && SpecialGuestRunStateModifier.TryGet(eventRunState) is { ActiveGuestId: { } activeGuestId } eventState
            && eventState.CurrentStageIndex > 0
            && string.Equals(
                eventState.GetValue($"stage-proceed.{activeGuestId}.{eventState.CurrentStageIndex - 1}"),
                "started",
                StringComparison.Ordinal))
        {
            // The native stage-one proceed has resumed the parent model but the
            // asynchronous stage-two room push has not completed yet.
            __result = Task.CompletedTask;
            return false;
        }

        if (CurrentRun.Of(__instance) is not RunState runState
            || runState.CurrentRoom is not CombatRoom { Encounter: ISpecialGuestEncounterStage completed } combatRoom
            || !SpecialGuestRegistry.TryGet(completed.SpecialGuestId, out SpecialGuestDefinition? guest)
            || SpecialGuestRunStateModifier.TryGet(runState) is not { } state)
        {
            return true;
        }

        if (!combatRoom.IsPreFinished)
        {
            // A second click/message from the stage-one rewards UI can arrive
            // after stage two has already become the current room.  Proceed is
            // never legitimate during active combat, so consume that stale call.
            string previousToken = $"stage-proceed.{completed.SpecialGuestId}.{completed.SpecialGuestStageIndex - 1}";
            if (completed.SpecialGuestStageIndex > 0
                && string.Equals(state.GetValue(previousToken), "started", StringComparison.Ordinal))
            {
                __result = Task.CompletedTask;
                return false;
            }

            return true;
        }

        if (completed.SpecialGuestStageIndex + 1 >= guest.Stages.Count)
        {
            return true;
        }

        int nextStage = completed.SpecialGuestStageIndex + 1;
        string tokenKey = $"stage-proceed.{completed.SpecialGuestId}.{completed.SpecialGuestStageIndex}";
        if (string.Equals(state.GetValue(tokenKey), "started", StringComparison.Ordinal))
        {
            __result = Task.CompletedTask;
            return false;
        }

        if (!Gates.TryGetValue((CombatRoom)runState.CurrentRoom, out ProceedGate? gate))
        {
            gate = new ProceedGate(combatRoom, new PendingStage(completed.SpecialGuestId, nextStage, tokenKey));
            Gates.Add((CombatRoom)runState.CurrentRoom, gate);
        }

        if (gate.HasStarted)
        {
            __result = gate.Completion.Task;
            return false;
        }

        gate.HasStarted = true;
        state.SetValue(tokenKey, "pending");
        Interlocked.Increment(ref _suppressParentFadeIn);
        lock (ActiveGateSync)
        {
            _activeProceedTask = gate.Completion.Task;
        }
        __state = gate;
        return true;
    }

    [HarmonyPostfix]
    private static void Postfix(RunManager __instance, ProceedGate? __state, ref Task __result)
    {
        if (__state != null)
        {
            __result = BeginPendingStageAfterNativeProceed(__instance, __result, __state);
        }
    }

    private static async Task BeginPendingStageAfterNativeProceed(
        RunManager manager,
        Task nativeProceed,
        ProceedGate gate)
    {
        PendingStage pending = gate.Pending;
        bool suppressionHeld = true;
        try
        {
            await nativeProceed;
            ReleaseFadeSuppression();
            suppressionHeld = false;
            if (CurrentRun.Of(manager) is not RunState runState
                || SpecialGuestRunStateModifier.TryGet(runState) is not { } state)
            {
                gate.Completion.TrySetResult();
                return;
            }

            if (runState.CurrentRoom is not EventRoom
                || manager.EventSynchronizer.GetLocalEvent() is not SpecialGuestEventBase)
            {
                state.SetValue(pending.TokenKey, null);
                Log.Error($"[SpecialGuest] Native stage proceed did not resume parent event for {pending.GuestId}.");
                await RestoreParentEventAsync(manager);
                gate.HasStarted = false;
                Gates.Remove(gate.SourceRoom);
                gate.Completion.TrySetResult();
                return;
            }

            state.AdvanceToStage(pending.StageIndex);
            if (NEventRoom.Instance is { } parentEventNode)
            {
                // Native Resume creates this node before its FadeIn.  The FadeIn
                // was suppressed above; hide the node until stage two replaces it
                // so the initial options page never flashes for one frame.
                parentEventNode.Visible = false;
            }
            GameApi.ResetEventCombat(manager.EventSynchronizer);
            foreach (SpecialGuestEventBase specialEvent in manager.EventSynchronizer.Events
                         .OfType<SpecialGuestEventBase>())
            {
                specialEvent.BeginPendingStage(pending.StageIndex);
            }

            state.SetValue(pending.TokenKey, "started");
            gate.Completion.TrySetResult();
        }
        catch (Exception exception)
        {
            if (suppressionHeld)
            {
                ReleaseFadeSuppression();
                suppressionHeld = false;
            }

            if (CurrentRun.Of(manager) is { } runState
                && SpecialGuestRunStateModifier.TryGet(runState) is { } state)
            {
                state.SetValue(pending.TokenKey, null);
            }

            if (CurrentRun.Of(manager) is RunState currentRunState
                && currentRunState.CurrentRoom is EventRoom)
            {
                try
                {
                    // If stage creation failed after the native parent FadeIn
                    // was suppressed, never leave the player on an invisible
                    // event room.  Restoring visibility also keeps the retry
                    // path usable after the transition token is cleared.
                    await RestoreParentEventAsync(manager);
                }
                catch (Exception restoreException)
                {
                    Log.Error(
                        "[SpecialGuest] Failed to restore the parent event after stage transition failure: "
                        + restoreException);
                }
            }

            gate.Completion.TrySetException(exception);
            gate.HasStarted = false;
            Gates.Remove(gate.SourceRoom);
            throw;
        }
        finally
        {
            if (suppressionHeld)
            {
                ReleaseFadeSuppression();
            }

            lock (ActiveGateSync)
            {
                if (ReferenceEquals(_activeProceedTask, gate.Completion.Task))
                {
                    _activeProceedTask = null;
                }
            }
        }
    }

    private static void ReleaseFadeSuppression()
    {
        int next = Interlocked.Decrement(ref _suppressParentFadeIn);
        if (next < 0)
        {
            Volatile.Write(ref _suppressParentFadeIn, 0);
        }
    }

    private static async Task RestoreParentEventAsync(RunManager manager)
    {
        if (NEventRoom.Instance is { } parentEventNode)
        {
            parentEventNode.Visible = true;
        }

        await RunManagerCompat.FadeIn(manager, showTransition: true);
    }

    private sealed record PendingStage(string GuestId, int StageIndex, string TokenKey);

    private sealed class ProceedGate(CombatRoom sourceRoom, PendingStage pending)
    {
        public CombatRoom SourceRoom { get; } = sourceRoom;

        public PendingStage Pending { get; } = pending;

        public TaskCompletionSource Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool HasStarted { get; set; }
    }
}

[HarmonyPatch(typeof(RunManager), "FadeIn", typeof(bool))]
[LibraryPatch(Reason = "RunManager.FadeIn 公开非虚且无 Hook；仅在本模组特邀嘉宾阶段衔接、当前房间为嘉宾父事件时抑制中间淡入，避免父事件闪现。")]
internal static class SpecialGuestSuppressIntermediateParentFadeInPatch
{
    [HarmonyPrefix]
    private static bool Prefix(ref Task __result)
    {
        // 只在特邀嘉宾阶段衔接、且原版已把房间恢复成嘉宾父事件时吞掉淡入；窗口内其他代码的 FadeIn 照常执行。
        if (!SpecialGuestTerminalRewardsProceedPatch.ShouldSuppressParentFadeIn
            || CurrentRun.State?.CurrentRoom is not EventRoom { CanonicalEvent: SpecialGuestEventBase })
        {
            return true;
        }

        __result = Task.CompletedTask;
        return false;
    }
}

[HarmonyPatch(typeof(Player), nameof(Player.AddRelicInternal))]
internal static class SpecialGuestRelicAddedPatch
{
    [HarmonyPostfix]
    private static void Postfix(Player __instance)
    {
        if (LibraryRunSettings.IsMonsterExtensionEnabled(__instance.RunState))
        {
            SpecialGuestRegistry.EnsureUnlocks(__instance.RunState);
        }
    }
}

[HarmonyPatch(typeof(Player), nameof(Player.RemoveRelicInternal))]
internal static class SpecialGuestRelicRemovedPatch
{
    [HarmonyPostfix]
    private static void Postfix(Player __instance)
    {
        if (LibraryRunSettings.IsMonsterExtensionEnabled(__instance.RunState))
        {
            SpecialGuestRegistry.EnsureUnlocks(__instance.RunState);
        }
    }
}

/// <summary>
/// 嘉宾战的剧情、阶段推进与奖励增补，由 <see cref="SpecialGuestRunStateModifier"/> 的钩子覆写调用。
/// 胜利后剧情在原版等待的 AfterCombatVictory 里播放：所有已连接玩家放行之前，战斗不会标记结束，奖励也不会存档。
/// </summary>
public static class SpecialGuestStageFlow
{
    public static async Task BeforeCombatStartAsync(IRunState runState)
    {
        if (!TryResolve(runState, out SpecialGuestDefinition guest, out SpecialGuestStageDefinition stage,
                out SpecialGuestRunStateModifier state, out CombatRoom room))
        {
            return;
        }

        ISpecialGuestEncounterStage encounterStage = (ISpecialGuestEncounterStage)room.Encounter;
        state.AdvanceToStage(encounterStage.SpecialGuestStageIndex);
        SpecialGuestStageContext context = new(runState, room, guest, stage, state);
        PrepareStoryIfNeeded(state, stage.BeforeCombatStory);
        if (stage.BeforeCombatStorySetup != null)
        {
            await stage.BeforeCombatStorySetup(context);
        }

        if (stage.BeforeCombatStory is { HasBackgroundMusic: false } beforeStory
            && !state.IsStoryCompleted(beforeStory.Id)
            && beforeStory.Lines.FirstOrDefault()?.BgmCue?.Action
                != SpecialGuestStoryBgmAction.FadeOut)
        {
            NRunMusicController.Instance?.StopMusic();
        }

        await PlayStoryIfNeededAsync(
            state,
            stage.BeforeCombatStory);

        if (stage.AfterCombatStoryRelease != null)
        {
            await stage.AfterCombatStoryRelease(context);
        }
    }

    public static async Task AfterCombatVictoryAsync(IRunState runState, CombatRoom room)
    {
        if (!TryResolve(runState, room, out SpecialGuestDefinition guest, out SpecialGuestStageDefinition stage,
                out SpecialGuestRunStateModifier state))
        {
            return;
        }

        if (stage.AfterVictoryStory == null
            && stage.BeforeVictoryStorySetup == null
            && stage.AfterVictoryStoryRelease == null)
        {
            state.MarkStageCompleted(((ISpecialGuestEncounterStage)room.Encounter).SpecialGuestStageIndex);
            return;
        }

        if (stage.AfterVictoryStory is { HasBackgroundMusic: false })
        {
            NRunMusicController.Instance?.StopMusic();
        }
        SpecialGuestStageContext context = new(runState, room, guest, stage, state);
        PrepareStoryIfNeeded(state, stage.AfterVictoryStory);
        if (stage.BeforeVictoryStorySetup != null)
        {
            await stage.BeforeVictoryStorySetup(context);
        }

        await PlayStoryIfNeededAsync(
            state,
            stage.AfterVictoryStory);

        if (stage.AfterVictoryStoryRelease != null)
        {
            await stage.AfterVictoryStoryRelease(context);
        }

        state.MarkStageCompleted(((ISpecialGuestEncounterStage)room.Encounter).SpecialGuestStageIndex);
    }

    public static async Task AugmentRewardsAsync(IRunState runState, CombatRoom room, RewardsSet rewards)
    {
        if (!TryResolve(runState, room, out SpecialGuestDefinition guest, out SpecialGuestStageDefinition stage,
                out SpecialGuestRunStateModifier state)
            || stage.RewardAugmenter == null)
        {
            return;
        }

        await stage.RewardAugmenter.AugmentAsync(
            new SpecialGuestStageContext(runState, room, guest, stage, state),
            rewards);
    }

    private static async Task PlayStoryIfNeededAsync(
        SpecialGuestRunStateModifier state,
        SpecialGuestStorySequence? story)
    {
        if (story == null || state.IsStoryCompleted(story.Id))
        {
            return;
        }

        StoryReadySynchronizer.Prepare(story.Id);
        try
        {
            await SpecialGuestStoryPlayer.PlayAsync(story);
        }
        finally
        {
            // A missing texture, audio stream, or exiting node completes the
            // local reader.  It must never strand the multiplayer barrier.
            await StoryReadySynchronizer.WaitForReleaseAsync(story.Id);
        }

        state.MarkStoryCompleted(story.Id);
    }

    private static void PrepareStoryIfNeeded(
        SpecialGuestRunStateModifier state,
        SpecialGuestStorySequence? story)
    {
        if (story != null && !state.IsStoryCompleted(story.Id))
        {
            // Register before guest-specific setup performs any awaited work.
            StoryReadySynchronizer.Prepare(story.Id);
        }
    }

    private static bool TryResolve(
        IRunState runState,
        out SpecialGuestDefinition guest,
        out SpecialGuestStageDefinition stage,
        out SpecialGuestRunStateModifier state,
        out CombatRoom room)
    {
        guest = null!;
        stage = null!;
        state = null!;
        room = (runState as RunState)?.CurrentRoom as CombatRoom
            ?? null!;
        return room != null && TryResolve(runState, room, out guest, out stage, out state);
    }

    private static bool TryResolve(
        IRunState runState,
        CombatRoom room,
        out SpecialGuestDefinition guest,
        out SpecialGuestStageDefinition stage,
        out SpecialGuestRunStateModifier state)
    {
        guest = null!;
        stage = null!;
        state = null!;
        if (room.Encounter is not ISpecialGuestEncounterStage encounterStage
            || !SpecialGuestRegistry.TryGet(encounterStage.SpecialGuestId, out guest)
            || (uint)encounterStage.SpecialGuestStageIndex >= (uint)guest.Stages.Count
            || SpecialGuestRunStateModifier.TryGet(runState) is not { } carrier)
        {
            return false;
        }

        state = carrier;
        stage = guest.GetStage(encounterStage.SpecialGuestStageIndex);
        return true;
    }
}
