using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using LibraryOfRuina.compat;
using LibraryOfRuina.features.settings;
using LibraryOfRuina.specialguests.Iori;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.sts2.Core.Nodes.TopBar;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace LibraryOfRuina.specialguests;

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
internal static class SpecialGuestModifyNextEventPatch
{
    [HarmonyPostfix]
    private static void Postfix(IRunState runState, ref EventModel __result)
    {
        if (LibraryOfRuinaSettings.MonsterExtensionEnabled)
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
        if (!LibraryOfRuinaSettings.MonsterExtensionEnabled)
        {
            return;
        }

        SpecialGuestRoomPersistence.RepairMissingActiveEventRoom(save);
        _rollbackGuestId = TryRollbackMidReceptionSave(save);
    }

    [HarmonyPostfix]
    private static void Postfix(RunState __result)
    {
        if (!LibraryOfRuinaSettings.MonsterExtensionEnabled)
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

        if (__instance.DebugOnlyGetState() is RunState eventRunState
            && eventRunState.CurrentRoom is EventRoom
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

        if (__instance.DebugOnlyGetState() is not RunState runState
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
            if (manager.DebugOnlyGetState() is not RunState runState
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
            manager.EventSynchronizer.BeforeExitingRoom();
            EventModel localEvent = manager.EventSynchronizer.GetLocalEvent();
            manager.EventSynchronizer.GenerateInternalCombatStateIfNecessary(localEvent);
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

            if (manager.DebugOnlyGetState() is { } runState
                && SpecialGuestRunStateModifier.TryGet(runState) is { } state)
            {
                state.SetValue(pending.TokenKey, null);
            }

            if (manager.DebugOnlyGetState() is RunState currentRunState
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
internal static class SpecialGuestSuppressIntermediateParentFadeInPatch
{
    [HarmonyPrefix]
    private static bool Prefix(ref Task __result)
    {
        if (!SpecialGuestTerminalRewardsProceedPatch.ShouldSuppressParentFadeIn)
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
        if (LibraryOfRuinaSettings.MonsterExtensionEnabled)
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
        if (LibraryOfRuinaSettings.MonsterExtensionEnabled)
        {
            SpecialGuestRegistry.EnsureUnlocks(__instance.RunState);
        }
    }
}

[HarmonyPatch(typeof(NTopBarModifier), nameof(NTopBarModifier.Create))]
internal static class SpecialGuestHideRunStateTopBarPatch
{
    [HarmonyPrefix]
    private static bool Prefix(ModifierModel modifier, ref NTopBarModifier? __result)
    {
        if (modifier is not SpecialGuestRunStateModifier)
        {
            return true;
        }

        __result = null;
        return false;
    }
}

[HarmonyPatch(typeof(NTopBar), nameof(NTopBar.Initialize))]
internal static class SpecialGuestHideEmptyModifierContainerPatch
{
    private static readonly FieldInfo? ModifiersContainerField =
        AccessTools.Field(typeof(NTopBar), "_modifiersContainer");

    [HarmonyPostfix]
    private static void Postfix(NTopBar __instance, IRunState runState)
    {
        if (!runState.Modifiers.Any(static modifier => modifier is SpecialGuestRunStateModifier)
            || ModifiersContainerField?.GetValue(__instance) is not Control container)
        {
            return;
        }

        bool hasVisibleChildren = container.GetChildren()
            .OfType<Control>()
            .Any(static child => GodotObject.IsInstanceValid(child));
        if (!hasVisibleChildren)
        {
            container.Visible = false;
        }
    }
}

/// <summary>
/// Plays an after-victory story inside the native awaited victory hook.  The
/// combat is not marked pre-finished and rewards are not saved until every
/// connected player has released the barrier.
/// </summary>
[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCombatVictory))]
internal static class SpecialGuestAfterCombatVictoryPatch
{
    [HarmonyPostfix]
    private static void Postfix(IRunState runState, CombatRoom room, ref Task __result)
    {
        __result = Wrap(__result, runState, room);
    }

    private static async Task Wrap(Task original, IRunState runState, CombatRoom room)
    {
        await original;
        await SpecialGuestStageFlow.AfterCombatVictoryAsync(runState, room);
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.BeforeCombatStart))]
internal static class SpecialGuestBeforeCombatStartPatch
{
    [HarmonyPostfix]
    private static void Postfix(IRunState runState, ref Task __result)
    {
        __result = Wrap(__result, runState);
    }

    private static async Task Wrap(Task original, IRunState runState)
    {
        await original;
        await SpecialGuestStageFlow.BeforeCombatStartAsync(runState);
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.BeforeCombatRewardOffered))]
internal static class SpecialGuestRewardAugmentPatch
{
    [HarmonyPostfix]
    private static void Postfix(RewardsSet rewards, IRunState runState, CombatRoom room, ref Task __result)
    {
        __result = Wrap(__result, rewards, runState, room);
    }

    private static async Task Wrap(Task original, RewardsSet rewards, IRunState runState, CombatRoom room)
    {
        await original;
        await SpecialGuestStageFlow.AugmentRewardsAsync(runState, room, rewards);
    }
}

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
