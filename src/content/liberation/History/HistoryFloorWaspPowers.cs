using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using LibraryLib.Combat.HealthBars;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.encounters;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.interop;
using LibraryOfRuina.ui;
using ISecondaryDisplayAmountPower = LibraryOfRuina.framework.powers.ISecondaryDisplayAmountPower;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.liberation.History;

public sealed class HistoryFloorWaspSporePower :
    LibraryOfRuinaPowerModel,
    ILibraryHealthBarDamageForecastSource
{
    private const int SpawnStackThreshold = 0;

    protected override string LegacyPowerId => "HISTORY_FLOOR_WASP_SPORE_POWER";

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public IEnumerable<LibraryHealthBarDamageForecast>
        GetLibraryHealthBarDamageForecasts(
            LibraryHealthBarForecastContext context) =>
        [
            new(
                Amount,
                LibraryOfRuina.ui.LibraryHealthBarForecastColors.Spore,
                ValueProp.Unpowered,
                Order: 20,
                Dealer: Owner)
        ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("SpawnThreshold", SpawnStackThreshold)
    ];

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, CombatStateLike combatState)
    {
        if (Owner.IsDead || side != Owner.Side || side != CombatSide.Player || Amount <= 0)
        {
            return;
        }

        if (combatState.Encounter is not ISporeWorkerSpawner spawner
            || !spawner.CanSpawnSporeWorkers)
        {
            await PowerCmd.Remove(this);
            return;
        }

        int damage = Math.Max(0, Amount);
        await CreatureCmdCompat.Damage(
            new ThrowingPlayerChoiceContext(),
            Owner,
            damage,
            ValueProp.Unpowered,
            Owner,
            null);

        int halved = damage / 2;
        if (halved != Amount)
        {
            await PowerCmdCompat.ModifyAmount(this, halved - Amount, Owner, null, silent: true);
        }

        if (halved > SpawnStackThreshold)
        {
            return;
        }

        await PowerCmd.Remove(this);
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        if (Owner.IsDead
            || cardPlay.Card.Owner.Creature != Owner
            || cardPlay.Card.Type != CardType.Attack
            || Amount <= 0)
        {
            return;
        }

        await CreatureCmdCompat.Damage(
            context,
            Owner,
            Amount,
            ValueProp.Unpowered,
            Owner,
            null);
    }
}

public sealed class HistoryFloorWaspParalysisPower : LibraryOfRuinaPowerModel, ISecondaryDisplayAmountPower
{
    private sealed class Data
    {
        public int TurnsRemaining = 1;
    }

    private sealed class TurnsVar : DynamicVar
    {
        public TurnsVar() : base("Turns", 0m)
        {
        }

        protected override decimal GetBaseValueForIConvertible()
        {
            return _owner is HistoryFloorWaspParalysisPower power ? power.TurnsRemainingForText : base.GetBaseValueForIConvertible();
        }

        public override string ToString()
        {
            return GetBaseValueForIConvertible().ToString();
        }
    }

    private sealed class PercentVar : DynamicVar
    {
        public PercentVar() : base("Percent", 0m)
        {
        }

        protected override decimal GetBaseValueForIConvertible()
        {
            return _owner is HistoryFloorWaspParalysisPower power
                ? power.Amount * 25m
                : base.GetBaseValueForIConvertible();
        }

        public override string ToString()
        {
            return GetBaseValueForIConvertible().ToString();
        }
    }

    protected override string LegacyPowerId => "HISTORY_FLOOR_WASP_PARALYSIS_POWER";

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new TurnsVar(),
        new PercentVar()
    ];

    public bool ShowSecondaryDisplayAmount => TurnsRemaining > 0;

    public int SecondaryDisplayAmount => TurnsRemaining;

    public Color SecondaryDisplayAmountLabelColor => _normalAmountLabelColor;

    protected override object InitInternalData()
    {
        return new Data();
    }

    private int TurnsRemaining => GetInternalData<Data>().TurnsRemaining;

    public int TurnsRemainingForText => TurnsRemaining;

    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource, CardPlay? cardPlay)
    {
        if (dealer != Owner || Amount <= 0 || !ValuePropCompat.IsPoweredAttack(props))
        {
            return 1m;
        }

        return Math.Max(0m, 1m - 0.25m * Amount);
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        if (Owner.IsDead
            || cardPlay.Card.Owner.Creature != Owner
            || !LibraryOfRuinaParalysisCardClassifier.CountsAsAttack(
                cardPlay.Card)
            || Amount <= 0)
        {
            return;
        }

        await PowerCmdCompat.ModifyAmount(this, -1m, Owner, null, silent: true);
        if (Amount <= 0)
        {
            await PowerCmd.Remove(this);
        }
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != Owner.Side)
        {
            return;
        }

        int turnsRemaining = Math.Max(0, TurnsRemaining - 1);
        SetTurnsRemaining(turnsRemaining);
        if (turnsRemaining <= 0)
        {
            await PowerCmd.Remove(this);
        }
    }

    public void SetTurnsRemaining(int turnsRemaining)
    {
        AssertMutable();
        GetInternalData<Data>().TurnsRemaining = Math.Max(0, turnsRemaining);
        InvokeDisplayAmountChanged();
    }
}

public sealed class HistoryFloorWaspExpansionPower : LibraryOfRuinaPowerModel
{
    private const int SporeAmount = 2;
    private const int WorkerBeeLimit = 2;

    protected override string LegacyPowerId => "HISTORY_FLOOR_WASP_EXPANSION_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Spore", SporeAmount),
        new DynamicVar("WorkerLimit", WorkerBeeLimit)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<HistoryFloorWaspSporePower>()
    ];

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, CombatStateLike combatState)
    {
        if (Owner.IsDead || side != Owner.Side || side != CombatSide.Enemy)
        {
            return;
        }

        int workerCount = combatState.Enemies.Count(static enemy => enemy.IsAlive && enemy.Monster is HistoryFloorWorkerBee);
        if (workerCount >= WorkerBeeLimit)
        {
            return;
        }

        IReadOnlyList<Creature> players = combatState.PlayerCreatures
            .Where(static player => player.IsAlive)
            .ToArray();
        if (players.Count == 0)
        {
            return;
        }

        await PowerCmdCompat.Apply<HistoryFloorWaspSporePower>(players, SporeAmount, Owner, null);
    }
}

public sealed class HistoryFloorWaspPheromonePower : LibraryOfRuinaPowerModel
{
    private const int WarlikeInterval = 3;
    private const int ReducedChao = 0;

    private sealed class Data
    {
        public int EnemyTurnCount;
        public bool WorkerStaggerBrokenForTurn;
        public bool WarlikeQueuedForNextEnemyTurn;
    }

    protected override string LegacyPowerId => "HISTORY_FLOOR_WASP_PHEROMONE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Interval", WarlikeInterval),
        new DynamicVar("ReducedChao", ReducedChao)
    ];

    private int TurnDisplay
    {
        get
        {
            int turnsUntilWarlike = GetInternalData<Data>().EnemyTurnCount % WarlikeInterval;
            return turnsUntilWarlike == 0 ? WarlikeInterval : turnsUntilWarlike;
        }
    }

    protected override object InitInternalData()
    {
        return new Data();
    }

    public override Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        Data data = GetInternalData<Data>();
        if (side == CombatSide.Player)
        {
            data.EnemyTurnCount++;
            data.WarlikeQueuedForNextEnemyTurn = false;
            SetAmount(TurnDisplay, silent: true);

            if (data.EnemyTurnCount % WarlikeInterval == 0
                && Owner is { IsAlive: true, Monster: HistoryFloorWaspBoss boss })
            {
                boss.QueueWarlikeEnhancementForNextRoll();
                data.WarlikeQueuedForNextEnemyTurn = true;
            }
        }

        return Task.CompletedTask;
    }

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, CombatStateLike combatState)
    {
        Data data = GetInternalData<Data>();

        if (side == CombatSide.Player && data.WorkerStaggerBrokenForTurn)
        {
            foreach (Creature worker in combatState.Enemies.Where(static enemy => enemy.IsAlive && enemy.Monster is HistoryFloorWorkerBee))
            {
                if (worker is LibraryCreature workerLc)
                {
                    await LibraryCreatureCmd.SetCurrentChaoValue(workerLc, HistoryFloorWorkerBee.StaggerResistance);
                }
            }

            data.WorkerStaggerBrokenForTurn = false;
            return;
        }

        if (Owner.IsDead || side != Owner.Side || side != CombatSide.Enemy)
        {
            return;
        }


        if (Owner.Monster is HistoryFloorWaspBoss boss && boss.BuffComboUsed)
        {
            if (Owner is LibraryCreature lc && lc.CurrentChaoValue > 0)
            {
                HistoryFloorLiberationBackgroundController.PlayWaspLoyaltyOverlay();
            }
            boss.BuffComboUsed = false;
        }

        if (!data.WarlikeQueuedForNextEnemyTurn
            || Owner.Monster is not HistoryFloorWaspBoss warlikeBoss
            || !warlikeBoss.IsWarlikeEnhancementQueuedForCurrentTurn)
        {
            return;
        }

        data.WarlikeQueuedForNextEnemyTurn = false;
        data.WorkerStaggerBrokenForTurn = true;
        foreach (Creature worker in combatState.Enemies.Where(static enemy => enemy.IsAlive && enemy.Monster is HistoryFloorWorkerBee))
        {
            if (worker is LibraryCreature workerLc && workerLc.CurrentChaoValue > 0)
            {
                await LibraryCreatureCmd.ChaoDamage(
                    new ThrowingPlayerChoiceContext(),
                    [workerLc],
                    workerLc.CurrentChaoValue,
                    ValueProp.Unblockable | ValueProp.Unpowered,
                    Owner,
                    null,
                    null);
            }
        }
    }
}
