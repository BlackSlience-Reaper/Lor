using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.compat;
using LibraryOfRuina.encounters.LittleRedMercenary;
using LibraryOfRuina.guests.DawnOffice;
using LibraryOfRuina.monsters.LittleRedMercenary;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.powers.LittleRedMercenary;

public sealed class TransferredBlockPower : LibraryOfRuinaPowerModel
{
    private sealed class Data
    {
        public bool EnemyTurnSeen;
        public bool PendingRemoval;
        public int SuppressTrackingDepth;
    }

    protected override string LegacyPowerId => "LITTLE_RED_TRANSFERRED_BLOCK_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override bool IsVisibleInternal => false;

    protected override object InitInternalData()
    {
        return new Data();
    }

    public static async Task TrackTransfer(Creature target, int amount, Creature? applier)
    {
        if (amount <= 0 || target.IsDead)
        {
            return;
        }

        TransferredBlockPower? existing = target.GetPower<TransferredBlockPower>();
        if (existing == null)
        {
            var mutable = (TransferredBlockPower)ModelDb.Power<TransferredBlockPower>().ToMutable();
            await PowerCmdCompat.Apply(mutable, target, amount, applier, null, silent: true);
            return;
        }

        existing.SetTrackedAmount(existing.Amount + amount);
    }

    public static int GetTrackedAmount(Creature? target)
    {
        return Math.Max(0, target?.GetPower<TransferredBlockPower>()?.Amount ?? 0);
    }

    public static void SuspendTrackingWhile(Creature? target, Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        TransferredBlockPower? power = target?.GetPower<TransferredBlockPower>();
        if (power == null)
        {
            action();
            return;
        }

        power.PushTrackingSuppression();
        try
        {
            action();
        }
        finally
        {
            power.PopTrackingSuppression();
        }
    }

    public static void Clear(Creature? target)
    {
        TransferredBlockPower? power = target?.GetPower<TransferredBlockPower>();
        if (power == null)
        {
            return;
        }

        power.DetachOwnerBlockTracking();
        power.RemoveInternal();
    }

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        Owner.BlockChanged += OnOwnerBlockChanged;
        return Task.CompletedTask;
    }

    public override Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side, IReadOnlyList<Creature> participants, CombatStateLike combatState)
    {
        if (side != CombatSide.Player || Owner.IsDead)
        {
            return Task.CompletedTask;
        }

        Data data = GetInternalData<Data>();
        if (!data.EnemyTurnSeen)
        {
            return Task.CompletedTask;
        }

        SetTrackedAmount(0);
        data.PendingRemoval = true;
        return Task.CompletedTask;
    }

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, CombatStateLike combatState)
    {
        Data data = GetInternalData<Data>();
        if (side == CombatSide.Enemy)
        {
            data.EnemyTurnSeen = true;
            return;
        }

        if (side == CombatSide.Player && data.PendingRemoval)
        {
            await RemoveImmediately();
        }
    }

    public override Task AfterRemoved(Creature oldOwner)
    {
        DetachOwnerBlockTracking();
        Data data = GetInternalData<Data>();
        data.EnemyTurnSeen = false;
        data.PendingRemoval = false;
        data.SuppressTrackingDepth = 0;
        return Task.CompletedTask;
    }

    private void OnOwnerBlockChanged(int oldBlock, int newBlock)
    {
        Data data = GetInternalData<Data>();
        if (data.SuppressTrackingDepth > 0 || newBlock >= oldBlock || Amount <= 0)
        {
            return;
        }

        int lostBlock = oldBlock - newBlock;
        SetTrackedAmount(Math.Max(0, Amount - lostBlock));
    }

    private void SetTrackedAmount(int amount)
    {
        SetAmount(Math.Max(0, amount), silent: true);
    }

    private void PushTrackingSuppression()
    {
        GetInternalData<Data>().SuppressTrackingDepth++;
    }

    private void PopTrackingSuppression()
    {
        Data data = GetInternalData<Data>();
        data.SuppressTrackingDepth = Math.Max(0, data.SuppressTrackingDepth - 1);
    }

    private void DetachOwnerBlockTracking()
    {
        Creature? owner = Owner;
        if (owner != null)
        {
            owner.BlockChanged -= OnOwnerBlockChanged;
        }
    }

    private async Task RemoveImmediately()
    {
        Creature oldOwner = Owner;
        RemoveInternal();
        await AfterRemoved(oldOwner);
    }
}

public sealed class LittleRedAngerGaugePower : LibraryOfRuinaPowerModel
{
    public const int MaxAnger = 100;
    public const int AngerResetValue = 0;
    public const int AngerLossOnWolfHit = 10;
    public const int WolfAttackAngerPerUnblockedHit = 40;
    public const int WolfHowlAngerPerUnblockedHit = 60;

    protected override string LegacyPowerId => "LITTLE_RED_ANGER_GAUGE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("StartAnger", AngerResetValue),
        new DynamicVar("MaxAnger", MaxAnger),
        new DynamicVar("WolfAttackAnger", WolfAttackAngerPerUnblockedHit),
        new DynamicVar("WolfHowlAnger", WolfHowlAngerPerUnblockedHit),
        new DynamicVar("WolfHitLoss", AngerLossOnWolfHit)
    ];

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        SetAmount(0, silent: true);
        return Task.CompletedTask;
    }

    public async Task ChangeAnger(int delta)
    {
        if (Owner.IsDead || delta == 0)
        {
            return;
        }

        int nextAmount = Math.Clamp(Amount + delta, 0, MaxAnger);
        if (nextAmount == Amount)
        {
            return;
        }

        SetAmount(nextAmount, silent: true);
        if (nextAmount >= MaxAnger && Owner.Monster is LittleRedRidingHoodedMercenary littleRed)
        {
            await littleRed.EnterRage();
        }
    }

    public void ResetGauge()
    {
        SetAmount(0, silent: true);
    }
}

public sealed class LittleRedRagePower : LibraryOfRuinaPowerModel, ISecondaryDisplayAmountPower
{
    public const int DefaultTurns = 2;
    public const int StrengthAfterRageAttack = 2;

    private sealed class Data
    {
        public int TurnsRemaining;
    }

    private sealed class TurnsVar : DynamicVar
    {
        public TurnsVar() : base("Turns", 0m)
        {
        }

        protected override decimal GetBaseValueForIConvertible()
        {
            return _owner is LittleRedRagePower power ? power.TurnsRemainingForText : base.GetBaseValueForIConvertible();
        }

        public override string ToString()
        {
            return GetBaseValueForIConvertible().ToString();
        }
    }

    protected override string LegacyPowerId => "LITTLE_RED_RAGE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Counter;

    public bool ShowSecondaryDisplayAmount => true;

    public int SecondaryDisplayAmount => TurnsRemaining;

    public Color SecondaryDisplayAmountLabelColor => _normalAmountLabelColor;

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<StrengthPower>()];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new TurnsVar(),
        new PowerVar<StrengthPower>(StrengthAfterRageAttack),
        new DynamicVar("ResetAnger", LittleRedAngerGaugePower.AngerResetValue)
    ];

    protected override object InitInternalData()
    {
        return new Data();
    }

    private int TurnsRemaining => GetInternalData<Data>().TurnsRemaining;

    public int TurnsRemainingForText => TurnsRemaining;

    public static async Task<LittleRedRagePower?> ApplyWithDuration(
        Creature target,
        int amount,
        int turns,
        Creature applier)
    {
        LittleRedRagePower? existing = target.GetPower<LittleRedRagePower>();
        if (existing == null)
        {
            var mutable = (LittleRedRagePower)ModelDb.Power<LittleRedRagePower>().ToMutable();
            mutable.SetTurnsRemaining(turns, notifyDisplay: false);
            mutable.SkipNextDurationTick = target.CombatState?.CurrentSide == target.Side;
            await PowerCmdCompat.Apply(mutable, target, amount, applier, null);
            return mutable;
        }

        existing.SetTurnsRemaining(Math.Max(existing.TurnsRemaining, turns));
        if (amount != 0)
        {
            await PowerCmdCompat.ModifyAmount(existing, amount, applier, null);
            existing.SkipNextDurationTick = existing.CombatState.CurrentSide == existing.Owner.Side;
        }

        return existing;
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != Owner.Side || Owner.IsDead)
        {
            return;
        }

        if (SkipNextDurationTick)
        {
            SkipNextDurationTick = false;
            return;
        }

        int nextTurns = TurnsRemaining - 1;
        SetTurnsRemaining(nextTurns);
        if (nextTurns <= 0)
        {
            await PowerCmd.Remove(this);
        }
    }

    public override Task AfterRemoved(Creature oldOwner)
    {
        if (oldOwner.Monster is LittleRedRidingHoodedMercenary littleRed)
        {
            littleRed.OnRageEnded();
        }

        return Task.CompletedTask;
    }

    private void SetTurnsRemaining(int turnsRemaining, bool notifyDisplay = true)
    {
        AssertMutable();
        Data data = GetInternalData<Data>();
        int clamped = Math.Max(0, turnsRemaining);
        if (data.TurnsRemaining == clamped)
        {
            return;
        }

        data.TurnsRemaining = clamped;
        if (notifyDisplay)
        {
            InvokeDisplayAmountChanged();
        }
    }
}

public sealed class LittleRedUnrelievedAngerPassivePower : LibraryOfRuinaPowerModel
{
    public const int MaxHpIncreasePercent = 50;
    public const int HealPercent = 100;

    protected override string LegacyPowerId => "LITTLE_RED_UNRELIEVED_ANGER_PASSIVE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("MaxHpIncreasePercent", MaxHpIncreasePercent),
        new DynamicVar("HealPercent", HealPercent)
    ];
}

public sealed class LittleRedUnrelievedAngerPower : LibraryOfRuinaPowerModel
{
    public const int StrengthPerPlayerTurn = 3;

    protected override string LegacyPowerId => "LITTLE_RED_UNRELIEVED_ANGER_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<StrengthPower>(StrengthPerPlayerTurn)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<StrengthPower>()];

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, CombatStateLike combatState)
    {
        if (side != CombatSide.Player || Owner.IsDead)
        {
            return;
        }

        await PowerCmdCompat.Apply<StrengthPower>(Owner, StrengthPerPlayerTurn, Owner, null);
    }
}

internal static class WolfPhaseTwoThreshold
{
    public const int PhaseTwoHpNumerator = 3;
    private const int PhaseTwoHpDenominator = 10;
    public const int PhaseTwoHpPercent = PhaseTwoHpNumerator * 100 / PhaseTwoHpDenominator;

    public static int Calculate(Creature? owner)
    {
        if (owner == null)
        {
            return 0;
        }

        return owner.MaxHp * PhaseTwoHpNumerator / PhaseTwoHpDenominator;
    }
}

public sealed class WolfHowlPassivePower : LibraryOfRuinaPowerModel
{
    public const int BaseMovesPerHowl = 3;
    public const int PhaseTwoStunTurns = 1;

    protected override string LegacyPowerId => "WOLF_HOWL_PASSIVE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new ThresholdVar(),
        new DynamicVar("ThresholdPercent", WolfPhaseTwoThreshold.PhaseTwoHpPercent),
        new DynamicVar("BaseMoves", BaseMovesPerHowl),
        new DynamicVar("StunTurns", PhaseTwoStunTurns)
    ];

    private sealed class ThresholdVar : DynamicVar
    {
        public ThresholdVar() : base("Threshold", 0m)
        {
        }

        protected override decimal GetBaseValueForIConvertible()
        {
            return _owner is WolfHowlPassivePower power
                ? WolfPhaseTwoThreshold.Calculate(power.Owner)
                : base.GetBaseValueForIConvertible();
        }

        public override string ToString()
        {
            return GetBaseValueForIConvertible().ToString();
        }
    }
}

public sealed class WolfHowlingNightmarePower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "WOLF_HOWLING_NIGHTMARE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new ThresholdVar(),
        new DynamicVar("ThresholdPercent", WolfPhaseTwoThreshold.PhaseTwoHpPercent),
        new PowerVar<LibraryOfRuinaNextTurnStrength>("NextTurnStrength", WolfInHerNightmares.PhaseTwoNextTurnStrength)
    ];

    private sealed class ThresholdVar : DynamicVar
    {
        public ThresholdVar() : base("Threshold", 0m)
        {
        }

        protected override decimal GetBaseValueForIConvertible()
        {
            return _owner is WolfHowlingNightmarePower power
                ? WolfPhaseTwoThreshold.Calculate(power.Owner)
                : base.GetBaseValueForIConvertible();
        }

        public override string ToString()
        {
            return GetBaseValueForIConvertible().ToString();
        }
    }
}

public sealed class LittleRedMercenaryDamageTrackerPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "LITTLE_RED_DAMAGE_TRACKER_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override bool IsVisibleInternal => false;

    public override Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        if (LittleRedMercenaryEncounterHelper.IsLittleRedEncounter(target.CombatState)
            && result.WasTargetKilled
            && target.Monster is LittleRedRidingHoodedMercenary or WolfInHerNightmares)
        {
            LittleRedDeathContext.Record(target, dealer);
        }

        return Task.CompletedTask;
    }

    public override async Task AfterAttack(
        PlayerChoiceContext choiceContext,
        AttackCommand command)
    {
        if (command.Attacker != Owner
            || command.Attacker?.Monster is not LittleRedRidingHoodedMercenary
            || command.Attacker.GetPower<LittleRedRagePower>() == null)
        {
            return;
        }

        if (AttackCommandCompat.Results(command).Any())
        {
            await PowerCmdCompat.Apply<LibraryOfRuinaNextTurnStrength>(
                command.Attacker,
                LittleRedRagePower.StrengthAfterRageAttack,
                command.Attacker,
                null);
        }
    }
}
