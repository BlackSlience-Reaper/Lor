using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.interop;
using ISecondaryDisplayAmountPower = LibraryOfRuina.framework.powers.ISecondaryDisplayAmountPower;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.abnormalities.PriceOfSilence;

public abstract class PriceOfSilencePowerModel : LibraryOfRuinaPowerModel
{
    protected abstract string IconFileName { get; }

    public override string PackedIconPath => ImageHelper.GetImagePath("powers/" + IconFileName);

    public override string ResolvedBigIconPath => PackedIconPath;
}

public sealed class PriceOfSilencePassivePower : PriceOfSilencePowerModel
{
    protected override string LegacyPowerId => "PRICE_OF_SILENCE_PASSIVE_POWER";

    protected override string IconFileName => "price_of_silence_passive_power.png";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("CountdownTurns", PriceOfSilence.CountdownTurns),
        new DynamicVar("RequiredTraces", PriceOfSilence.RequiredDestroyedTraces)
    ];
}

public sealed class PriceOfSilenceYourTimePassivePower : PriceOfSilencePowerModel
{
    protected override string LegacyPowerId => "PRICE_OF_SILENCE_YOUR_TIME_PASSIVE_POWER";

    protected override string IconFileName => "price_of_silence_your_time_passive_power.png";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;
}

public sealed class TimeTraceRestorationPower : LibraryFakeDeathPowerModel
{
    protected override string LegacyPowerId => "TIME_TRACE_RESTORATION_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    public override string PackedIconPath => ImageHelper.GetImagePath("powers/time_trace_restoration_power.png");

    public override string ResolvedBigIconPath => PackedIconPath;

    protected override bool IsVisibleInternal => false;

    protected override bool IsOwnerFakeDead =>
        Owner.Monster is TimeTrace { IsFakeDead: true };

    protected override bool CanEnterFakeDeath(Creature creature) =>
        Owner.Monster is TimeTrace trace && trace.CanEnterFakeDeath(creature);

    protected override Task EnterFakeDeath(Creature creature) =>
        Owner.Monster is TimeTrace trace ? trace.EnterFakeDeath() : Task.CompletedTask;
}

public sealed class PriceOfSilenceSilencePower : PriceOfSilencePowerModel
{
    protected override string LegacyPowerId => "PRICE_OF_SILENCE_SILENCE_POWER";

    protected override string IconFileName => "price_of_silence_silence_power.png";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Enemy)
        {
            if (this != null) await PowerCmd.Remove(this);
        }
    }
}

public sealed class AccumulatedTimePower : PriceOfSilencePowerModel
{
    protected override string LegacyPowerId => "ACCUMULATED_TIME_POWER";

    protected override string IconFileName => "accumulated_time_power.png";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override int DisplayAmount => Math.Max(0, Amount - 1);

    public override bool AllowNegative => true;

    public override Color AmountLabelColor => _normalAmountLabelColor;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DestroyedTracesVar()
    ];

    private sealed class DestroyedTracesVar : DynamicVar
    {
        public DestroyedTracesVar() : base("DestroyedTraces", 0m)
        {
        }

        protected override decimal GetBaseValueForIConvertible()
        {
            return _owner is AccumulatedTimePower power
                ? power.DisplayAmount
                : base.GetBaseValueForIConvertible();
        }

        public override string ToString()
        {
            return GetBaseValueForIConvertible().ToString();
        }
    }
}

public sealed class UnstoppableTimePower : PriceOfSilencePowerModel
{
    protected override string LegacyPowerId => "UNSTOPPABLE_TIME_POWER";

    protected override string IconFileName => "unstoppable_time_power.png";

    public override PowerType Type => PowerType.None;

    //public override int DisplayAmount => base.DisplayAmount;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("CountdownTurns", PriceOfSilence.CountdownTurns),
        new DynamicVar("RequiredTraces", PriceOfSilence.RequiredDestroyedTraces)
    ];
}

public sealed class TickingAttackPower : PriceOfSilencePowerModel
{
    protected override string LegacyPowerId => "TICKING_ATTACK_POWER";

    protected override string IconFileName => "ticking_attack_power.png";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

#if STS2_0_111_0
    public override decimal ModifyDamageAdditive(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource, CardPlay? cardPlay)
#else
    public override decimal ModifyDamageAdditive(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
#endif
    {
        if (dealer != Owner || !ValuePropCompat.IsPoweredAttack(props))
        {
            return 0m;
        }

        return Math.Max(0m, PriceOfSilence.TickingAttackMinimumDamage - amount);
    }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("MinimumDamage", PriceOfSilence.TickingAttackMinimumDamage)
    ];
}

public sealed class TickingGuardPower : PriceOfSilencePowerModel
{
    protected override string LegacyPowerId => "TICKING_GUARD_POWER";

    protected override string IconFileName => "ticking_guard_power.png";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    public override decimal ModifyBlockAdditive(
        Creature target,
        decimal block,
        ValueProp props,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        if (target != Owner || !ValuePropCompat.IsPoweredCardOrMonsterMoveBlock(props))
        {
            return 0m;
        }

        return Math.Max(0m, PriceOfSilence.TickingGuardMinimumBlock - block);
    }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("MinimumBlock", PriceOfSilence.TickingGuardMinimumBlock)
    ];
}

public sealed class TimeTraceMarkedPlayerPower : PriceOfSilencePowerModel, ISecondaryDisplayAmountPower
{
    protected override string LegacyPowerId => "TIME_TRACE_MARKED_PLAYER_POWER";

    protected override string IconFileName => "price_of_silence_your_time_passive_power.png";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override bool IsVisibleInternal => false;

    public override int DisplayAmount => CurrentDamage;

    public bool ShowSecondaryDisplayAmount => true;

    public int SecondaryDisplayAmount => CurrentBlock;

    public Color SecondaryDisplayAmountLabelColor => _normalAmountLabelColor;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new RecordedDamageVar(),
        new RecordedBlockVar(),
        new UsedSilenceVar()
    ];

    protected override object InitInternalData() => new Data();

    private int CurrentDamage
    {
        get
        {
            Data data = GetInternalData<Data>();
            return data.HasFrozenIntent ? data.IntentDamage : data.Damage;
        }
    }

    private int CurrentBlock
    {
        get
        {
            Data data = GetInternalData<Data>();
            return data.HasFrozenIntent ? data.IntentBlock : data.Block;
        }
    }

    private bool CurrentUsedSilence
    {
        get
        {
            Data data = GetInternalData<Data>();
            return data.HasFrozenIntent ? data.IntentUsedSilence : data.UsedSilence;
        }
    }

    public void ResetCounters()
    {
        Data data = GetInternalData<Data>();
        data.Damage = 0;
        data.Block = 0;
        data.UsedSilence = false;
        data.IntentDamage = 0;
        data.IntentBlock = 0;
        data.IntentUsedSilence = false;
        data.HasFrozenIntent = false;
        InvokeDisplayAmountChanged();
    }

    public TimeTraceSnapshot Snapshot()
    {
        Data data = GetInternalData<Data>();
        return new TimeTraceSnapshot(Owner.Player ?? Owner.PetOwner, data.Damage, data.Block, data.UsedSilence);
    }

    public TimeTraceSnapshot IntentSnapshot()
    {
        Data data = GetInternalData<Data>();
        return data.HasFrozenIntent
            ? new TimeTraceSnapshot(
                Owner.Player ?? Owner.PetOwner,
                data.IntentDamage,
                data.IntentBlock,
                data.IntentUsedSilence)
            : Snapshot();
    }

    public TimeTraceSnapshot FreezeForIntent()
    {
        Data data = GetInternalData<Data>();
        if (data.HasFrozenIntent)
        {
            return IntentSnapshot();
        }

        data.IntentDamage = data.Damage;
        data.IntentBlock = data.Block;
        data.IntentUsedSilence = data.UsedSilence;
        data.HasFrozenIntent = true;
        data.Damage = 0;
        data.Block = 0;
        data.UsedSilence = false;
        InvokeDisplayAmountChanged();
        return IntentSnapshot();
    }

    public void MarkSilenceUsed()
    {
        Data data = GetInternalData<Data>();
        if (data.UsedSilence)
        {
            return;
        }

        data.UsedSilence = true;
        InvokeDisplayAmountChanged();
    }

    public override Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        if (Owner.CombatState?.CurrentSide != CombatSide.Player
            || result.UnblockedDamage <= 0
            || target.Side == CombatSide.Player)
        {
            return Task.CompletedTask;
        }

        Player? player = ResolvePlayerSource(dealer, cardSource);
        if (player != Owner.Player)
        {
            return Task.CompletedTask;
        }

        Data data = GetInternalData<Data>();
        data.Damage += result.UnblockedDamage;
        InvokeDisplayAmountChanged();
        return Task.CompletedTask;
    }

    public override Task AfterBlockGained(
        Creature creature,
        decimal amount,
        ValueProp props,
        CardModel? cardSource)
    {
        if (Owner.CombatState?.CurrentSide != CombatSide.Player
            || amount <= 0m
            || creature.Player != Owner.Player && creature.PetOwner != Owner.Player)
        {
            return Task.CompletedTask;
        }

        Data data = GetInternalData<Data>();
        data.Block += (int)amount;
        InvokeDisplayAmountChanged();
        return Task.CompletedTask;
    }

    private static Player? ResolvePlayerSource(Creature? dealer, CardModel? cardSource)
    {
        if (cardSource?.Owner != null)
        {
            return cardSource.Owner;
        }

        return dealer?.Player ?? dealer?.PetOwner;
    }

    private sealed class RecordedDamageVar : DynamicVar
    {
        public RecordedDamageVar() : base("RecordedDamage", 0m)
        {
        }

        protected override decimal GetBaseValueForIConvertible() =>
            _owner is TimeTraceMarkedPlayerPower power
                ? power.CurrentDamage
                : base.GetBaseValueForIConvertible();

        public override string ToString() => GetBaseValueForIConvertible().ToString();
    }

    private sealed class RecordedBlockVar : DynamicVar
    {
        public RecordedBlockVar() : base("RecordedBlock", 0m)
        {
        }

        protected override decimal GetBaseValueForIConvertible() =>
            _owner is TimeTraceMarkedPlayerPower power
                ? power.CurrentBlock
                : base.GetBaseValueForIConvertible();

        public override string ToString() => GetBaseValueForIConvertible().ToString();
    }

    private sealed class UsedSilenceVar : StringVar
    {
        public UsedSilenceVar() : base("UsedSilence", "No")
        {
        }

        public override string ToString()
        {
            return _owner is TimeTraceMarkedPlayerPower power && power.CurrentUsedSilence
                ? "Yes"
                : "No";
        }
    }

    private sealed class Data
    {
        public int Damage;
        public int Block;
        public bool UsedSilence;
        public int IntentDamage;
        public int IntentBlock;
        public bool IntentUsedSilence;
        public bool HasFrozenIntent;
    }
}

public sealed class PriceOfSilenceEncounterTrackerPower : PriceOfSilencePowerModel, ISecondaryDisplayAmountPower
{
    private Dictionary<Player, int> _currentDamage = [];
    private Dictionary<Player, int> _currentBlock = [];
    private HashSet<Player> _currentSilenceUsers = [];

    private Dictionary<Player, int> _lastDamage = [];
    private Dictionary<Player, int> _lastBlock = [];
    private HashSet<Player> _lastSilenceUsers = [];

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _currentDamage = new(_currentDamage);
        _currentBlock = new(_currentBlock);
        _currentSilenceUsers = [.. _currentSilenceUsers];
        _lastDamage = new(_lastDamage);
        _lastBlock = new(_lastBlock);
        _lastSilenceUsers = [.. _lastSilenceUsers];
    }

    protected override string LegacyPowerId => "PRICE_OF_SILENCE_ENCOUNTER_TRACKER_POWER";

    protected override string IconFileName => "price_of_silence_your_time_passive_power.png";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override bool IsVisibleInternal => false;

    public override int DisplayAmount => CurrentDamageTotal;

    public bool ShowSecondaryDisplayAmount => true;

    public int SecondaryDisplayAmount => CurrentBlockTotal;

    public Color SecondaryDisplayAmountLabelColor => _normalAmountLabelColor;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CurrentDamageVar(),
        new CurrentBlockVar(),
        new LastDamageVar(),
        new LastBlockVar(),
        new SilenceUsersVar()
    ];

    private int CurrentDamageTotal => _currentDamage.Values.Sum();

    private int CurrentBlockTotal => _currentBlock.Values.Sum();

    private int LastDamageTotal => _lastDamage.Values.Sum();

    private int LastBlockTotal => _lastBlock.Values.Sum();

    public override Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (TurnParticipants.IsRoundPlayerTurn(side))
        {
            _lastDamage = new Dictionary<Player, int>(_currentDamage);
            _lastBlock = new Dictionary<Player, int>(_currentBlock);
            _lastSilenceUsers = new HashSet<Player>(_currentSilenceUsers);
            _currentDamage.Clear();
            _currentBlock.Clear();
            _currentSilenceUsers.Clear();
            InvokeDisplayAmountChanged();
        }

        return Task.CompletedTask;
    }

    public override Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        if (cardPlay.Card is SilenceStatusCard && cardPlay.Card.Owner != null)
        {
            _currentSilenceUsers.Add(cardPlay.Card.Owner);
            InvokeDisplayAmountChanged();
        }

        return Task.CompletedTask;
    }

    public override Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        if (result.UnblockedDamage <= 0 || target.Side == CombatSide.Player)
        {
            return Task.CompletedTask;
        }

        Player? player = ResolvePlayerSource(dealer, cardSource);
        if (player == null || !player.Creature.IsAlive)
        {
            return Task.CompletedTask;
        }

        _currentDamage[player] = _currentDamage.GetValueOrDefault(player) + result.UnblockedDamage;
        InvokeDisplayAmountChanged();
        return Task.CompletedTask;
    }

    public override Task AfterBlockGained(
        Creature creature,
        decimal amount,
        ValueProp props,
        CardModel? cardSource)
    {
        Player? player = creature.Player ?? creature.PetOwner;
        if (player != null && amount > 0m)
        {
            _currentBlock[player] = _currentBlock.GetValueOrDefault(player) + (int)amount;
            InvokeDisplayAmountChanged();
        }

        return Task.CompletedTask;
    }

    public TimeTraceSnapshot SnapshotForRandomLivingPlayer(CombatStateLike? combatState)
    {
        IReadOnlyList<Creature> livingPlayers = PriceOfSilenceEncounterHelper.LivingPlayers(combatState);
        if (livingPlayers.Count == 0)
        {
            return TimeTraceSnapshot.Empty;
        }

        Creature selected = (combatState?.RunState.Rng.MonsterAi.NextItem(livingPlayers)) ?? livingPlayers[0];
        Player? player = selected.Player ?? selected.PetOwner;
        if (player == null)
        {
            return TimeTraceSnapshot.Empty;
        }

        return SnapshotFor(player);
    }

    public TimeTraceSnapshot SnapshotFor(Player player)
    {
        return new TimeTraceSnapshot(
            player,
            _lastDamage.GetValueOrDefault(player),
            _lastBlock.GetValueOrDefault(player),
            _lastSilenceUsers.Contains(player));
    }

    public void MarkSilenceUsed(Player player)
    {
        _currentSilenceUsers.Add(player);
        InvokeDisplayAmountChanged();
    }

    private static Player? ResolvePlayerSource(Creature? dealer, CardModel? cardSource)
    {
        if (cardSource?.Owner != null)
        {
            return cardSource.Owner;
        }

        return dealer?.Player ?? dealer?.PetOwner;
    }

    private sealed class CurrentDamageVar : DynamicVar
    {
        public CurrentDamageVar() : base("CurrentDamage", 0m)
        {
        }

        protected override decimal GetBaseValueForIConvertible() =>
            _owner is PriceOfSilenceEncounterTrackerPower power
                ? power.CurrentDamageTotal
                : base.GetBaseValueForIConvertible();

        public override string ToString() => GetBaseValueForIConvertible().ToString();
    }

    private sealed class CurrentBlockVar : DynamicVar
    {
        public CurrentBlockVar() : base("CurrentBlock", 0m)
        {
        }

        protected override decimal GetBaseValueForIConvertible() =>
            _owner is PriceOfSilenceEncounterTrackerPower power
                ? power.CurrentBlockTotal
                : base.GetBaseValueForIConvertible();

        public override string ToString() => GetBaseValueForIConvertible().ToString();
    }

    private sealed class LastDamageVar : DynamicVar
    {
        public LastDamageVar() : base("LastDamage", 0m)
        {
        }

        protected override decimal GetBaseValueForIConvertible() =>
            _owner is PriceOfSilenceEncounterTrackerPower power
                ? power.LastDamageTotal
                : base.GetBaseValueForIConvertible();

        public override string ToString() => GetBaseValueForIConvertible().ToString();
    }

    private sealed class LastBlockVar : DynamicVar
    {
        public LastBlockVar() : base("LastBlock", 0m)
        {
        }

        protected override decimal GetBaseValueForIConvertible() =>
            _owner is PriceOfSilenceEncounterTrackerPower power
                ? power.LastBlockTotal
                : base.GetBaseValueForIConvertible();

        public override string ToString() => GetBaseValueForIConvertible().ToString();
    }

    private sealed class SilenceUsersVar : DynamicVar
    {
        public SilenceUsersVar() : base("SilenceUsers", 0m)
        {
        }

        protected override decimal GetBaseValueForIConvertible() =>
            _owner is PriceOfSilenceEncounterTrackerPower power
                ? power._currentSilenceUsers.Count
                : base.GetBaseValueForIConvertible();

        public override string ToString() => GetBaseValueForIConvertible().ToString();
    }
}

public readonly record struct TimeTraceSnapshot(
    Player? Player,
    int Damage,
    int Block,
    bool UsedSilence)
{
    public static TimeTraceSnapshot Empty => new(null, 0, 0, false);
}
