using System;
using System.Threading.Tasks;
using LibraryOfRuina.infra.lifecycle;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.content.specialguests;

/// <summary>
/// Reusable three-option event page and multi-stage event-combat router.
/// Concrete guests only provide a registry ID and their conventional event
/// title/portrait localization assets.
/// </summary>
public abstract class SpecialGuestEventBase : EventModel
{
    internal const string ReceptionStageCountVar = "ReceptionStageCount";
    private const string InitialOptionsRoot =
        "SPECIAL_GUEST_EVENT.pages.INITIAL.options.";
    private const int EscapeMaxHpLossPercent = 4;
    private const int DefaultEscapeGoldGain = 67;
    private const int DefaultEscapeGoldReward = 50;
    private const string EscapedResolution = "escaped";
    private const string CompletedResolution = "completed";

    protected abstract string GuestDefinitionId { get; }

    protected SpecialGuestDefinition Definition => SpecialGuestRegistry.Get(GuestDefinitionId);

    protected virtual string SecondOptionLocKey =>
        InitialOptionsRoot + "RECEIVE_WITH_LIBRARIAN_LOCKED";

    protected virtual string ThirdOptionLocKey =>
        InitialOptionsRoot + "ESCAPE";

    protected virtual int EscapeGoldGain => DefaultEscapeGoldGain;

    protected virtual int EscapeGoldReward => DefaultEscapeGoldReward;

    public sealed override bool IsShared => true;

    public sealed override EventLayoutType LayoutType => EventLayoutType.Default;

    public sealed override IEnumerable<LocString> GameInfoOptions
    {
        get
        {
            string[] optionLocKeys =
            [
                InitialOptionsRoot + "RECEIVE_ALONE",
                SecondOptionLocKey,
                ThirdOptionLocKey
            ];
            foreach (string optionLocKey in optionLocKeys)
            {
                foreach (string suffix in new[] { ".title", ".description" })
                {
                    LocString text = new(
                        "events",
                        optionLocKey + suffix);
                    DynamicVars.AddTo(text);
                    yield return text;
                }
            }
        }
    }

    public override LocString InitialDescription =>
        new("events", "SPECIAL_GUEST_EVENT.pages.INITIAL.description");
    
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new StringVar("GuestName"),
        new(ReceptionStageCountVar, 1m),
        new HpLossVar("EscapeMaxHpLossPercent", EscapeMaxHpLossPercent),
        new HpLossVar(0m),
        new GoldVar("GoldGain", EscapeGoldGain)
    ];

    
    public override bool IsAllowed(IRunState runState)
    {
        SpecialGuestRunStateModifier? state = SpecialGuestRunStateModifier.TryGet(runState);
        return state != null && state.IsUnlocked(GuestDefinitionId) && state.IsConsumed(GuestDefinitionId);
    }

    public override void CalculateVars()
    {
        SpecialGuestDefinition definition = Definition;
        ((StringVar)DynamicVars["GuestName"]).StringValue = definition.GuestName.GetFormattedText();
        DynamicVars[ReceptionStageCountVar].BaseValue =
            ResolveReceptionStageCount(definition.Stages.Count);
        var maxHp = Owner?.Creature.MaxHp ?? 0m;
        var hploss = maxHp * EscapeMaxHpLossPercent / 100;
        DynamicVars.HpLoss.BaseValue = hploss;
    }

    internal static int ResolveReceptionStageCount(int stageCount) =>
        Math.Max(1, stageCount);

    public override IEnumerable<string> GetAssetPaths(IRunState runState)
    {
        HashSet<string> paths = new(base.GetAssetPaths(runState), StringComparer.Ordinal);
        foreach (SpecialGuestStageDefinition stage in Definition.Stages)
        {
            paths.UnionWith(stage.GetAssetPaths());
        }

        return paths;
    }

    protected sealed override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        EventOption secondOption = CreateCustomSecondOption()
            ?? new EventOption(
                this,
                null,
                InitialOptionsRoot + "RECEIVE_WITH_LIBRARIAN_LOCKED", new HoverTip(
                    new LocString("events", "SPECIAL_GUEST_EVENT.locked.title"),
                    new LocString("events", "SPECIAL_GUEST_EVENT.locked.description")));
        EventOption thirdOption = CreateCustomThirdOption()
            ?? CreateOption(ThirdOptionLocKey, Escape)
                .ThatDecreasesMaxHp(DynamicVars.HpLoss.BaseValue);

        return
        [
            CreateOption(InitialOptionsRoot + "RECEIVE_ALONE", BeginFirstStage),
            secondOption,
            thirdOption
        ];
    }

    protected virtual EventOption? CreateCustomSecondOption() => null;

    protected virtual EventOption? CreateCustomThirdOption() => null;

    protected sealed override void SetInitialEventState(bool isPreFinished)
    {
        if (!isPreFinished
            || Owner?.RunState is not RunState runState
            || SpecialGuestRunStateModifier.TryGet(runState) is not { } state)
        {
            base.SetInitialEventState(isPreFinished);
            return;
        }

        string? resolution = state.GetValue("resolution." + GuestDefinitionId);
        if (string.Equals(resolution, EscapedResolution, StringComparison.Ordinal))
        {
            SetEventFinished(new LocString(
                "events",
                "SPECIAL_GUEST_EVENT.pages.ESCAPED.description"));
            return;
        }

        if (string.Equals(resolution, CompletedResolution, StringComparison.Ordinal))
        {
            SetEventFinished(new LocString(
                "events",
                Id.Entry + ".pages.COMPLETE.description"));
            return;
        }

        if (resolution != null
            && ResolveCustomResolutionDescription(resolution) is { } customDescription)
        {
            SetEventFinished(customDescription);
            return;
        }

        // A malformed or legacy room should stay playable instead of passing a
        // non-ancient pre-finished flag into the native implementation.
        base.SetInitialEventState(isPreFinished: false);
    }

    protected virtual LocString? ResolveCustomResolutionDescription(
        string resolution) => null;

    public sealed override async Task AfterEventStarted()
    {
        await base.AfterEventStarted();

        // ModifyNextEvent runs after the native map-entry checkpoint.  Preserve
        // the exact EventRoom after it is current; saving null here would save
        // post-roll RNG and make an Unknown node reroll on load.
        if (Owner?.RunState is RunState runState
            && ReferenceEquals(CurrentRun.State, runState)
            && SpecialGuestRunStateModifier.TryGet(runState) is not
            {
                ActiveGuestId: not null,
                CompletedStageIndex: >= 0,
            })
        {
            await SpecialGuestRoomPersistence.SaveCurrentEventRoomAsync(runState, Id);
        }
    }

    public sealed override async Task Resume(AbstractRoom exitedRoom)
    {
        if (exitedRoom is not CombatRoom { Encounter: ISpecialGuestEncounterStage completed }
            || !string.Equals(completed.SpecialGuestId, GuestDefinitionId, StringComparison.Ordinal)
            || Owner?.RunState is not RunState runState)
        {
            return;
        }

        SpecialGuestRunStateModifier state = SpecialGuestRunStateModifier.GetOrCreate(runState);
        int nextStage = completed.SpecialGuestStageIndex + 1;
        if (nextStage < Definition.Stages.Count)
        {
            // The terminal-reward redirect calls the original native proceed
            // first, which resumes this event and clears EventCombatSynchronizer.
            // Record the transition here; the redirect starts stage 2 only
            // after that native reset has completed.
            state.AdvanceToStage(nextStage);
            return;
        }

        SpecialGuestReceptionProgress.MarkSuccessfullyReceived(
            runState,
            GuestDefinitionId);
        state.SetValue("resolved." + GuestDefinitionId, "true");
        state.SetValue("resolution." + GuestDefinitionId, CompletedResolution);
        state.ClearActiveGuest();
        SetEventFinished(new LocString("events", Id.Entry + ".pages.COMPLETE.description"));
        if (Owner.NetId == RunManager.Instance.NetService.NetId
            && ReferenceEquals(CurrentRun.State, runState))
        {
            await SpecialGuestRoomPersistence.SaveFinishedCurrentEventRoomAsync(runState, Id);
        }
    }

    private Task BeginFirstStage()
    {
        if (Owner?.RunState is not RunState runState)
        {
            return Task.CompletedTask;
        }

        SpecialGuestRunStateModifier state = SpecialGuestRunStateModifier.GetOrCreate(runState);
        if (!string.Equals(state.ActiveGuestId, GuestDefinitionId, StringComparison.Ordinal))
        {
            state.BeginGuest(GuestDefinitionId);
        }

        EnterCombatWithoutExitingEvent(
            Definition.GetStage(0).EncounterFactory(),
            Array.Empty<Reward>(),
            shouldResumeAfterCombat: true);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called exclusively by the idempotent terminal-reward redirect after the
    /// native resume path has reset the event-combat synchronizer.
    /// </summary>
    internal void BeginPendingStage(int stageIndex)
    {
        EnterCombatWithoutExitingEvent(
            Definition.GetStage(stageIndex).EncounterFactory(),
            Array.Empty<Reward>(),
            shouldResumeAfterCombat: true);
    }

    private async Task Escape()
    {
        if (Owner is { } owner)
        {
            int maxHpLoss = ResolveEscapeMaxHpLoss();
            if (maxHpLoss > 0)
            {
                await CreatureCmd.LoseMaxHp(
                    new ThrowingPlayerChoiceContext(),
                    owner.Creature,
                    maxHpLoss,
                    isFromCard: false);
            }

            await PlayerCmd.GainGold(
                EscapeGoldReward,
                owner);
        }

        await FinishWithoutReceptionAsync(
            EscapedResolution,
            new LocString("events", "SPECIAL_GUEST_EVENT.pages.ESCAPED.description"));
    }

    protected async Task FinishWithoutReceptionAsync(
        string resolution,
        LocString finishedDescription)
    {
        if (string.IsNullOrWhiteSpace(resolution))
        {
            throw new ArgumentException(
                "A special-guest alternate resolution must have a stable ID.",
                nameof(resolution));
        }

        if (Owner?.RunState is RunState runState)
        {
            SpecialGuestRunStateModifier state = SpecialGuestRunStateModifier.GetOrCreate(runState);
            state.SetValue("resolved." + GuestDefinitionId, "true");
            state.SetValue("resolution." + GuestDefinitionId, resolution);
            state.ClearActiveGuest();
        }

        SetEventFinished(finishedDescription);

        // A shared event executes this handler once per player model on every
        // peer.  Only the locally-owned model awaits the checkpoint; SaveManager
        // itself writes only in singleplayer or on the host.
        if (Owner?.RunState is RunState ownerRunState
            && Owner.NetId == RunManager.Instance.NetService.NetId
            && ReferenceEquals(CurrentRun.State, ownerRunState))
        {
            await SpecialGuestRoomPersistence.SaveFinishedCurrentEventRoomAsync(ownerRunState, Id);
        }
    }

    private int ResolveEscapeMaxHpLoss() =>
        Owner?.Creature is { MaxHp: > 0 } creature
            ? Math.Max(
                1,
                (int)decimal.Ceiling(
                    creature.MaxHp * EscapeMaxHpLossPercent / 100m))
            : 0;

    protected EventOption CreateOption(
        string optionLocKey,
        Func<Task>? action,
        IEnumerable<IHoverTip>? hoverTips = null) =>
        new(
            this,
            action,
            optionLocKey,
            hoverTips ?? Array.Empty<IHoverTip>());
}
