using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.relics;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.abnormalities.JudgementBird;

public enum JudgementBirdPageMode
{
    None = 0,
    WeightOfSin = 1,
    Judgement = 2,
    TiltedScale = 3
}

public sealed class JudgementBirdPageRelic : ModalPageRelic<JudgementBirdPageMode>
{
    public const int WeightOfSinSelfDamage = 1;
    public const int WeightOfSinStrong = 5;
    public const int WeightOfSinStrongTurns = 1;
    public const int JudgementSinMultiplier = 2;
    public const int JudgementSinLossPercent = 70;
    public const int TiltedScaleHeal = 3;
    public const int TiltedScaleMaxTriggers = 4;

    private Dictionary<Creature, Queue<bool>>
        _highestEnemySnapshots = [];

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _highestEnemySnapshots = _highestEnemySnapshots.ToDictionary(
            static pair => pair.Key,
            static pair => new Queue<bool>(pair.Value));
    }

    protected override string IconBaseName =>
        "judgement_bird_page_relic";

    public override RelicRarity Rarity => RelicRarity.Event;

    public override bool IsAllowed(IRunState runState) => false;

    public override bool ShowCounter =>
        CombatManager.Instance.IsInProgress
        && Mode == JudgementBirdPageMode.TiltedScale;

    public override int DisplayAmount =>
        Mode == JudgementBirdPageMode.TiltedScale
            ? Math.Max(
                0,
                TiltedScaleMaxTriggers
                - TiltedScaleTriggersThisTurn)
            : 0;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Mode", (int)JudgementBirdPageMode.None),
        new DynamicVar("SelfDamage", WeightOfSinSelfDamage),
        new DynamicVar("Strong", WeightOfSinStrong),
        new DynamicVar("Turns", WeightOfSinStrongTurns),
        new DynamicVar("SinMultiplier", JudgementSinMultiplier),
        new DynamicVar("SinLossPercent", JudgementSinLossPercent),
        new DynamicVar("Heal", TiltedScaleHeal),
        new DynamicVar("MaxTriggers", TiltedScaleMaxTriggers),
        new DynamicVar("Remaining", TiltedScaleMaxTriggers)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        Mode switch
        {
            JudgementBirdPageMode.WeightOfSin =>
            [
                HoverTipFactory.FromPower<LibraryStrongPower>()
            ],
            JudgementBirdPageMode.Judgement =>
            [
                HoverTipFactory.FromPower<JudgementBirdSinPower>(),
                HoverTipFactory.Static(StaticHoverTip.Stun)
            ],
            _ => []
        };

    [SavedProperty]
    public JudgementBirdPageMode Mode { get; private set; }

    protected override JudgementBirdPageMode SelectedMode
    {
        get => Mode;
        set => Mode = value;
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int TiltedScaleTriggersThisTurn { get; private set; }

    public override Task BeforeCombatStart()
    {
        EnsureValidModeOrFallback(nameof(BeforeCombatStart));
        ResetTransientCombatState();
        UpdateModeUiState();
        return Task.CompletedTask;
    }

    public override Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (TurnParticipants.IsOwnTurn(Owner.Creature, side, participants))
        {
            TiltedScaleTriggersThisTurn = 0;
            _highestEnemySnapshots.Clear();
            UpdateModeUiState();
        }

        return Task.CompletedTask;
    }

    public override Task BeforeDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (Mode != JudgementBirdPageMode.TiltedScale
            || target.IsDead
            || !IsEnemy(target)
            || !IsOwnerAttackSource(dealer, cardSource)
            || !ValuePropCompat.IsPoweredAttack(props)
            || Owner.Creature.CombatState == null)
        {
            return Task.CompletedTask;
        }

        int highestHp = Owner.Creature.CombatState.Enemies
            .Where(static enemy => enemy.IsAlive && AllyTurnRegistry.IsPlayerEnemy(enemy))
            .Select(static enemy => enemy.CurrentHp)
            .DefaultIfEmpty(int.MinValue)
            .Max();
        if (!_highestEnemySnapshots.TryGetValue(
                target,
                out Queue<bool>? snapshots))
        {
            snapshots = new Queue<bool>();
            _highestEnemySnapshots[target] = snapshots;
        }

        snapshots.Enqueue(target.CurrentHp >= highestHp);
        return Task.CompletedTask;
    }

    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        bool ownerAttack =
            IsOwnerAttackSource(dealer, cardSource)
            && ValuePropCompat.IsPoweredAttack(props)
            && IsEnemy(target);
        if (!ownerAttack)
        {
            return;
        }

        if (Mode == JudgementBirdPageMode.Judgement
            && target.IsAlive
            && result.TotalDamage > 0)
        {
            Flash();
            await JudgementBirdSinService.ApplyLocked(
                choiceContext,
                target,
                result.TotalDamage * JudgementSinMultiplier,
                Owner.Creature,
                cardSource);
        }

        if (Mode != JudgementBirdPageMode.TiltedScale)
        {
            return;
        }

        bool wasHighest = TryDequeueHighestSnapshot(target);
        if (!wasHighest
            || result.TotalDamage <= 0
            || TiltedScaleTriggersThisTurn
                >= TiltedScaleMaxTriggers)
        {
            return;
        }

        Creature? lowestPlayer = Owner.Creature.CombatState?.Players
            .Select(static player => player.Creature)
            .Where(static creature => creature.IsAlive)
            .OrderBy(static creature => creature.CurrentHp)
            .ThenBy(static creature => creature.CombatId ?? uint.MaxValue)
            .FirstOrDefault();
        if (lowestPlayer == null)
        {
            return;
        }

        TiltedScaleTriggersThisTurn++;
        Flash();
        await CreatureCmd.Heal(lowestPlayer, TiltedScaleHeal);
        UpdateModeUiState();
    }

    public override async Task AfterAttack(
        PlayerChoiceContext choiceContext,
        AttackCommand command)
    {
        try
        {
            if (Mode != JudgementBirdPageMode.WeightOfSin
                || !IsOwnerAttack(command))
            {
                return;
            }

            bool causedHpLoss = command.Results
                .SelectMany(static hit => hit)
                .Any(static result => result.UnblockedDamage > 0);
            if (causedHpLoss || Owner.Creature.IsDead)
            {
                return;
            }

            Flash();
            await CreatureCmdCompat.Damage(
                choiceContext,
                Owner.Creature,
                WeightOfSinSelfDamage,
                ValueProp.Unpowered,
                Owner.Creature,
                null);
            if (Owner.Creature.IsAlive)
            {
                await LibraryPowerCmd.Apply<LibraryStrongPower>(
                    Owner.Creature,
                    WeightOfSinStrong,
                    WeightOfSinStrongTurns,
                    Owner.Creature,
                    null);
            }
        }
        finally
        {
            _highestEnemySnapshots.Clear();
        }
    }

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (Mode != JudgementBirdPageMode.Judgement
            || !TurnParticipants.IsOwnTurn(Owner.Creature, side, participants)
            || Owner.Creature.CombatState == null)
        {
            return;
        }

        foreach (Creature enemy in Owner.Creature.CombatState.Enemies
            .Where(static enemy => enemy.IsAlive && AllyTurnRegistry.IsPlayerEnemy(enemy))
            .OrderBy(static enemy => enemy.CombatId ?? uint.MaxValue)
            .ToArray())
        {
            int sin = JudgementBirdSinService.GetTotal(enemy);
            if (sin < enemy.CurrentHp || sin <= 0)
            {
                continue;
            }

            Flash();
            if (enemy is LibraryCreature
                {
                    HasChaoResistance: true
                } libraryEnemy)
            {
                await LibraryCreatureCmd.SetCurrentChaoValue(
                    libraryEnemy,
                    0);
            }
            else
            {
                await CreatureCmd.Stun(enemy);
            }

            if (enemy.IsAlive)
            {
                await JudgementBirdSinService.HalveWithFloorLoss(
                    choiceContext,
                    enemy,
                    Owner.Creature);
            }
        }
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        ResetTransientCombatState();
        UpdateModeUiState();
        return Task.CompletedTask;
    }

    protected override IReadOnlyList<CardModel> CreateModeChoiceCards() =>
    [
        Owner.RunState.CreateCard<
            JudgementBirdWeightOfSinChoiceCard>(Owner),
        Owner.RunState.CreateCard<
            JudgementBirdJudgementChoiceCard>(Owner),
        Owner.RunState.CreateCard<
            JudgementBirdTiltedScaleChoiceCard>(Owner)
    ];

    private void ResetTransientCombatState()
    {
        TiltedScaleTriggersThisTurn = 0;
        _highestEnemySnapshots.Clear();
    }

    private bool TryDequeueHighestSnapshot(Creature target)
    {
        if (!_highestEnemySnapshots.TryGetValue(
                target,
                out Queue<bool>? snapshots)
            || snapshots.Count == 0)
        {
            return false;
        }

        bool result = snapshots.Dequeue();
        if (snapshots.Count == 0)
        {
            _highestEnemySnapshots.Remove(target);
        }

        return result;
    }

    private bool IsEnemy(Creature target) =>
        Owner?.Creature != null
        && target.Side != Owner.Creature.Side
        && !AllyTurnRegistry.IsFriendlyAlly(target);

    private bool IsOwnerAttackSource(
        Creature? dealer,
        CardModel? cardSource) =>
        Owner?.Creature != null
        && (dealer == Owner.Creature
            || cardSource?.Owner == Owner);

    private bool IsOwnerAttack(AttackCommand command) =>
        Owner?.Creature != null
        && (command.Attacker == Owner.Creature
            || command.ModelSource is CardModel card
                && card.Owner == Owner);

    protected override void ResetStateOnModeSet(JudgementBirdPageMode mode) => ResetTransientCombatState();

    protected override void ResetStateOnFallback() => ResetTransientCombatState();

    protected override void UpdateModeUiState()
    {
        DynamicVars["Mode"].BaseValue = (int)Mode;
        DynamicVars["Remaining"].BaseValue = DisplayAmount;
        Status = ShowCounter && DisplayAmount <= 0
            ? RelicStatus.Disabled
            : RelicStatus.Normal;
        InvokeDisplayAmountChanged();
    }
}
