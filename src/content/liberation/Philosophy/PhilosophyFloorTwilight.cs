using LibraryLib.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.features.secondascension;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.monsters;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.infra.helpers;
using LibraryLib.Entities.Creatures;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.liberation.Philosophy;

public enum PhilosophyFloorTwilightEgg
{
    None,
    BigEyes,
    SmallBeak,
    LongArms
}

public enum PhilosophyFloorTwilightMode
{
    Surveillance,
    Punishment,
    SinTrace,
    Judgment,
    EndOne,
    EndTwo,
    EndThree,
    Other
}

public enum PhilosophyFloorTwilightAction
{
    SlamDown,
    Talon,
    Prowl,
    ProtectBlackForest,
    TornMouth,
    TiltedScale,
    ForestLight,
    Punishment,
    BrilliantEyes,
    PeaceForAll,
    Surveillance,
    Judgment
}

[Flags]
public enum PhilosophyFloorTwilightBranchCounter
{
    None = 0,
    Judgment = 1 << 0,
    SinTrace = 1 << 1,
    Punishment = 1 << 2
}

internal sealed class PhilosophyFloorTwilightPersistentState
{
    // Monster and power models are not SavedProperties save holders. The
    // encounter owns this snapshot and serializes it through EncounterState.
    internal int AliveEggMask { get; init; } =
        PhilosophyFloorTwilight.AllEggMask;

    internal PhilosophyFloorTwilightEgg ActiveEgg { get; init; } =
        PhilosophyFloorTwilightEgg.BigEyes;

    internal PhilosophyFloorTwilightMode PlannedMode { get; init; }

    internal bool HasPlannedMode { get; init; }

    internal int ModeCycleStep { get; init; }

    internal int JudgmentBranchEntries { get; init; }

    internal int SinTraceBranchEntries { get; init; }

    internal int PunishmentBranchEntries { get; init; }

    internal bool NextEndFallbackIsOne { get; init; } = true;

    internal PhilosophyFloorTwilightBranchCounter PlannedBranchCounter { get; init; }

    internal bool PlannedUsesEndFallback { get; init; }

    internal PhilosophyFloorTwilightAction PlannedOtherFirstAction { get; init; } = PhilosophyFloorTwilightAction.Talon;

    internal PhilosophyFloorTwilightAction PlannedOtherSecondAction { get; init; } = PhilosophyFloorTwilightAction.Talon;

    internal int LastEggScheduleRound { get; init; } = -1;

    internal bool BrokenEggRecoveryPending { get; init; }

    internal bool IntroCgPlayed { get; init; }

    internal int[] PlannedTargetCombatIds { get; init; } = [0, 0, 0, 0];

    internal int SmallBeakProcessedRound { get; init; } = -1;

    internal int[] SmallBeakProcessedPlayerCombatIds { get; init; } = [];
}

public sealed partial class PhilosophyFloorTwilight :
    CounterIntentMonsterModel,
    IEncounterDynamicBgmTrackSource, ILibraryAbstractModel
{
    /// <summary>死亡动画时长；不会移出战斗的死亡返回 0，见 <see cref="LayeredBossSpine.DeathLength"/>。</summary>
    public override float DeathAnimLengthOverride => LayeredBossSpine.DeathLength(this);

    internal const int AllEggMask = 0b111;
    private const string RouterStateId = "PHILOSOPHY_FLOOR_TWILIGHT_ROUTER";
    internal const int EggBreakHpLossPercent = 10;
    internal const int PermanentPowerGainPerEgg = 3;
    internal const int ModeCycleLength = 3;
    internal const int EggScheduleCycleLength = 6;
    internal const int EggScheduleStageLength = 2;

    private Dictionary<PhilosophyFloorTwilightMode, MoveState>
        _statesByMode = [];
    private List<PhilosophyFloorTwilightAction>
        _performedActionTrace = [];
    private AbstractIntent[]? _otherPlannedIntents;
    private MonsterState? _routerState;
    private bool _victoryCgPlayed;

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _statesByMode = [];
        _performedActionTrace = [.. _performedActionTrace];
        _otherPlannedIntents = null;
        _routerState = null;
    }

    public int AliveEggMask { get; private set; } = AllEggMask;

    public PhilosophyFloorTwilightEgg ActiveEgg { get; private set; } =
        PhilosophyFloorTwilightEgg.BigEyes;

    public PhilosophyFloorTwilightMode PlannedMode { get; private set; }

    public bool HasPlannedMode { get; private set; }

    public int ModeCycleStep { get; private set; }

    public int JudgmentBranchEntries { get; private set; }

    public int SinTraceBranchEntries { get; private set; }

    public int PunishmentBranchEntries { get; private set; }

    public bool NextEndFallbackIsOne { get; private set; } = true;

    public PhilosophyFloorTwilightBranchCounter PlannedBranchCounter { get; private set; }

    public bool PlannedUsesEndFallback { get; private set; }

    public PhilosophyFloorTwilightAction PlannedOtherFirstAction { get; private set; } = PhilosophyFloorTwilightAction.Talon;

    public PhilosophyFloorTwilightAction PlannedOtherSecondAction { get; private set; } = PhilosophyFloorTwilightAction.Talon;

    public int LastEggScheduleRound { get; private set; } = -1;

    public bool BrokenEggRecoveryPending { get; private set; }

    public bool IntroCgPlayed { get; private set; }

    internal IReadOnlyList<PhilosophyFloorTwilightAction>
        DebugPerformedActionTrace => _performedActionTrace;

    public int PlannedTargetOneCombatId { get; private set; }

    public int PlannedTargetTwoCombatId { get; private set; }

    public int PlannedTargetThreeCombatId { get; private set; }

    public int PlannedTargetFourCombatId { get; private set; }

    internal int SmallBeakProcessedRound { get; private set; } = -1;

    internal int[] SmallBeakProcessedPlayerCombatIds { get; private set; } = [];

    internal bool HasEnhancedBrokenEggs =>
        LibrarySecondAscensionState.HasLevel(
            LibrarySecondAscensionLevel.FinalReception,
            Creature.CombatState?.RunState);

    public int AliveEggCount => CountAliveEggs(AliveEggMask);

    public int DestroyedEggCount => 3 - CountAliveEggs(AliveEggMask);

    public int CurrentEncounterBgmTrackIndex =>
        Math.Clamp(DestroyedEggCount, 0, 2);

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.ToughEnemies,
            1400,
            1200);

    public override int MaxInitialHp => MinInitialHp;

    public override int DefaultChaoResistance => 200;

    // Doom plays its death VFX and removes the creature node before
    // CreatureCmd.Kill runs the death-prevention chain. While Eternal Peace
    // is active the kill is always prevented (HP is restored to the lock
    // threshold), so the node must not disappear in that pre-kill step.
    public override bool ShouldDisappearFromDoom =>
        !Creature.HasPower<PhilosophyFloorTwilightPeacePowerBase>();

    public override LibraryCreatureResistanceData.Resistance?
        DefaultPhysicalResistanceData => CreateUniformResistance(
            LibraryResistanceLevel.Immune);

    public override LibraryCreatureResistanceData.Resistance?
        DefaultChaoResistanceData => new()
        {
            Slash = LibraryResistanceLevel.Vulnerable,
            Pierce = LibraryResistanceLevel.Resist,
            Blunt = LibraryResistanceLevel.Resist
        };

    public override IEnumerable<string> AssetPaths =>
        PhilosophyFloorTwilightCreatureVisuals.Profile.AssetPaths
            .Concat(PhilosophyFloorLiberationVfx.AssetPaths)
            .Concat(PhilosophyFloorLiberationVfx.PowerIconPaths)
            .Concat(PhilosophyFloorLiberationCgController.AssetPaths)
            .Concat(EnumerateIntentAssets()
                .SelectMany(static intent => intent.AssetPaths))
            .Distinct();

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        EncounterBgmController.RegisterMonster(Creature);
        await PhilosophyFloorTwilightPowerController.EnsureBossPowers(this);
        if (Creature.CombatState is { } combatState)
        {
            int round = Math.Max(1, combatState.RoundNumber);
            if (LastEggScheduleRound < 0)
            {
                ActiveEgg = ResolveActiveEgg(round);
                LastEggScheduleRound = round;
            }

            await ApplyEggResistances(new ThrowingPlayerChoiceContext());
            await PhilosophyFloorTwilightPowerController.SyncPeacePower(this);
            await RemoveVisibleDebuffsIfLongArmsActive();
            PhilosophyFloorLiberationBackgroundController.SetEggState(
                ActiveEgg,
                AliveEggMask,
                flash: false);
        }
    }

    public override async Task BeforeCombatStart()
    {
        await base.BeforeCombatStart();
        PhilosophyFloorLiberationBackgroundController.SetEggState(
            ActiveEgg,
            AliveEggMask,
            flash: false);
        if (IntroCgPlayed)
        {
            return;
        }

        IntroCgPlayed = true;
        await PhilosophyFloorLiberationCgController.PlayIntroAsync();
    }

    public override void BeforeRemovedFromRoom()
    {
        base.BeforeRemovedFromRoom();
        PhilosophyFloorLiberationCgController.ResetPresentation();
    }

    public override async Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        await base.AfterDeath(
            choiceContext,
            creature,
            wasRemovalPrevented,
            deathAnimLength);
        if (creature != Creature
            || wasRemovalPrevented
            || _victoryCgPlayed)
        {
            return;
        }

        _victoryCgPlayed = true;
        // Awaited inside the AfterDeath hook chain: a CG failure must not skip later listeners.
        await PresentationGuard.RunAsync(
            () => PhilosophyFloorLiberationCgController.PlayVictoryAsync(),
            "PhilosophyFloorTwilight victory CG");
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        _statesByMode.Clear();
        _otherPlannedIntents = new AbstractIntent[3];
        RefreshOtherPlannedIntents();

        foreach (PhilosophyFloorTwilightMode mode in
                 Enum.GetValues<PhilosophyFloorTwilightMode>())
        {
            AbstractIntent[] intents = mode == PhilosophyFloorTwilightMode.Other
                ? _otherPlannedIntents
                : GetModeActions(mode)
                    .Select((action, slot) =>
                        CreateActionIntent(action, slot))
                    .ToArray();
            _statesByMode[mode] = new MoveState(
                GetModeStateId(mode),
                targets => PerformMode(mode, targets),
                intents);
        }

        var router = new DelegatingMonsterRouterState(
            RouterStateId,
            (_, rng) =>
            {
                if (!HasPlannedMode)
                {
                    PlanNextMode(rng);
                }

                RefreshOtherPlannedIntents();
                return _statesByMode[PlannedMode].Id;
            },
            shouldAppearInLogs: true);
        _routerState = router;
        foreach (MoveState state in _statesByMode.Values)
        {
            state.FollowUpState = router;
        }

        MonsterState initialState = HasPlannedMode
            ? _statesByMode[PlannedMode]
            : router;
        return new MonsterMoveStateMachine(
            _statesByMode.Values.Cast<MonsterState>().Append(router),
            initialState);
    }

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (side == CombatSide.Player && Creature.IsAlive)
        {
            if (BrokenEggRecoveryPending)
            {
                await RecoverFromBrokenEgg(choiceContext);
            }

            int round = Math.Max(1, combatState.RoundNumber);
            bool eggChanged = false;
            if (LastEggScheduleRound != round)
            {
                PhilosophyFloorTwilightEgg nextEgg = ResolveActiveEgg(round);
                eggChanged = nextEgg != ActiveEgg;
                ActiveEgg = nextEgg;
                LastEggScheduleRound = round;
            }

            await ApplyEggResistances(choiceContext);
            await PhilosophyFloorTwilightPowerController.SyncPeacePower(this);
            await RemoveVisibleDebuffsIfLongArmsActive();
            PresentationGuard.Run(
                () => PhilosophyFloorLiberationBackgroundController.SetEggState(
                    ActiveEgg,
                    AliveEggMask,
                    flash: eggChanged),
                "PhilosophyFloorTwilight egg state");
        }

        await base.BeforeSideTurnStart(
            choiceContext,
            side,
            participants,
            combatState);
    }

    public Task AfterCurrentChaoValueChanged(
        Creature target,
        decimal amount,
        LibraryDamageType type)
    {
        if (target != Creature
            || amount >= 0m
            || target is not LibraryCreature
            {
                CurrentChaoValue: <= 0,
                IsChaoed: true
            })
        {
            return Task.CompletedTask;
        }

        return BreakActiveEgg();
    }

    public Task AfterStun(Creature creature)
    {
        return creature == Creature
            ? BreakActiveEgg()
            : Task.CompletedTask;
    }

    private async Task BreakActiveEgg()
    {
        if (BrokenEggRecoveryPending
            || ActiveEgg == PhilosophyFloorTwilightEgg.None
            || !IsEggAlive(ActiveEgg))
        {
            return;
        }

        PhilosophyFloorTwilightEgg brokenEgg = ActiveEgg;
        AliveEggMask &= ~EggBit(brokenEgg);
        BrokenEggRecoveryPending = true;
        ActiveEgg = ResolveActiveEgg(
            Math.Max(1, Creature.CombatState?.RoundNumber ?? 1));
        ClearPlannedModeAfterStun();
        var choiceContext = new ThrowingPlayerChoiceContext();
        if (HasEnhancedBrokenEggs && brokenEgg == PhilosophyFloorTwilightEgg.BigEyes)
        {
            foreach (var player in Creature.CombatState!.Players)
            {
                foreach (PowerModel power in player.Creature.Powers
                    .Where(power => power.TypeForCurrentAmount == PowerType.Buff)
                    .ToArray())
                {
                    await PowerCmd.Remove(power);
                }
            }
        }

        if (HasEnhancedBrokenEggs && brokenEgg == PhilosophyFloorTwilightEgg.SmallBeak)
        {
            foreach (var player in Creature.CombatState!.Players)
            {
                foreach (var card in player.PlayerCombatState!.AllCards)
                {
                    card.InvokeEnergyCostChanged();
                }
            }
        }

        await ApplyEggResistances(choiceContext);
        await PhilosophyFloorTwilightPowerController.SyncPeacePower(this);
        await RemoveVisibleDebuffsIfLongArmsActive();
        PresentationGuard.Run(
            () => PhilosophyFloorLiberationBackgroundController.SetEggState(
                ActiveEgg,
                AliveEggMask,
                flash: true),
            "PhilosophyFloorTwilight egg state");
        // Started before the HP loss and awaited after it; guarded so neither a synchronous throw
        // nor a faulted task can skip the damage or surface into the AfterStun hook chain.
        Task eggBreakTask = PresentationGuard.RunAsync(
            () => PhilosophyFloorLiberationBackgroundController.PlayEggBreak(brokenEgg),
            "PhilosophyFloorTwilight egg break");
        Task eggBreakCgTask = PresentationGuard.RunAsync(
            () => PhilosophyFloorLiberationCgController.PlayEggBreakAsync(brokenEgg),
            "PhilosophyFloorTwilight egg break CG");

        int hpLoss = Math.Max(
            1,
            (int)Math.Ceiling(
                Creature.MaxHp * EggBreakHpLossPercent / 100m));
        await CreatureCmd.Damage(
            choiceContext,
            Creature,
            hpLoss,
            ValueProp.Unblockable | ValueProp.Unpowered,
            null,
            null);
        PresentationGuard.Run(EncounterBgmController.RefreshCurrentEncounterTrack, "PhilosophyFloorTwilight bgm");
        await Task.WhenAll(eggBreakTask, eggBreakCgTask);
    }

    private void ClearPlannedModeAfterStun()
    {
        HasPlannedMode = false;
        PlannedBranchCounter =
            PhilosophyFloorTwilightBranchCounter.None;
        PlannedUsesEndFallback = false;
        ClearPlannedTargets();
        if (Creature.Monster?.NextMove is { Id: var currentMoveId } stunned
            && currentMoveId == stunnedMoveId
            && _routerState != null)
        {
            stunned.FollowUpState = _routerState;
        }
    }

    private async Task RecoverFromBrokenEgg(PlayerChoiceContext choiceContext)
    {
        BrokenEggRecoveryPending = false;
        PowerModel[] negativePowers = Creature.Powers
            .Where(static power =>
                power.TypeForCurrentAmount == PowerType.Debuff)
            .ToArray();
        foreach (PowerModel power in negativePowers)
        {
            await PowerCmd.Remove(power);
        }

        await LibraryPowerCmd.Apply<LibraryStrongPower>(new ThrowingPlayerChoiceContext(),
            Creature,
            PermanentPowerGainPerEgg,
            0,
            true,
            Creature,
            null);
        await LibraryPowerCmd.Apply<LibraryEndurancePower>(new ThrowingPlayerChoiceContext(),
            Creature,
            PermanentPowerGainPerEgg,
            0,
            true,
            Creature,
            null);
    }

    private async Task RemoveVisibleDebuffsIfLongArmsActive()
    {
        if (!IsEggActive(PhilosophyFloorTwilightEgg.LongArms))
        {
            return;
        }

        PowerModel[] visibleDebuffs = Creature.Powers
            .Where(static power =>
                power.IsVisible
                && power.TypeForCurrentAmount == PowerType.Debuff)
            .ToArray();
        foreach (PowerModel power in visibleDebuffs)
        {
            await PowerCmd.Remove(power);
        }
    }

    private async Task ApplyEggResistances(PlayerChoiceContext choiceContext)
    {
        if (Creature is not LibraryCreature creature)
        {
            return;
        }

        LibraryResistanceLevel physical = AliveEggCount == 0
            ? LibraryResistanceLevel.Resist
            : LibraryResistanceLevel.Immune;
        LibraryResistanceLevel slashChao = AliveEggCount == 0
            ? LibraryResistanceLevel.Normal
            : ActiveEgg == PhilosophyFloorTwilightEgg.BigEyes
                ? LibraryResistanceLevel.Vulnerable
                : LibraryResistanceLevel.Resist;
        LibraryResistanceLevel bluntChao = AliveEggCount == 0
            ? LibraryResistanceLevel.Normal
            : ActiveEgg == PhilosophyFloorTwilightEgg.SmallBeak
                ? LibraryResistanceLevel.Vulnerable
                : LibraryResistanceLevel.Resist;
        LibraryResistanceLevel pierceChao = AliveEggCount == 0
            ? LibraryResistanceLevel.Normal
            : ActiveEgg == PhilosophyFloorTwilightEgg.LongArms
                ? LibraryResistanceLevel.Vulnerable
                : LibraryResistanceLevel.Resist;

        await LibraryCreatureCmd.SetPhysicalResistance(
            choiceContext, creature, Creature,
            LibraryDamageType.Slash, physical);
        await LibraryCreatureCmd.SetPhysicalResistance(
            choiceContext, creature, Creature,
            LibraryDamageType.Pierce, physical);
        await LibraryCreatureCmd.SetPhysicalResistance(
            choiceContext, creature, Creature,
            LibraryDamageType.Blunt, physical);
        await LibraryCreatureCmd.SetChaoResistance(
            choiceContext, creature, Creature,
            LibraryDamageType.Slash, slashChao);
        await LibraryCreatureCmd.SetChaoResistance(
            choiceContext, creature, Creature,
            LibraryDamageType.Pierce, pierceChao);
        await LibraryCreatureCmd.SetChaoResistance(
            choiceContext, creature, Creature,
            LibraryDamageType.Blunt, bluntChao);
        creature.HealthBar?.RefreshValues();
    }

    private void PlanNextMode(Rng rng)
    {
        PlannedBranchCounter = PhilosophyFloorTwilightBranchCounter.None;
        PlannedUsesEndFallback = false;
        PlannedMode = ModeCycleStep switch
        {
            0 => PlanFirstCycleMode(),
            1 => PlanSecondCycleMode(),
            _ => PlanOtherMode(rng)
        };
        PlanTargetsForMode(rng);
        HasPlannedMode = true;
    }

    private PhilosophyFloorTwilightMode PlanFirstCycleMode()
    {
        if (CountPlayersWithThreeSin() <= 1
            && IsEggActive(PhilosophyFloorTwilightEgg.BigEyes))
        {
            return PhilosophyFloorTwilightMode.Surveillance;
        }

        if (IsEggActive(PhilosophyFloorTwilightEgg.LongArms))
        {
            PlannedBranchCounter |=
                PhilosophyFloorTwilightBranchCounter.Judgment;
            return IsThirdBranchEntry(JudgmentBranchEntries)
                ? PhilosophyFloorTwilightMode.EndTwo
                : PhilosophyFloorTwilightMode.Judgment;
        }

        return PhilosophyFloorTwilightMode.EndTwo;
    }

    private PhilosophyFloorTwilightMode PlanSecondCycleMode()
    {
        if (CountPlayersWithThreeSin() <= 1
            && IsEggActive(PhilosophyFloorTwilightEgg.LongArms))
        {
            PlannedBranchCounter |=
                PhilosophyFloorTwilightBranchCounter.SinTrace;
            if (!IsThirdBranchEntry(SinTraceBranchEntries))
            {
                return PhilosophyFloorTwilightMode.SinTrace;
            }
        }

        if (IsEggActive(PhilosophyFloorTwilightEgg.SmallBeak))
        {
            PlannedBranchCounter |=
                PhilosophyFloorTwilightBranchCounter.Punishment;
            if (!IsThirdBranchEntry(PunishmentBranchEntries))
            {
                return PhilosophyFloorTwilightMode.Punishment;
            }
        }

        PlannedUsesEndFallback = true;
        return NextEndFallbackIsOne
            ? PhilosophyFloorTwilightMode.EndOne
            : PhilosophyFloorTwilightMode.EndThree;
    }

    private PhilosophyFloorTwilightMode PlanOtherMode(Rng rng)
    {
        List<PhilosophyFloorTwilightAction> firstCandidates =
        [
            PhilosophyFloorTwilightAction.Talon,
            PhilosophyFloorTwilightAction.ForestLight
        ];
        if (IsEggAlive(PhilosophyFloorTwilightEgg.BigEyes))
        {
            firstCandidates.Add(
                PhilosophyFloorTwilightAction.BrilliantEyes);
        }
        if (IsEggAlive(PhilosophyFloorTwilightEgg.LongArms))
        {
            firstCandidates.Add(
                PhilosophyFloorTwilightAction.TiltedScale);
        }
        if (IsEggAlive(PhilosophyFloorTwilightEgg.SmallBeak))
        {
            firstCandidates.Add(
                PhilosophyFloorTwilightAction.TornMouth);
        }

        PlannedOtherFirstAction = rng.NextItem(firstCandidates);
        PlannedOtherSecondAction = rng.NextItem(
        [
            PhilosophyFloorTwilightAction.Talon,
            PhilosophyFloorTwilightAction.Prowl
        ]);
        RefreshOtherPlannedIntents();
        return PhilosophyFloorTwilightMode.Other;
    }

    private async Task PerformMode(
        PhilosophyFloorTwilightMode mode,
        IReadOnlyList<Creature> targets)
    {
        _performedActionTrace.Clear();
        IReadOnlyList<PhilosophyFloorTwilightAction> actions =
            mode == PhilosophyFloorTwilightMode.Other
                ?
                [
                    PlannedOtherFirstAction,
                    PlannedOtherSecondAction,
                    PhilosophyFloorTwilightAction.SlamDown
                ]
                : GetModeActions(mode);
        try
        {
            for (int slot = 0; slot < actions.Count; slot++)
            {
                if (Creature.IsDead)
                {
                    break;
                }

                _performedActionTrace.Add(actions[slot]);
                await ExecuteAction(actions[slot], targets, slot);
            }
        }
        catch
        {
            ClearInterruptedPlan();
            throw;
        }

        CommitPerformedPlan();
    }

    private void ClearInterruptedPlan()
    {
        PlannedBranchCounter = PhilosophyFloorTwilightBranchCounter.None;
        PlannedUsesEndFallback = false;
        HasPlannedMode = false;
        ClearPlannedTargets();
    }

    private void CommitPerformedPlan()
    {
        if (PlannedBranchCounter.HasFlag(
                PhilosophyFloorTwilightBranchCounter.Judgment))
        {
            JudgmentBranchEntries++;
        }
        if (PlannedBranchCounter.HasFlag(
                PhilosophyFloorTwilightBranchCounter.SinTrace))
        {
            SinTraceBranchEntries++;
        }
        if (PlannedBranchCounter.HasFlag(
                PhilosophyFloorTwilightBranchCounter.Punishment))
        {
            PunishmentBranchEntries++;
        }

        if (PlannedUsesEndFallback)
        {
            NextEndFallbackIsOne = !NextEndFallbackIsOne;
        }

        ModeCycleStep = (ModeCycleStep + 1) % ModeCycleLength;
        PlannedBranchCounter = PhilosophyFloorTwilightBranchCounter.None;
        PlannedUsesEndFallback = false;
        HasPlannedMode = false;
        ClearPlannedTargets();
    }

    private static bool IsThirdBranchEntry(int priorEntries) =>
        (priorEntries + 1) % 3 == 0;

    private int CountPlayersWithThreeSin() =>
        Creature.CombatState?.PlayerCreatures.Count(player =>
            player.IsAlive
            && (player.GetPower<PhilosophyFloorTwilightSinPower>()?.Amount
                ?? 0m) >= 3m) ?? 0;

    private PhilosophyFloorTwilightEgg ResolveActiveEgg(int round)
    {
        int cycleRound = (((round - 1) % EggScheduleCycleLength)
                          + EggScheduleCycleLength)
                         % EggScheduleCycleLength;
        PhilosophyFloorTwilightEgg[] priority =
            cycleRound switch
            {
                < EggScheduleStageLength =>
                [
                    PhilosophyFloorTwilightEgg.BigEyes,
                    PhilosophyFloorTwilightEgg.SmallBeak,
                    PhilosophyFloorTwilightEgg.LongArms
                ],
                < EggScheduleStageLength * 2 =>
                [
                    PhilosophyFloorTwilightEgg.SmallBeak,
                    PhilosophyFloorTwilightEgg.LongArms,
                    PhilosophyFloorTwilightEgg.BigEyes
                ],
                _ =>
                [
                    PhilosophyFloorTwilightEgg.LongArms,
                    PhilosophyFloorTwilightEgg.BigEyes,
                    PhilosophyFloorTwilightEgg.SmallBeak
                ]
            };
        return priority.FirstOrDefault(IsEggAlive);
    }

    private static IReadOnlyList<PhilosophyFloorTwilightAction>
        GetModeActions(PhilosophyFloorTwilightMode mode) => mode switch
        {
            PhilosophyFloorTwilightMode.Surveillance =>
            [
                PhilosophyFloorTwilightAction.Surveillance,
                PhilosophyFloorTwilightAction.BrilliantEyes,
                PhilosophyFloorTwilightAction.ForestLight
            ],
            PhilosophyFloorTwilightMode.Punishment =>
            [
                PhilosophyFloorTwilightAction.Punishment,
                PhilosophyFloorTwilightAction.TornMouth,
                PhilosophyFloorTwilightAction.SlamDown
            ],
            PhilosophyFloorTwilightMode.SinTrace =>
            [
                PhilosophyFloorTwilightAction.TiltedScale,
                PhilosophyFloorTwilightAction.SlamDown,
                PhilosophyFloorTwilightAction.Talon
            ],
            PhilosophyFloorTwilightMode.Judgment =>
            [
                PhilosophyFloorTwilightAction.Judgment,
                PhilosophyFloorTwilightAction.TiltedScale,
                PhilosophyFloorTwilightAction.Prowl
            ],
            PhilosophyFloorTwilightMode.EndOne =>
            [
                PhilosophyFloorTwilightAction.PeaceForAll,
                PhilosophyFloorTwilightAction.ProtectBlackForest,
                PhilosophyFloorTwilightAction.ForestLight
            ],
            PhilosophyFloorTwilightMode.EndTwo =>
            [
                PhilosophyFloorTwilightAction.PeaceForAll,
                PhilosophyFloorTwilightAction.Prowl,
                PhilosophyFloorTwilightAction.SlamDown
            ],
            PhilosophyFloorTwilightMode.EndThree =>
            [
                PhilosophyFloorTwilightAction.ProtectBlackForest,
                PhilosophyFloorTwilightAction.ForestLight,
                PhilosophyFloorTwilightAction.Talon,
                PhilosophyFloorTwilightAction.SlamDown
            ],
            _ => Array.Empty<PhilosophyFloorTwilightAction>()
        };

    private void RefreshOtherPlannedIntents()
    {
        if (_otherPlannedIntents is not { Length: 3 })
        {
            return;
        }

        _otherPlannedIntents[0] =
            CreateActionIntent(PlannedOtherFirstAction);
        _otherPlannedIntents[1] =
            CreateActionIntent(PlannedOtherSecondAction, 1);
        _otherPlannedIntents[2] =
            CreateActionIntent(
                PhilosophyFloorTwilightAction.SlamDown,
                2);
    }

    private static string GetModeStateId(
        PhilosophyFloorTwilightMode mode) =>
        $"PHILOSOPHY_FLOOR_TWILIGHT_{mode.ToString().ToUpperInvariant()}";

    private IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        foreach (PhilosophyFloorTwilightAction action in
                 Enum.GetValues<PhilosophyFloorTwilightAction>())
        {
            yield return CreateActionIntent(action);
        }
    }

    private void PlanTargetsForMode(Rng rng)
    {
        ClearPlannedTargets();
        IReadOnlyList<PhilosophyFloorTwilightAction> actions =
            PlannedMode == PhilosophyFloorTwilightMode.Other
                ?
                [
                    PlannedOtherFirstAction,
                    PlannedOtherSecondAction,
                    PhilosophyFloorTwilightAction.SlamDown
                ]
                : GetModeActions(PlannedMode);
        for (int slot = 0; slot < actions.Count && slot < 4; slot++)
        {
            if (!RequiresPlannedPlayerTargets(actions[slot]))
            {
                continue;
            }

            SetPlannedTargetCombatId(
                slot,
                ToSavedCombatId(SelectRandomTarget(rng)));
        }
    }

    private Creature? SelectRandomTarget(Rng rng)
    {
        IReadOnlyList<Creature> players = GetLivingPlayers();
        return players.Count == 0 ? null : rng.NextItem(players);
    }

    internal static int ResolveNonAttackTargetCount(int playerCount) =>
        playerCount switch
        {
            <= 0 => 0,
            1 or 2 => 1,
            3 => 2,
            4 => 4,
            _ => playerCount - 1
        };

    private IReadOnlyList<Creature> ResolvePlannedTargets(
        int slot,
        Creature owner)
    {
        IReadOnlyList<Creature>? party = owner.CombatState?.PlayerCreatures;
        IReadOnlyList<Creature> players = CombatTargets.DeterministicLiving(
            party,
            owner);
        int targetCount = Math.Min(
            ResolveNonAttackTargetCount(party?.Count ?? players.Count),
            players.Count);
        if (targetCount <= 0)
        {
            return [];
        }

        if (targetCount >= players.Count)
        {
            return players;
        }

        int combatId = GetPlannedTargetCombatId(slot);
        Creature? planned = combatId > 0
            ? players.FirstOrDefault(player =>
                player.CombatId == (uint)combatId)
            : null;
        if (targetCount == 1)
        {
            return planned == null ? players.Take(1).ToArray() : [planned];
        }

        return planned == null
            ? players.Take(targetCount).ToArray()
            : players.Where(player => player != planned)
                .Take(targetCount)
                .ToArray();
    }

    private static bool RequiresPlannedPlayerTargets(
        PhilosophyFloorTwilightAction action) => action is
            PhilosophyFloorTwilightAction.ForestLight
            or PhilosophyFloorTwilightAction.BrilliantEyes
            or PhilosophyFloorTwilightAction.Surveillance;

    private static int ToSavedCombatId(Creature? target) =>
        target?.CombatId is { } combatId
        && combatId <= int.MaxValue
            ? (int)combatId
            : 0;

    internal int GetPlannedTargetCombatId(int slot) => slot switch
    {
        0 => PlannedTargetOneCombatId,
        1 => PlannedTargetTwoCombatId,
        2 => PlannedTargetThreeCombatId,
        3 => PlannedTargetFourCombatId,
        _ => 0
    };

    private void SetPlannedTargetCombatId(int slot, int combatId)
    {
        switch (slot)
        {
            case 0:
                PlannedTargetOneCombatId = combatId;
                break;
            case 1:
                PlannedTargetTwoCombatId = combatId;
                break;
            case 2:
                PlannedTargetThreeCombatId = combatId;
                break;
            case 3:
                PlannedTargetFourCombatId = combatId;
                break;
        }
    }

    private void ClearPlannedTargets()
    {
        PlannedTargetOneCombatId = 0;
        PlannedTargetTwoCombatId = 0;
        PlannedTargetThreeCombatId = 0;
        PlannedTargetFourCombatId = 0;
    }

    internal PhilosophyFloorTwilightPersistentState CapturePersistentState() =>
        new()
        {
            AliveEggMask = AliveEggMask,
            ActiveEgg = ActiveEgg,
            PlannedMode = PlannedMode,
            HasPlannedMode = HasPlannedMode,
            ModeCycleStep = ModeCycleStep,
            JudgmentBranchEntries = JudgmentBranchEntries,
            SinTraceBranchEntries = SinTraceBranchEntries,
            PunishmentBranchEntries = PunishmentBranchEntries,
            NextEndFallbackIsOne = NextEndFallbackIsOne,
            PlannedBranchCounter = PlannedBranchCounter,
            PlannedUsesEndFallback = PlannedUsesEndFallback,
            PlannedOtherFirstAction = PlannedOtherFirstAction,
            PlannedOtherSecondAction = PlannedOtherSecondAction,
            LastEggScheduleRound = LastEggScheduleRound,
            BrokenEggRecoveryPending = BrokenEggRecoveryPending,
            IntroCgPlayed = IntroCgPlayed,
            PlannedTargetCombatIds =
            [
                PlannedTargetOneCombatId,
                PlannedTargetTwoCombatId,
                PlannedTargetThreeCombatId,
                PlannedTargetFourCombatId
            ],
            SmallBeakProcessedRound = SmallBeakProcessedRound,
            SmallBeakProcessedPlayerCombatIds =
                [.. SmallBeakProcessedPlayerCombatIds]
        };

    internal void RestorePersistentState(
        PhilosophyFloorTwilightPersistentState state)
    {
        AliveEggMask = state.AliveEggMask & AllEggMask;
        ActiveEgg = Enum.IsDefined(state.ActiveEgg)
            ? state.ActiveEgg
            : PhilosophyFloorTwilightEgg.BigEyes;
        if (AliveEggMask == 0)
        {
            ActiveEgg = PhilosophyFloorTwilightEgg.None;
        }
        else if (!IsEggAlive(ActiveEgg))
        {
            ActiveEgg = new[]
                {
                    PhilosophyFloorTwilightEgg.BigEyes,
                    PhilosophyFloorTwilightEgg.SmallBeak,
                    PhilosophyFloorTwilightEgg.LongArms
                }
                .First(IsEggAlive);
        }

        PlannedMode = Enum.IsDefined(state.PlannedMode)
            ? state.PlannedMode
            : PhilosophyFloorTwilightMode.Surveillance;
        HasPlannedMode = state.HasPlannedMode;
        ModeCycleStep = ((state.ModeCycleStep % ModeCycleLength)
                         + ModeCycleLength) % ModeCycleLength;
        JudgmentBranchEntries = Math.Max(0, state.JudgmentBranchEntries);
        SinTraceBranchEntries = Math.Max(0, state.SinTraceBranchEntries);
        PunishmentBranchEntries = Math.Max(0, state.PunishmentBranchEntries);
        NextEndFallbackIsOne = state.NextEndFallbackIsOne;
        PlannedBranchCounter = state.PlannedBranchCounter &
            (PhilosophyFloorTwilightBranchCounter.Judgment
             | PhilosophyFloorTwilightBranchCounter.SinTrace
             | PhilosophyFloorTwilightBranchCounter.Punishment);
        PlannedUsesEndFallback = state.PlannedUsesEndFallback;
        PlannedOtherFirstAction = Enum.IsDefined(
            state.PlannedOtherFirstAction)
            ? state.PlannedOtherFirstAction
            : PhilosophyFloorTwilightAction.Talon;
        PlannedOtherSecondAction = Enum.IsDefined(
            state.PlannedOtherSecondAction)
            ? state.PlannedOtherSecondAction
            : PhilosophyFloorTwilightAction.Talon;
        LastEggScheduleRound = Math.Max(-1, state.LastEggScheduleRound);
        BrokenEggRecoveryPending = state.BrokenEggRecoveryPending;
        IntroCgPlayed = state.IntroCgPlayed;

        int[] targets = state.PlannedTargetCombatIds ?? [];
        PlannedTargetOneCombatId = GetNonNegative(targets, 0);
        PlannedTargetTwoCombatId = GetNonNegative(targets, 1);
        PlannedTargetThreeCombatId = GetNonNegative(targets, 2);
        PlannedTargetFourCombatId = GetNonNegative(targets, 3);
        SmallBeakProcessedRound = Math.Max(
            -1,
            state.SmallBeakProcessedRound);
        SmallBeakProcessedPlayerCombatIds =
        [
            .. (state.SmallBeakProcessedPlayerCombatIds ?? [])
                .Where(static combatId => combatId >= 0)
                .Distinct()
        ];
    }

    internal bool TryMarkSmallBeakPlayerProcessed(
        int round,
        int playerCombatId)
    {
        if (SmallBeakProcessedRound != round)
        {
            SmallBeakProcessedRound = round;
            SmallBeakProcessedPlayerCombatIds = [];
        }

        if (SmallBeakProcessedPlayerCombatIds.Contains(playerCombatId))
        {
            return false;
        }

        SmallBeakProcessedPlayerCombatIds =
            [.. SmallBeakProcessedPlayerCombatIds, playerCombatId];
        return true;
    }

    private static int GetNonNegative(int[] values, int index) =>
        index < values.Length ? Math.Max(0, values[index]) : 0;

    internal bool IsEggAlive(PhilosophyFloorTwilightEgg egg)
    {
        int bit = EggBit(egg);
        return bit != 0 && (AliveEggMask & bit) != 0;
    }

    internal bool IsEggActive(PhilosophyFloorTwilightEgg egg) =>
        ActiveEgg == egg && IsEggAlive(egg);

    internal static int EggBit(PhilosophyFloorTwilightEgg egg) => egg switch
    {
        PhilosophyFloorTwilightEgg.BigEyes => 0b001,
        PhilosophyFloorTwilightEgg.SmallBeak => 0b010,
        PhilosophyFloorTwilightEgg.LongArms => 0b100,
        _ => 0
    };

    private static int CountAliveEggs(int mask) =>
        (mask & 1) + ((mask >> 1) & 1) + ((mask >> 2) & 1);

    private static LibraryCreatureResistanceData.Resistance
        CreateUniformResistance(LibraryResistanceLevel level) => new()
        {
            Slash = level,
            Pierce = level,
            Blunt = level
        };
}
