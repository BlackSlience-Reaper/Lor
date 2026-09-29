using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryLib.Hooks;
using LibraryOfRuina.intents;
using LibraryOfRuina.monsters;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.specialguests.Iori;

/// <summary>
/// 伊织两个阶段的共同实现。按职责分在几个 partial 文件里：本文件是战斗状态、行动状态机与每回合的计划；
/// <c>IoriMonsterBase.Moves.cs</c> 是招式执行；<c>IoriMonsterBase.Stance.cs</c> 是姿态切换（纯规则在
/// <see cref="IoriStanceController"/>）；<c>IoriMonsterBase.Escape.cs</c> 是一阶段撤离与二阶段读回快照
/// （快照的存取在 <see cref="IoriReceptionSnapshotStore"/>）。
/// </summary>
public abstract partial class IoriMonsterBase :
    SpecialGuestMonsterBase, LibraryOfRuina.infra.helpers.IFinalHpLossClamp
{
    internal const string RouterMoveId = "IORI_ROUTER";
    internal const string CompositeMoveId = "IORI_COMPOSITE";
    internal const string HiddenMoveId = "IORI_HIDDEN";
    internal const string EscapeMoveId = "IORI_ESCAPE";

    private PlannedMoveController<IoriMove>? _plan;
    private MoveState? _escapeState;
    private bool _escapeCompleting;
    private LibraryDamageType _activeDamageType = LibraryDamageType.None;
    private LibraryDamageType _previewDamageType = LibraryDamageType.None;
    private bool _shouldLockHealthBar;

    internal bool IsHealthBarLockActive => _shouldLockHealthBar;

    public IoriStance CurrentStance { get; private set; } = IoriStance.None;

    public int LastPlannedRound { get; private set; } = -1;

    public int LastRegularMove { get; private set; } = (int)IoriMove.None;

    public IoriStance PlannedNextStance { get; private set; } = IoriStance.None;

    public int SelectedStanceMask { get; private set; }

    public bool HasExpandedRoundTwoCapacity { get; private set; }

    public int ReceptionRoundOffset { get; private set; }

    public bool EscapeQueued { get; private set; }

    public bool EscapeCompleted { get; private set; }

    public bool StageSnapshotRestored { get; private set; }

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

    // 计划存在基类的五个槽位里；实例随怪物克隆丢弃、用到时重建（见 PlannedMoveController）。
    private PlannedMoveController<IoriMove> Plan => _plan ??= new(
        this,
        StoredIntentSlots,
        slot => (IoriMove)GetStoredIntent(slot),
        (slot, move) => SetStoredIntent(slot, (int)move),
        (_, move) => CreateIntent(move),
        IoriMove.None);

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
        _plan = null;
        _escapeState = null;
        _escapeCompleting = false;
        _activeDamageType = LibraryDamageType.None;
        _previewDamageType = LibraryDamageType.None;
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState compositeState = Plan.CreateCompositeState(
            CompositeMoveId,
            PerformCompositeMove,
            mustPerformOnce: true);
        MoveState hiddenState = Plan.CreateHiddenState(HiddenMoveId);
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
        compositeState.FollowUpState = router;
        hiddenState.FollowUpState = router;
        _escapeState.FollowUpState = _escapeState;

        return new MonsterMoveStateMachine(
            [compositeState, hiddenState, _escapeState, router],
            EscapeQueued
                ? _escapeState
                : HasStoredIntentPlan
                    ? compositeState
                    : hiddenState);
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

        // 读档回到已规划过的回合：只重建意图并揭示，不再掷一次计划（多掷会让 MonsterAi 与存档时分叉）。
        if (LastPlannedRound == combatState.RoundNumber
            && HasStoredIntentPlan)
        {
            Plan.RefreshIntents();
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
        Plan.WriteSlots(count, slot => moves[slot]);

        LastPlannedRound = roundNumber;
        HasCompletedFirstTurn = true;
        Plan.RefreshIntents();
        RevealPlan();
    }

    private async Task PerformCompositeMove(
        IReadOnlyList<Creature> targets)
    {
        _ = targets;
        await Plan.PerformPlan(
            () => Creature.IsAlive && CanPerformRegularMoves,
            (_, move) => PerformMove(move));

        ClearPlan();
        if (EscapeQueued && !EscapeCompleted)
        {
            RevealEscapeIntent();
        }
        else
        {
            Plan.Hide();
        }
    }

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
                ? IoriStanceController.ResolveDamageType(CurrentStance)
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
                : IoriStanceController.ResolveDamageType(CurrentStance);

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

    private void ClearPlan()
    {
        Plan.ClearSlots();
        Plan.RefreshIntents();
    }

    private void RevealPlan()
    {
        if (HasStoredIntentPlan && CanPerformRegularMoves)
        {
            Plan.Reveal();
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
}
