using System;
using System.Globalization;
using System.Linq;
using LibraryOfRuina.helpers;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using HarmonyLib;
using LibraryLib.Entities.Creatures;
using LibraryLib.Hooks;
using LibraryOfRuina.audio;
using LibraryOfRuina.cards.Iori;
using LibraryOfRuina.compat;
using LibraryOfRuina.intents;
using LibraryOfRuina.monsters;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.specialguests.Iori;

public abstract class IoriMonsterBase :
    SpecialGuestMonsterBase, LibraryOfRuina.helpers.IFinalHpLossClamp
{
    internal const string RouterMoveId = "IORI_ROUTER";
    internal const string CompositeMoveId = "IORI_COMPOSITE";
    internal const string HiddenMoveId = "IORI_HIDDEN";
    internal const string EscapeMoveId = "IORI_ESCAPE";

    private static readonly IoriStance[] AllStances =
    [
        IoriStance.Slash,
        IoriStance.Pierce,
        IoriStance.Blunt,
        IoriStance.Defense,
    ];

    private static readonly IoriStance[] OffensiveStances =
    [
        IoriStance.Slash,
        IoriStance.Pierce,
        IoriStance.Blunt,
    ];

    private AbstractIntent[]? _plannedIntents;
    private MoveState? _compositeState;
    private MoveState? _hiddenState;
    private MoveState? _escapeState;
    private bool _escapeCompleting;
    private LibraryDamageType _activeDamageType = LibraryDamageType.None;
    private LibraryDamageType _previewDamageType = LibraryDamageType.None;
    private bool _shouldLockHealthBar;

    internal bool IsHealthBarLockActive => _shouldLockHealthBar;

    [SavedProperty]
    public IoriStance CurrentStance { get; private set; } = IoriStance.None;

    [SavedProperty]
    public int LastPlannedRound { get; private set; } = -1;

    [SavedProperty]
    public int LastRegularMove { get; private set; } = (int)IoriMove.None;

    [SavedProperty]
    public IoriStance PlannedNextStance { get; private set; } = IoriStance.None;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int SelectedStanceMask { get; private set; }

    [SavedProperty]
    public bool HasExpandedRoundTwoCapacity { get; private set; }

    [SavedProperty]
    public int ReceptionRoundOffset { get; private set; }

    [SavedProperty]
    public bool EscapeQueued { get; private set; }

    [SavedProperty]
    public bool EscapeCompleted { get; private set; }

    [SavedProperty]
    public bool StageSnapshotRestored { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string ChainsContributionByPlayerNetId { get; private set; } =
        string.Empty;

    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 900, 700);

    public override int MaxInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 900, 700);

    public override int DefaultChaoResistance => 320;

    public override LibraryCreatureResistanceData.Resistance?
        DefaultPhysicalResistanceData => new()
        {
            Slash = LibraryResistanceLevel.Endure,
            Pierce = LibraryResistanceLevel.Normal,
            Blunt = LibraryResistanceLevel.Resist,
        };

    public override LibraryCreatureResistanceData.Resistance?
        DefaultChaoResistanceData => new()
        {
            Slash = LibraryResistanceLevel.Normal,
            Pierce = LibraryResistanceLevel.Normal,
            Blunt = LibraryResistanceLevel.Endure,
        };

    protected override int InitialIntentCapacity => 1;

    protected abstract bool IsSecondStage { get; }

    protected virtual bool CanPerformRegularMoves =>
        !EscapeQueued && !EscapeCompleted;

    public override IEnumerable<string> AssetPaths => IoriSpecialGuestAssets.All;

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await IoriPassiveInstaller.EnsureFor(this);
        _shouldLockHealthBar = false;
        if (IsSecondStage && !StageSnapshotRestored)
        {
            bool restored = await TryRestoreReceptionSnapshot();
            StageSnapshotRestored = true;
            if (!restored)
            {
                await CreatureCmd.SetCurrentHp(
                    Creature,
                    ResolveEscapeThreshold());
            }
        }

        IoriCreatureVisuals.RefreshStance(Creature);
        if (Creature.CombatState != null)
        {
            IoriSpecialGuestBgmController.Ensure(Creature.CombatState);
        }
    }

    public override async Task AfterCombatEnd(CombatRoom room)
    {
        await base.AfterCombatEnd(room);
        if (room.Encounter is ISpecialGuestEncounterStage
            {
                SpecialGuestId: IoriSpecialGuestIds.Guest,
            })
        {
            IoriSpecialGuestBgmController.Stop();
        }
    }

    public override void BeforeRemovedFromRoom()
    {
        IoriSpecialGuestBgmController.Stop();
        base.BeforeRemovedFromRoom();
    }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _plannedIntents = null;
        _compositeState = null;
        _hiddenState = null;
        _escapeState = null;
        _escapeCompleting = false;
        _activeDamageType = LibraryDamageType.None;
        _previewDamageType = LibraryDamageType.None;
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        _plannedIntents = new AbstractIntent[StoredIntentSlots];
        RefreshPlannedIntents();

        _compositeState = new MoveState(
            CompositeMoveId,
            PerformCompositeMove,
            _plannedIntents)
        {
            MustPerformOnceBeforeTransitioning = true,
        };
        _hiddenState = new MoveState(
            HiddenMoveId,
            static _ => Task.CompletedTask,
            new HiddenIntent());
        _escapeState = new MoveState(
            EscapeMoveId,
            PerformEscapeMove,
            new EscapeIntent())
        {
            MustPerformOnceBeforeTransitioning = true,
        };

        var router = new DelegatingMonsterRouterState(
            RouterMoveId,
            (_, _) => ResolveFollowUpMoveId());
        _compositeState.FollowUpState = router;
        _hiddenState.FollowUpState = router;
        _escapeState.FollowUpState = _escapeState;

        return new MonsterMoveStateMachine(
            [_compositeState, _hiddenState, _escapeState, router],
            EscapeQueued
                ? _escapeState
                : HasStoredIntentPlan
                    ? _compositeState
                    : _hiddenState);
    }

    private string ResolveFollowUpMoveId()
    {
        if (EscapeQueued && !EscapeCompleted)
        {
            return EscapeMoveId;
        }

        return HasStoredIntentPlan && CanPerformRegularMoves
            ? CompositeMoveId
            : HiddenMoveId;
    }

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        IoriSpecialGuestBgmController.RefreshForRound(combatState);
        await base.BeforeSideTurnStart(
            choiceContext,
            side,
            participants,
            combatState);

        if (side != CombatSide.Player
            || Creature.IsDead
            || CombatManager.Instance.PlayersTakingExtraTurn.Count > 0)
        {
            return;
        }

        if (!IsSecondStage
            && Creature.CurrentHp <= ResolveEscapeThreshold())
        {
            QueueEscape();
        }

        if (CurrentStance == IoriStance.None)
        {
            await ChangeStance(
                DrawInitialStance(RunRng.MonsterAi),
                applyContributions: true);
        }

        int receptionRound = ReceptionRoundOffset + combatState.RoundNumber;
        if (!HasExpandedRoundTwoCapacity && receptionRound >= 2)
        {
            HasExpandedRoundTwoCapacity = true;
            IntentCapacity = Math.Min(
                StoredIntentSlots,
                IntentCapacity + 1);
        }

        if (EscapeQueued && !EscapeCompleted)
        {
            RevealEscapeIntent();
            return;
        }

        if (LastPlannedRound == combatState.RoundNumber
            && HasStoredIntentPlan)
        {
            RefreshPlannedIntents();
            RevealPlan();
            return;
        }

        PlanRound(combatState.RoundNumber, RunRng.MonsterAi);
    }

    private void PlanRound(int roundNumber, Rng rng)
    {
        if (roundNumber > 0 && roundNumber % 3 == 0)
        {
            PlannedNextStance = DrawNextStance(rng);
            InstallPlan([IoriMove.StanceShift], roundNumber);
            return;
        }

        PlannedNextStance = IoriStance.None;
        var moves = new List<IoriMove>(StoredIntentSlots);
        if (IsSecondStage
            && roundNumber >= 2
            && (roundNumber - 2) % 3 == 0)
        {
            moves.Add(IoriMove.PhantomDance);
        }

        int regularSlots = Math.Max(
            0,
            Math.Min(IntentCapacity, StoredIntentSlots) - moves.Count);
        moves.AddRange(DrawRegularMoves(rng, regularSlots));
        InstallPlan(moves, roundNumber);
    }

    private IReadOnlyList<IoriMove> DrawRegularMoves(Rng rng, int count)
    {
        if (count <= 0)
        {
            return [];
        }

        List<IoriMove> candidates =
            IoriMoveDefinitions.GetStanceMoves(CurrentStance).ToList();
        rng.Shuffle(candidates);

        IoriMove previous = (IoriMove)LastRegularMove;
        if (candidates.Count > 1 && candidates[0] == previous)
        {
            int replacement = candidates.FindIndex(
                1,
                move => move != previous);
            if (replacement > 0)
            {
                (candidates[0], candidates[replacement]) =
                    (candidates[replacement], candidates[0]);
            }
        }

        List<IoriMove> selected = candidates
            .Take(Math.Min(count, candidates.Count))
            .ToList();
        // Penetrating Wound keeps its normal random-selection chance, then
        // moves to the final slot only when it was selected.
        if (selected.Remove(IoriMove.PenetratingWound))
        {
            selected.Add(IoriMove.PenetratingWound);
        }

        if (selected.Count > 0)
        {
            LastRegularMove = (int)selected[^1];
        }

        return selected;
    }

    private void InstallPlan(
        IReadOnlyList<IoriMove> moves,
        int roundNumber)
    {
        int count = Math.Min(
            moves.Count,
            Math.Min(IntentCapacity, StoredIntentSlots));
        for (int slot = 0; slot < StoredIntentSlots; slot++)
        {
            SetStoredIntent(
                slot,
                (int)(slot < count ? moves[slot] : IoriMove.None));
        }

        LastPlannedRound = roundNumber;
        HasCompletedFirstTurn = true;
        RefreshPlannedIntents();
        RevealPlan();
    }

    private async Task PerformCompositeMove(
        IReadOnlyList<Creature> targets)
    {
        _ = targets;
        for (int slot = 0;
             slot < StoredIntentSlots
             && Creature.IsAlive
             && CanPerformRegularMoves;
             slot++)
        {
            IoriMove move = GetPlannedMove(slot);
            if (move == IoriMove.None)
            {
                break;
            }

            await PerformMove(move);
        }

        ClearPlan();
        if (EscapeQueued && !EscapeCompleted)
        {
            RevealEscapeIntent();
        }
        else
        {
            HidePlan();
        }
    }

    private async Task PerformMove(IoriMove move)
    {
        IoriMoveDefinition definition = IoriMoveDefinitions.Get(move);
        if (move == IoriMove.StanceShift)
        {
            LibraryDamageType type = ResolveStanceDamageType(CurrentStance);
            var attack = new IoriAttackSegment(
                definition.Attacks[0].Damage,
                type,
                "StanceChange",
                definition.Attacks[0].SfxFile);
            await ExecuteAttack(attack, hitCount: 1);
            if (CanPerformRegularMoves)
            {
                await ChangeStance(
                    PlannedNextStance == IoriStance.None
                        ? ResolveFallbackNextStance()
                        : PlannedNextStance,
                    applyContributions: true);
            }
            return;
        }

        if (move == IoriMove.PhantomDance)
        {
            IoriCreatureVisuals.BeginAttackChain(Creature);
            try
            {
                foreach (IoriAttackSegment attack in definition.Attacks)
                {
                    if (!CanPerformRegularMoves)
                    {
                        break;
                    }

                    IReadOnlyList<DamageResult> results =
                        await ExecuteAttack(attack, hitCount: 1);
                    await ApplyBleedAfterHit(
                        results,
                        definition.PrimaryEffect.Resolve());
                }
            }
            finally
            {
                IoriCreatureVisuals.EndAttackChain(Creature);
            }
            return;
        }

        if (definition.Attacks.Count > 0)
        {
            bool continuous = definition.Attacks.Count > 1;
            if (continuous)
            {
                IoriCreatureVisuals.BeginAttackChain(Creature);
            }

            try
            {
                foreach (IoriAttackSegment attack in definition.Attacks)
                {
                    if (!CanPerformRegularMoves)
                    {
                        break;
                    }

                    await ExecuteAttack(attack, hitCount: 1);
                }
            }
            finally
            {
                if (continuous)
                {
                    IoriCreatureVisuals.EndAttackChain(Creature);
                }
            }
        }

        if (!CanPerformRegularMoves)
        {
            return;
        }

        if (definition.BlockAmount > 0)
        {
            await PlayGuardAnimation();
            await CreatureCmd.GainBlock(
                Creature,
                definition.BlockAmount,
                ValueProp.Move,
                null);
        }
        else if (definition.Attacks.Count == 0
                 && definition.Effect != IoriMoveEffect.None)
        {
            await PlayGuardAnimation();
        }

        await ApplyMoveEffect(definition);
    }

    private async Task<IReadOnlyList<DamageResult>> ExecuteAttack(
        IoriAttackSegment segment,
        int hitCount)
    {
        if (Creature.IsDead
            || Creature.CombatState == null
            || !CanPerformRegularMoves)
        {
            return [];
        }

        var allResults = new List<DamageResult>();
        bool continuous = hitCount > 1;
        if (continuous)
        {
            IoriCreatureVisuals.BeginAttackChain(Creature);
        }

        _activeDamageType = segment.DamageType;
        try
        {
            for (int hit = 0;
                 hit < Math.Max(1, hitCount) && CanPerformRegularMoves;
                 hit++)
            {
                LocalOggOneShotPlayer.Play(
                    IoriSpecialGuestIds.CombatAudioRoot + segment.SfxFile,
                    -2f);
                await CreatureCmd.TriggerAnim(
                    Creature,
                    segment.Animation,
                    0f);
                await Cmd.Wait(IoriAnimationContract.ActionDurationSeconds);
                if (!CanPerformRegularMoves)
                {
                    break;
                }

                AttackCommand command = await DamageCmd
                    .Attack(segment.ResolveDamage())
                    .FromMonster(this)
                    .WithHitCount(1)
                    .WithNoAttackerAnim()
                    .WithHitFx(
                        segment.DamageType == LibraryDamageType.Blunt
                            ? "vfx/vfx_attack_blunt"
                            : "vfx/vfx_attack_slash")
                    .SpawningHitVfxOnEachCreature()
                    .Execute(null);
                allResults.AddRange(
                    AttackCommandCompat.Results(command)
                        .Where(static result => result.Receiver.IsPlayer));
            }

            RecordDirectAttackDamageDealt(allResults);
            return allResults;
        }
        finally
        {
            _activeDamageType = LibraryDamageType.None;
            if (continuous)
            {
                IoriCreatureVisuals.EndAttackChain(Creature);
            }
        }
    }

    private async Task ApplyMoveEffect(IoriMoveDefinition definition)
    {
        Creature[] players = LivingPlayers();
        switch (definition.Effect)
        {
            case IoriMoveEffect.None:
                return;
            case IoriMoveEffect.GainStrength:
                await PowerCmdCompat.Apply<StrengthPower>(
                    Creature,
                    definition.PrimaryEffect.Resolve(),
                    Creature,
                    null);
                return;
            case IoriMoveEffect.ApplyFrailAndWeak:
                foreach (Creature player in players)
                {
                    await PowerCmdCompat.ApplyDebuff<FrailPower>(
                        player,
                        definition.PrimaryEffect.Resolve(),
                        Creature,
                        null);
                    await PowerCmdCompat.ApplyDebuff<WeakPower>(
                        player,
                        definition.SecondaryEffect.Resolve(),
                        Creature,
                        null);
                }
                return;
            case IoriMoveEffect.ApplyBleedAndRapidWear:
                foreach (Creature player in players)
                {
                    await PowerCmdCompat.ApplyDebuff<LibraryBleedingPower>(
                        player,
                        definition.PrimaryEffect.Resolve(),
                        Creature,
                        null);
                    await LibraryPowerCmd.Apply<LibraryVulnerablePower>(
                        player,
                        definition.SecondaryEffect.Resolve(),
                        definition.EffectTurns,
                        Creature,
                        null);
                }
                return;
            case IoriMoveEffect.OfferPenetratingWoundChoice:
                await IoriPenetratingWoundChoices.ChooseForPlayersAsync(
                    players,
                    definition.PrimaryEffect.Resolve(),
                    definition.SecondaryEffect.Resolve());
                return;
            case IoriMoveEffect.HealPercentMaxHp:
                await CreatureCmd.Heal(
                    Creature,
                    (int)Math.Ceiling(
                        Creature.MaxHp
                        * definition.PrimaryEffect.Resolve()
                        / 100m),
                    playAnim: false);
                return;
            case IoriMoveEffect.AddWounds:
                foreach (Creature player in players)
                {
                    await CardPileCmdCompat.AddToCombatAndPreview<Wound>(
                        player,
                        PileType.Discard,
                        definition.PrimaryEffect.Resolve(),
                        addedByPlayer: false);
                }
                return;
            case IoriMoveEffect.ApplyWeak:
                foreach (Creature player in players)
                {
                    await PowerCmdCompat.ApplyDebuff<WeakPower>(
                        player,
                        definition.PrimaryEffect.Resolve(),
                        Creature,
                        null);
                }
                return;
            case IoriMoveEffect.ApplyBlur:
                await PowerCmdCompat.Apply<BlurPower>(
                    Creature,
                    definition.PrimaryEffect.Resolve(),
                    Creature,
                    null);
                return;
            case IoriMoveEffect.SwitchStance:
            case IoriMoveEffect.ApplyBleedPerHit:
                return;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(definition),
                    definition.Effect,
                    null);
        }
    }

    private async Task ApplyBleedAfterHit(
        IReadOnlyList<DamageResult> results,
        int amount)
    {
        foreach (Creature target in results
                     .Select(static result => result.Receiver)
                     .Where(static target => target.IsAlive)
                     .Distinct()
                     .OrderBy(static target => target.CombatId))
        {
            await PowerCmdCompat.ApplyDebuff<LibraryBleedingPower>(
                target,
                amount,
                Creature,
                null);
        }
    }

    private async Task PlayGuardAnimation()
    {
        LocalOggOneShotPlayer.Play(
            IoriSpecialGuestIds.CombatAudioRoot + "Purple_Guard.ogg",
            -2f);
        await CreatureCmd.TriggerAnim(Creature, "Guard", 0f);
        await Cmd.Wait(IoriAnimationContract.ActionDurationSeconds);
    }

    private Creature[] LivingPlayers() =>
        Creature.CombatState?.PlayerCreatures
            .Where(static creature => creature.IsAlive)
            .OrderBy(static creature => creature.CombatId)
            .ToArray()
        ?? [];

    private AbstractIntent CreateIntent(IoriMove move)
    {
        IoriMoveDefinition definition = IoriMoveDefinitions.Get(move);
        Func<int> damage = () => definition.ResolveIntentDamage();
        Func<LibraryDamageType> damageType = () =>
        {
            if (definition.Attacks.Count == 0)
            {
                return LibraryDamageType.None;
            }

            LibraryDamageType type = definition.Attacks[0].DamageType;
            return type == LibraryDamageType.None
                ? ResolveStanceDamageType(CurrentStance)
                : type;
        };
        int block = ResolveIntentBlock(definition);
        return definition.IntentKind switch
        {
            IoriMoveIntentKind.Attack =>
                new IoriTypedAttackIntent(
                    this,
                    damage,
                    damageType,
                    () => Math.Max(1, definition.ResolveHitCount())),
            IoriMoveIntentKind.AttackBuff =>
                new IoriTypedCombinedAttackIntent(
                    this,
                    damage,
                    damageType,
                    () => Math.Max(1, definition.ResolveHitCount()),
                    IoriTypedCombinedAttackKind.Buff,
                    blockAmount: 0,
                    definition.Effect == IoriMoveEffect.GainStrength
                        ? IntentBadge.Strength(
                            definition.PrimaryEffect.Resolve())
                        : IntentBadge.Custom(
                            "powers/library_passive_orange.png")),
            IoriMoveIntentKind.AttackDebuff =>
                new IoriTypedCombinedAttackIntent(
                    this,
                    damage,
                    damageType,
                    () => Math.Max(1, definition.ResolveHitCount()),
                    IoriTypedCombinedAttackKind.Debuff,
                    blockAmount: 0,
                    IntentBadge.Bleed(
                        definition.PrimaryEffect.Resolve()),
                    IntentBadge.RapidWear(
                        definition.SecondaryEffect.Resolve(),
                        definition.EffectTurns)),
            IoriMoveIntentKind.AttackDefend =>
                new IoriTypedCombinedAttackIntent(
                    this,
                    damage,
                    damageType,
                    () => Math.Max(1, definition.ResolveHitCount()),
                    IoriTypedCombinedAttackKind.Defend,
                    block),
            IoriMoveIntentKind.Debuff => new DebuffIntent(strong: true),
            IoriMoveIntentKind.CardDebuff => new CardDebuffIntent(),
            IoriMoveIntentKind.Heal => new HealIntent(),
            IoriMoveIntentKind.StatusCard =>
                new StatusIntent(definition.PrimaryEffect.Resolve()),
            IoriMoveIntentKind.DefendBuff =>
                new CombinedDefendBuffIntent(
                    block,
                    null,
                    IntentBadge.FromPower<BlurPower>(
                        definition.PrimaryEffect.Resolve())),
            IoriMoveIntentKind.DefendDebuff =>
                new CombinedDefendDebuffIntent(
                    block,
                    null,
                    IntentBadge.Weak(
                        definition.PrimaryEffect.Resolve())),
            IoriMoveIntentKind.MultiAttack =>
                new MultiAttackIntent(
                    definition.ResolveIntentDamage(),
                    () => Math.Max(1, definition.ResolveHitCount())),
            _ => new HiddenIntent(),
        };
    }

    private int ResolveIntentBlock(IoriMoveDefinition definition) =>
        definition.BlockAmount
        + (definition.BlockAmount > 0
           && CurrentStance == IoriStance.Defense
            ? 1
            : 0);

    internal LibraryDamageType ResolveActiveOrPreviewDamageType() =>
        _activeDamageType != LibraryDamageType.None
            ? _activeDamageType
            : _previewDamageType != LibraryDamageType.None
                ? _previewDamageType
                : ResolveStanceDamageType(CurrentStance);

    internal int ResolveTypedPreviewDamage(
        decimal baseDamage,
        LibraryDamageType type,
        Creature? target,
        bool keepPreviewType = false)
    {
        _previewDamageType = type;
        try
        {
            if (target?.CombatState == null)
            {
                return Math.Max(0, (int)baseDamage);
            }

            decimal damage = LibraryHooks.ModifyDamage(
                target.CombatState.RunState,
                target.CombatState,
                target,
                Creature,
                baseDamage,
                ValueProp.Move,
                null,
                null,
                ModifyDamageHookType.All,
                CardPreviewMode.None,
                out _,
                type);
            return Math.Max(0, (int)damage);
        }
        finally
        {
            if (!keepPreviewType)
            {
                _previewDamageType = LibraryDamageType.None;
            }
        }
    }

    private void RefreshPlannedIntents()
    {
        if (_plannedIntents == null)
        {
            return;
        }

        for (int slot = 0; slot < StoredIntentSlots; slot++)
        {
            _plannedIntents[slot] = CreateIntent(GetPlannedMove(slot));
        }
    }

    private IoriMove GetPlannedMove(int slot) =>
        (IoriMove)GetStoredIntent(slot);

    private void ClearPlan()
    {
        ClearStoredIntentPlan();
        RefreshPlannedIntents();
    }

    private void RevealPlan()
    {
        if (_compositeState != null
            && HasStoredIntentPlan
            && CanPerformRegularMoves)
        {
            SetMoveImmediate(_compositeState, forceTransition: true);
        }
    }

    private void HidePlan()
    {
        if (_hiddenState != null)
        {
            SetMoveImmediate(_hiddenState, forceTransition: true);
        }
    }

    private void RevealEscapeIntent()
    {
        ClearPlan();
        if (_escapeState != null)
        {
            SetMoveImmediate(_escapeState, forceTransition: true);
        }
    }

    private IoriStance DrawInitialStance(Rng rng)
    {
        IoriStance[] candidates = SelectableStances;
        return candidates[rng.NextInt(candidates.Length)];
    }

    private IoriStance DrawNextStance(Rng rng)
    {
        IoriStance[] candidates = GetPreferredNextStances();
        return candidates[rng.NextInt(candidates.Length)];
    }

    private IoriStance ResolveFallbackNextStance() =>
        GetPreferredNextStances()[0];

    private IoriStance[] SelectableStances =>
        IsSecondStage ? OffensiveStances : AllStances;

    private IoriStance[] GetPreferredNextStances()
    {
        IoriStance[] selectable = SelectableStances;
        int selectableMask = GetStanceMask(selectable);
        int selectedMask = SelectedStanceMask & selectableMask;
        if (selectedMask == selectableMask)
        {
            selectedMask = 0;
        }

        IoriStance[] unselected = selectable
            .Where(stance => stance != CurrentStance)
            .Where(stance => (selectedMask & GetStanceBit(stance)) == 0)
            .ToArray();
        return unselected.Length > 0
            ? unselected
            : selectable.Where(stance => stance != CurrentStance).ToArray();
    }

    private void RecordStanceSelection(IoriStance stance)
    {
        IoriStance[] selectable = SelectableStances;
        int selectableMask = GetStanceMask(selectable);
        SelectedStanceMask =
            (SelectedStanceMask & selectableMask) | GetStanceBit(stance);
        if ((SelectedStanceMask & selectableMask) == selectableMask)
        {
            SelectedStanceMask = 0;
        }
    }

    private static int GetStanceMask(IEnumerable<IoriStance> stances) =>
        stances.Aggregate(0, (mask, stance) => mask | GetStanceBit(stance));

    private static int GetStanceBit(IoriStance stance) =>
        stance == IoriStance.None ? 0 : 1 << (int)stance;

    private async Task ChangeStance(
        IoriStance next,
        bool applyContributions)
    {
        if (next == IoriStance.None || next == CurrentStance)
        {
            return;
        }

        if (CurrentStance != IoriStance.None)
        {
            await RemoveStanceContributions(CurrentStance);
            await RemoveStancePower(CurrentStance);
        }

        CurrentStance = next;
        RecordStanceSelection(next);
        if (applyContributions)
        {
            await AddStanceContributions(next);
        }

        await ApplyStancePower(next);
        IoriCreatureVisuals.RefreshStance(Creature);
    }

    private async Task ApplyStancePower(IoriStance stance)
    {
        switch (stance)
        {
            case IoriStance.Slash:
                await PowerCmdCompat.Apply<IoriSlashStancePower>(
                    Creature,
                    1,
                    Creature,
                    null);
                return;
            case IoriStance.Pierce:
                await PowerCmdCompat.Apply<IoriPierceStancePower>(
                    Creature,
                    1,
                    Creature,
                    null);
                return;
            case IoriStance.Blunt:
                await PowerCmdCompat.Apply<IoriBluntStancePower>(
                    Creature,
                    1,
                    Creature,
                    null);
                return;
            case IoriStance.Defense:
                await PowerCmdCompat.Apply<IoriDefenseStancePower>(
                    Creature,
                    1,
                    Creature,
                    null);
                return;
            default:
                return;
        }
    }

    private async Task RemoveStancePower(IoriStance stance)
    {
        PowerModel? power = stance switch
        {
            IoriStance.Slash => Creature.GetPower<IoriSlashStancePower>(),
            IoriStance.Pierce => Creature.GetPower<IoriPierceStancePower>(),
            IoriStance.Blunt => Creature.GetPower<IoriBluntStancePower>(),
            IoriStance.Defense => Creature.GetPower<IoriDefenseStancePower>(),
            _ => null,
        };
        if (power != null)
        {
            await PowerCmd.Remove(power);
        }
    }

    private async Task AddStanceContributions(IoriStance stance)
    {
        switch (stance)
        {
            case IoriStance.Slash:
                await AddPermanent<LibraryStrongPower>(2);
                await AddPermanent<LibraryStrongSlashPower>(1);
                break;
            case IoriStance.Pierce:
                await AddPermanent<LibraryStrongPower>(2);
                await AddPermanent<LibraryStrongPiercePower>(1);
                await PowerCmdCompat.Apply<PainfulStabsPower>(
                    Creature,
                    1,
                    Creature,
                    null);
                break;
            case IoriStance.Blunt:
                await AddPermanent<LibraryStrongPower>(2);
                await AddPermanent<LibraryStrongBluntPower>(1);
                await AddChainsContribution(2);
                break;
            case IoriStance.Defense:
                await AddPermanent<LibraryDefensePowerUpPower>(1);
                await AddPermanent<LibraryEndurancePower>(2);
                await PowerCmdCompat.Apply<ThornsPower>(
                    Creature,
                    ResolveDefenseThorns(),
                    Creature,
                    null);
                await ClearDebuffs();
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(stance),
                    stance,
                    null);
        }
    }

    private async Task RemoveStanceContributions(IoriStance stance)
    {
        switch (stance)
        {
            case IoriStance.Slash:
                await RemovePermanent<LibraryStrongPower>(2);
                await RemovePermanent<LibraryStrongSlashPower>(1);
                break;
            case IoriStance.Pierce:
                await RemovePermanent<LibraryStrongPower>(2);
                await RemovePermanent<LibraryStrongPiercePower>(1);
                await RemoveNativeContribution<PainfulStabsPower>(1);
                break;
            case IoriStance.Blunt:
                await RemovePermanent<LibraryStrongPower>(2);
                await RemovePermanent<LibraryStrongBluntPower>(1);
                await RemoveChainsContribution(2);
                break;
            case IoriStance.Defense:
                await RemovePermanent<LibraryDefensePowerUpPower>(1);
                await RemovePermanent<LibraryEndurancePower>(2);
                await RemoveNativeContribution<ThornsPower>(
                    ResolveDefenseThorns());
                break;
        }
    }

    private Task<T?> AddPermanent<T>(int amount)
        where T : LibraryPowerModel =>
        LibraryPowerCmd.Apply<T>(
            Creature,
            amount,
            turns: -1,
            Creature,
            null);

    private async Task RemovePermanent<T>(int amount)
        where T : LibraryPowerModel
    {
        if (Creature.GetPower<T>() != null)
        {
            await LibraryPowerCmd.ModifyAmount<T>(
                Creature,
                -amount,
                turns: -1,
                Creature,
                null,
                silent: true);
        }
    }

    private async Task RemoveNativeContribution<T>(int amount)
        where T : PowerModel
    {
        if (Creature.GetPower<T>() is { } power)
        {
            await PowerCmdCompat.ModifyAmount(
                power,
                -amount,
                Creature,
                null,
                silent: true);
        }
    }

    private async Task AddChainsContribution(int amount)
    {
        if (Creature.CombatState == null)
        {
            return;
        }

        var contributions = new Dictionary<ulong, int>();
        foreach (Creature player in Creature.CombatState.PlayerCreatures
                     .Where(static player => player.IsAlive)
                     .OrderBy(static player => player.CombatId))
        {
            int before = player.GetPower<ChainsOfBindingPower>()?.Amount ?? 0;
            await PowerCmdCompat.ApplyDebuff<ChainsOfBindingPower>(
                player,
                amount,
                Creature,
                null);
            int after = player.GetPower<ChainsOfBindingPower>()?.Amount ?? 0;
            int applied = Math.Max(0, after - before);
            if (applied > 0 && player.Player != null)
            {
                contributions[player.Player.NetId] = applied;
            }
        }

        ChainsContributionByPlayerNetId = SerializeContributions(
            contributions);
    }

    private async Task RemoveChainsContribution(int amount)
    {
        if (Creature.CombatState == null)
        {
            return;
        }

        Dictionary<ulong, int> contributions = ParseContributions();
        foreach (Creature player in Creature.CombatState.PlayerCreatures
                     .OrderBy(static player => player.CombatId))
        {
            if (player.Player != null
                && contributions.TryGetValue(
                    player.Player.NetId,
                    out int contribution)
                && contribution > 0
                && player.GetPower<ChainsOfBindingPower>() is { } chains)
            {
                await PowerCmdCompat.ModifyAmount(
                    chains,
                    -Math.Min(amount, contribution),
                    Creature,
                    null,
                    silent: true);
            }
        }

        ChainsContributionByPlayerNetId = string.Empty;
    }

    private Dictionary<ulong, int> ParseContributions()
    {
        var result = new Dictionary<ulong, int>();
        foreach (string item in ChainsContributionByPlayerNetId.Split(
                     ';',
                     StringSplitOptions.RemoveEmptyEntries
                     | StringSplitOptions.TrimEntries))
        {
            int separator = item.IndexOf(':');
            if (separator <= 0
                || !ulong.TryParse(
                    item[..separator],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out ulong netId)
                || !int.TryParse(
                    item[(separator + 1)..],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int amount)
                || amount <= 0)
            {
                continue;
            }

            result[netId] = amount;
        }

        return result;
    }

    private static string SerializeContributions(
        IReadOnlyDictionary<ulong, int> contributions) =>
        string.Join(
            ';',
            contributions
                .OrderBy(static pair => pair.Key)
                .Select(static pair =>
                    pair.Key.ToString(CultureInfo.InvariantCulture)
                    + ":"
                    + pair.Value.ToString(CultureInfo.InvariantCulture)));

    private async Task ClearDebuffs()
    {
        PowerModel[] debuffs = Creature.Powers
            .Where(static power =>
                power.TypeForCurrentAmount == PowerType.Debuff)
            .ToArray();
        foreach (PowerModel debuff in debuffs)
        {
            await PowerCmd.Remove(debuff);
        }
    }

    private static int ResolveDefenseThorns() =>
        new IoriAscensionValue(7, 9).Resolve();

    private static LibraryDamageType ResolveStanceDamageType(
        IoriStance stance) => stance switch
    {
        IoriStance.Slash => LibraryDamageType.Slash,
        IoriStance.Pierce => LibraryDamageType.Pierce,
        IoriStance.Blunt => LibraryDamageType.Blunt,
        _ => LibraryDamageType.Slash,
    };

    private decimal ClampStageOneHpLoss(
        Creature target,
        decimal amount)
    {
        if (IsSecondStage
            || target != Creature
            || amount <= 0m
            || EscapeCompleted)
        {
            return amount;
        }

        decimal threshold = ResolveEscapeThreshold();
        decimal maxLoss = Math.Max(0m, target.CurrentHp - threshold);
        return Math.Min(amount, maxLoss);
    }

    internal decimal ClampFinalStageOneHpLoss(decimal amount) =>
        ClampStageOneHpLoss(Creature, amount);

    public decimal ClampFinalHpLoss(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource) =>
        ClampStageOneHpLoss(target, amount);

    public override async Task AfterCurrentHpChanged(
        Creature creature,
        decimal delta)
    {
        await base.AfterCurrentHpChanged(creature, delta);
        TryQueueEscapeAfterHpChange(creature, delta);
    }

    public override async Task AfterCurrentHpChanged(
        Creature creature,
        decimal delta,
        LibraryDamageType type)
    {
        await base.AfterCurrentHpChanged(creature, delta, type);
        TryQueueEscapeAfterHpChange(creature, delta);
    }

    private void TryQueueEscapeAfterHpChange(
        Creature creature,
        decimal delta)
    {
        if (!IsSecondStage
            && creature == Creature
            && delta < 0m
            && creature.CurrentHp <= ResolveEscapeThreshold())
        {
            QueueEscape();
        }
    }

    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        LibraryDamageType type)
    {
        await base.AfterDamageReceived(
            choiceContext,
            target,
            result,
            props,
            dealer,
            cardSource,
            type);

        if (target != Creature)
        {
            return;
        }

        if (!IsSecondStage
            && Creature.CurrentHp <= ResolveEscapeThreshold())
        {
            QueueEscape();
            _shouldLockHealthBar = true;
        }

        if (result.WasFullyBlocked)
        {
            LocalOggOneShotPlayer.Play(
                IoriSpecialGuestIds.CombatAudioRoot + "Purple_Warp.ogg",
                -2f);
            await CreatureCmd.TriggerAnim(Creature, "Evade", 0f);
            await Cmd.Wait(IoriAnimationContract.ActionDurationSeconds);
        }
        else if (result.TotalDamage > 0m)
        {
            await Cmd.Wait(IoriAnimationContract.ActionDurationSeconds);
        }
    }

    private void QueueEscape()
    {
        if (IsSecondStage || EscapeQueued || EscapeCompleted)
        {
            return;
        }

        EscapeQueued = true;
        RevealEscapeIntent();
    }

    private decimal ResolveEscapeThreshold() =>
        Math.Ceiling(Creature.MaxHp * 0.5m);

    private async Task PerformEscapeMove(
        IReadOnlyList<Creature> targets)
    {
        _shouldLockHealthBar = false;
        _ = targets;
        if (!EscapeQueued || EscapeCompleted || IsSecondStage)
        {
            return;
        }

        await PresentationGuard.RunAsync(PlayGuardAnimation, "Iori guard animation");
        await CompleteStageOneEscape();
    }

    public override async Task AfterSideTurnEndLate(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        await base.AfterSideTurnEndLate(
            choiceContext,
            side,
            participants);
        if (side == CombatSide.Enemy
            && EscapeQueued
            && !EscapeCompleted
            && !IsSecondStage)
        {
            await PresentationGuard.RunAsync(PlayGuardAnimation, "Iori guard animation");
            await CompleteStageOneEscape();
        }
    }

    private async Task CompleteStageOneEscape()
    {
        if (_escapeCompleting || EscapeCompleted || IsSecondStage)
        {
            return;
        }

        _escapeCompleting = true;
        try
        {
            SaveReceptionSnapshot();
            await CreatureCmd.Escape(Creature);
            EscapeCompleted = true;
        }
        finally
        {
            _escapeCompleting = false;
        }
    }

    private void SaveReceptionSnapshot()
    {
        if (Creature.CombatState?.RunState is not RunState runState)
        {
            return;
        }

        int round = Creature.CombatState.RoundNumber;
        var snapshot = new IoriReceptionSnapshot(
            Version: 1,
            EmotionLevel,
            EmotionUnits,
            IntentCapacity,
            LevelFiveRoundCounter,
            CurrentStance,
            HasExpandedRoundTwoCapacity,
            ReceptionRound: ReceptionRoundOffset + round,
            LastRegularMove,
            SelectedStanceMask,
            PositivePowers: Creature.Powers
                .Where(static power =>
                    power.TypeForCurrentAmount == PowerType.Buff)
                .OrderBy(static power => power.Id.ToString(), StringComparer.Ordinal)
                .ThenBy(static power => power.Amount)
                .Select(power => IoriPowerSnapshot.Capture(power, round))
                .ToArray());

        SpecialGuestRunStateModifier.GetOrCreate(runState).SetValue(
            IoriSpecialGuestIds.SnapshotValueKey,
            JsonSerializer.Serialize(snapshot));
    }

    private async Task<bool> TryRestoreReceptionSnapshot()
    {
        if (Creature.CombatState?.RunState is not RunState runState)
        {
            return false;
        }

        string? json = SpecialGuestRunStateModifier.TryGet(runState)
            ?.GetValue(IoriSpecialGuestIds.SnapshotValueKey);
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        IoriReceptionSnapshot? snapshot;
        try
        {
            snapshot = JsonSerializer.Deserialize<IoriReceptionSnapshot>(json);
        }
        catch (Exception exception)
        {
            Log.Error("[Iori] Invalid reception snapshot: " + exception);
            return false;
        }

        if (snapshot == null || snapshot.Version != 1)
        {
            return false;
        }

        CurrentStance = snapshot.CurrentStance;
        HasExpandedRoundTwoCapacity = snapshot.HasExpandedRoundTwoCapacity;
        ReceptionRoundOffset = Math.Max(0, snapshot.ReceptionRound);
        LastRegularMove = snapshot.LastRegularMove;
        SelectedStanceMask = snapshot.SelectedStanceMask;
        RestoreSharedReceptionState(
            snapshot.EmotionLevel,
            snapshot.EmotionUnits,
            snapshot.IntentCapacity,
            snapshot.LevelFiveRoundCounter);

        await CreatureCmd.SetCurrentHp(
            Creature,
            ResolveEscapeThreshold());
        if (Creature is LibraryCreature libraryCreature)
        {
            await LibraryCreatureCmd.SetCurrentChaoValue(
                libraryCreature,
                libraryCreature.MaxChaoValue);
        }

        foreach (IoriPowerSnapshot power in snapshot.PositivePowers)
        {
            await RestorePower(power);
        }

        if (CurrentStance == IoriStance.Blunt)
        {
            // Player combat state is rebuilt between stages; restore the
            // stance-owned Chains contribution on the new player creatures.
            await AddChainsContribution(2);
        }

        if (CurrentStance != IoriStance.None)
        {
            // The stance marker power is a neutral state and is not part of the
            // positive-power snapshot; restore it for the recovered stance.
            await ApplyStancePower(CurrentStance);
        }

        IoriCreatureVisuals.RefreshStance(Creature);
        return true;
    }

    private Task RestorePower(IoriPowerSnapshot snapshot)
    {
        PowerModel? canonical;
        try
        {
            canonical = ModelDb.GetByIdOrNull<PowerModel>(
                ModelId.Deserialize(snapshot.ModelId));
        }
        catch (Exception exception)
        {
            Log.Warn(
                "[Iori] Could not parse saved power id "
                + snapshot.ModelId
                + ": "
                + exception.Message);
            return Task.CompletedTask;
        }

        if (canonical == null || snapshot.Amount == 0)
        {
            return Task.CompletedTask;
        }

        PowerModel mutable = canonical.ToMutable();
        if (mutable is LibraryDurationPowerModel duration)
        {
            duration.SetTurnsRemaining(
                snapshot.DurationTurns,
                notifyDisplay: false);
        }

        // Restore the serialized state directly. Replaying BeforeApplied for
        // powers such as temporary Strength would duplicate their companion
        // power, which is already present in this same positive-power snapshot.
        mutable.Applier = Creature;
        mutable.ApplyInternal(
            Creature,
            snapshot.Amount,
            silent: true);
        mutable.SkipNextDurationTick = snapshot.SkipNextDurationTick;

        if (mutable is LibraryTurnsPowerModel turnsPower)
        {
            int round = Creature.CombatState?.RoundNumber ?? 0;
            turnsPower.AmountPlan = snapshot.TurnPlan
                .GroupBy(entry =>
                    round + Math.Max(0, entry.RoundOffset))
                .ToDictionary(
                    static group => group.Key,
                    static group => group.Sum(entry => entry.Amount))
                .ToSortedDictionary();
        }

        return Task.CompletedTask;
    }
}

public sealed class IoriStageOne : IoriMonsterBase
{
    protected override bool IsSecondStage => false;
}

public sealed class IoriStageTwo : IoriMonsterBase
{
    protected override bool IsSecondStage => true;
}

internal sealed class IoriTypedAttackIntent : AttackIntent
{
    private readonly IoriMonsterBase _iori;
    private readonly Func<int> _baseDamage;
    private readonly Func<LibraryDamageType> _damageType;
    private readonly Func<int> _repeats;

    internal IoriTypedAttackIntent(
        IoriMonsterBase iori,
        Func<int> baseDamage,
        Func<LibraryDamageType> damageType,
        Func<int> repeats)
    {
        _iori = iori;
        _baseDamage = baseDamage;
        _damageType = damageType;
        _repeats = repeats;
        DamageCalc = static () => 0m;
    }

    public override int Repeats => Math.Max(1, _repeats());

    protected override LocString IntentLabelFormat =>
        Repeats > 1
            ? new LocString("intents", "FORMAT_DAMAGE_MULTI")
            : new LocString("intents", "FORMAT_DAMAGE_SINGLE");

    public override int GetTotalDamage(
        IEnumerable<Creature> targets,
        Creature owner) => PreviewDamage(owner) * Repeats;

    public override LocString GetIntentLabel(
        IEnumerable<Creature> targets,
        Creature owner)
    {
        var label = IntentLabelFormat;
        label.Add("Damage", PreviewDamage(owner));
        if (Repeats > 1)
        {
            label.Add("Repeat", Repeats);
        }
        return label;
    }

    protected override LocString GetIntentDescription(
        IEnumerable<Creature> targets,
        Creature owner)
    {
        LocString description = BadgedIntentDescription.Create(
            null,
            owner,
            "ATTACK");
        description.Add("Damage", PreviewDamage(owner));
        description.Add("Repeat", Repeats);
        return description;
    }

    private int PreviewDamage(Creature owner) =>
        _iori.ResolveTypedPreviewDamage(
            _baseDamage(),
            _damageType(),
            IoriIntentPreviewTarget.Resolve(owner));
}

internal enum IoriTypedCombinedAttackKind
{
    Buff,
    Debuff,
    Defend,
}

internal sealed class IoriTypedCombinedAttackIntent :
    CombinedAttackIntentBase
{
    private readonly IoriMonsterBase _iori;
    private readonly Func<int> _baseDamage;
    private readonly Func<LibraryDamageType> _damageType;
    private readonly IoriTypedCombinedAttackKind _kind;

    internal IoriTypedCombinedAttackIntent(
        IoriMonsterBase iori,
        Func<int> baseDamage,
        Func<LibraryDamageType> damageType,
        Func<int> repeats,
        IoriTypedCombinedAttackKind kind,
        int blockAmount,
        params IntentBadge[] badges)
        : base(static () => 0m, repeats, null, badges)
    {
        _iori = iori;
        _baseDamage = baseDamage;
        _damageType = damageType;
        _kind = kind;
        BlockAmount = blockAmount;
    }

    internal int BlockAmount { get; }

    protected override string AttackKind => _kind switch
    {
        IoriTypedCombinedAttackKind.Buff =>
            CombinedIntentAnimData.AttackBuff,
        IoriTypedCombinedAttackKind.Debuff =>
            CombinedIntentAnimData.AttackDebuff,
        _ => CombinedIntentAnimData.AttackDefend,
    };

    protected override string IntentPrefix => _kind switch
    {
        IoriTypedCombinedAttackKind.Buff => "COMBINED_ATTACK_BUFF",
        IoriTypedCombinedAttackKind.Debuff => "COMBINED_ATTACK_DEBUFF",
        _ => "COMBINED_ATTACK_DEFEND",
    };

    public override int GetTotalDamage(
        IEnumerable<Creature> targets,
        Creature owner) => PreviewDamage(owner) * Repeats;

    public override LocString GetIntentLabel(
        IEnumerable<Creature> targets,
        Creature owner)
    {
        LocString label = Repeats > 1
            ? new LocString("intents", "FORMAT_DAMAGE_MULTI")
            : new LocString("intents", "FORMAT_DAMAGE_SINGLE");
        label.Add("Damage", PreviewDamage(owner));
        if (Repeats > 1)
        {
            label.Add("Repeat", Repeats);
        }
        return label;
    }

    protected override LocString GetIntentDescription(
        IEnumerable<Creature> targets,
        Creature owner)
    {
        LocString description = BadgedIntentDescription.Create(
            null,
            owner,
            IntentPrefix);
        description.Add("Damage", PreviewDamage(owner));
        description.Add("Repeat", Repeats);
        if (BlockAmount > 0)
        {
            description.Add("Amount", BlockAmount);
            description.Add("BlockAmount", BlockAmount);
        }
        BadgedIntentDescription.AddBadgeVariables(description, Effects);
        return description;
    }

    private int PreviewDamage(Creature owner) =>
        _iori.ResolveTypedPreviewDamage(
            _baseDamage(),
            _damageType(),
            IoriIntentPreviewTarget.Resolve(owner));
}

internal static class IoriIntentPreviewTarget
{
    internal static Creature? Resolve(Creature owner)
    {
        if (owner.CombatState == null)
        {
            return null;
        }

        return LocalContextCompat.GetMe(owner.CombatState)?.Creature
            is { IsAlive: true } local
                ? local
                : owner.CombatState.PlayerCreatures.FirstOrDefault(
                    static creature => creature.IsAlive);
    }
}

internal sealed record IoriReceptionSnapshot(
    int Version,
    int EmotionLevel,
    int EmotionUnits,
    int IntentCapacity,
    int LevelFiveRoundCounter,
    IoriStance CurrentStance,
    bool HasExpandedRoundTwoCapacity,
    int ReceptionRound,
    int LastRegularMove,
    int SelectedStanceMask,
    IReadOnlyList<IoriPowerSnapshot> PositivePowers);

internal sealed record IoriPowerSnapshot(
    string ModelId,
    int Amount,
    bool SkipNextDurationTick,
    int DurationTurns,
    IReadOnlyList<IoriTurnPlanSnapshot> TurnPlan)
{
    internal static IoriPowerSnapshot Capture(
        PowerModel power,
        int currentRound)
    {
        int durationTurns = power is LibraryDurationPowerModel duration
            ? duration.TurnsRemaining
            : 0;
        IoriTurnPlanSnapshot[] plan = power is LibraryTurnsPowerModel turns
            ? turns.AmountPlan
                .OrderBy(static entry => entry.Key)
                .Select(entry => new IoriTurnPlanSnapshot(
                    Math.Max(0, entry.Key - currentRound),
                    entry.Value))
                .ToArray()
            : [];
        return new(
            power.Id.ToString(),
            power.Amount,
            power.SkipNextDurationTick,
            durationTurns,
            plan);
    }
}

internal sealed record IoriTurnPlanSnapshot(
    int RoundOffset,
    int Amount);

[HarmonyPatch(
    typeof(Creature),
    nameof(Creature.LoseHpInternal), typeof(decimal), typeof(ValueProp))]
[HarmonyPriority(Priority.Last)]
internal static class IoriStageOneFinalHpFloorPatch
{
    [HarmonyPrefix]
    private static void Prefix(Creature __instance, ref decimal amount)
    {
        if (amount > 0m
            && __instance.Monster is IoriStageOne iori
            && !iori.EscapeCompleted)
        {
            amount = iori.ClampFinalStageOneHpLoss(amount);
        }
    }
}

internal static class IoriSpecialGuestAssets
{
    internal static readonly string[] CombatAudio =
    [
        IoriSpecialGuestIds.CombatAudioRoot + "Purple_Slash_Hori.ogg",
        IoriSpecialGuestIds.CombatAudioRoot + "Purple_Slash_VertDown.ogg",
        IoriSpecialGuestIds.CombatAudioRoot + "Purple_Slash_VertUp.ogg",
        IoriSpecialGuestIds.CombatAudioRoot + "Purple_Stab_Stab1.ogg",
        IoriSpecialGuestIds.CombatAudioRoot + "Purple_Stab_Stab2.ogg",
        IoriSpecialGuestIds.CombatAudioRoot + "Purple_Hit_Hori.ogg",
        IoriSpecialGuestIds.CombatAudioRoot + "Purple_Hit_Vert.ogg",
        IoriSpecialGuestIds.CombatAudioRoot + "Purple_Guard.ogg",
        IoriSpecialGuestIds.CombatAudioRoot + "Purple_Warp.ogg",
    ];

    internal static readonly string[] PowerIcons =
    [
        "res://images/powers/library_passive_orange.png",
        "res://images/powers/iori_probability_fluctuation_passive_power.png",
        "res://images/powers/iori_dimensional_walk_passive_power.png",
        "res://images/powers/iori_stance_shift_passive_power.png",
        "res://images/powers/iori_card_play_pain_power.png",
        "res://images/powers/iori_slash_stance_power.png",
        "res://images/powers/iori_pierce_stance_power.png",
        "res://images/powers/iori_blunt_stance_power.png",
        "res://images/powers/iori_defense_stance_power.png",
    ];

    internal static readonly string[] All =
        IoriPresentationAssets.All
        .Concat(PowerIcons)
        .Distinct(StringComparer.Ordinal)
        .ToArray();
}

internal static class SortedDictionaryExtensions
{
    internal static SortedDictionary<TKey, TValue> ToSortedDictionary<TKey, TValue>(
        this IDictionary<TKey, TValue> source)
        where TKey : notnull => new(source);
}
