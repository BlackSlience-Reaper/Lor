using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.compat;
using LibraryOfRuina.guests.DawnOffice;
using LibraryOfRuina.monsters.TechnologyFloorLiberation;
using LibraryOfRuina.patches;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.powers.TechnologyFloorLiberation;

public sealed class RegretIronEchoPower : LibraryOfRuinaPowerModel
{
    private const int ParalysisStacks = 1;

    public override int DisplayAmount => 1;

    protected override string LegacyPowerId => "REGRET_IRON_ECHO_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<LibraryOfRuinaParalysisPower>("Paralysis", ParalysisStacks)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<LibraryOfRuinaParalysisPower>()
    ];

    public override async Task AfterAttack(
        PlayerChoiceContext choiceContext,
        AttackCommand command)
    {
        if (Owner.IsDead || command.Attacker != Owner)
        {
            return;
        }

        IReadOnlyList<Creature> targets = AttackCommandCompat.Results(command)
            .Where(static result => result.Receiver.IsAlive && result.UnblockedDamage > 0)
            .Select(static result => result.Receiver)
            .Distinct()
            .ToArray();

        if (targets.Count == 0)
        {
            return;
        }

        Flash();
        await PowerCmdCompat.Apply<LibraryOfRuinaParalysisPower>(targets, ParalysisStacks, Owner, null);
    }
}

public sealed class RegretExtremeViolencePower : LibraryOfRuinaPowerModel
{
    private const int BaseDamageThreshold = 15;
    private const int NextTurnStrengthPerThreshold = 2;

    private sealed class Data
    {
        public int DamageAccumulator;
    }

    private int ScaledDamageThreshold
    {
        get
        {
            int playerCount = MultiplayerScalingPatchHelper.ResolveRunPlayerCount(Owner?.CombatState);
            decimal multiplier = playerCount switch
            {
                <= 1 => 1m,
                2 => 2m,
                3 => 2.5m,
                4 => 3m,
                _ => 1m
            };
            return (int)decimal.Ceiling(BaseDamageThreshold * multiplier);
        }
    }

    public override int DisplayAmount => ScaledDamageThreshold - GetInternalData<Data>().DamageAccumulator;

    protected override string LegacyPowerId => "REGRET_EXTREME_VIOLENCE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Threshold", BaseDamageThreshold),
        new PowerVar<LibraryOfRuinaNextTurnStrength>("NextTurnStrength", NextTurnStrengthPerThreshold)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<LibraryOfRuinaNextTurnStrength>()
    ];

    protected override object InitInternalData()
    {
        return new Data();
    }

    public override Task BeforeApplied(Creature target, decimal amount, Creature? applier, CardModel? cardSource)
    {
        DynamicVars["Threshold"].BaseValue = ScaledDamageThreshold;
        return Task.CompletedTask;
    }

    public override async Task AfterAttack(
        PlayerChoiceContext choiceContext,
        AttackCommand command)
    {
        if (Owner.IsDead || command.Attacker != Owner)
        {
            return;
        }

        int unblockedDealt = AttackCommandCompat.Results(command)
            .Where(static result => result.Receiver.IsPlayer)
            .Sum(static result => Math.Max(0, result.UnblockedDamage));

        if (unblockedDealt <= 0)
        {
            return;
        }

        int threshold = ScaledDamageThreshold;
        Data data = GetInternalData<Data>();
        data.DamageAccumulator += unblockedDealt;
        InvokeDisplayAmountChanged();
        if (data.DamageAccumulator < threshold)
        {
            return;
        }

        int thresholdsCrossed = data.DamageAccumulator / threshold;
        data.DamageAccumulator -= thresholdsCrossed * threshold;
        InvokeDisplayAmountChanged();
        int strengthToGrant = thresholdsCrossed * NextTurnStrengthPerThreshold;
        if (strengthToGrant <= 0)
        {
            return;
        }

        Flash();
        await PowerCmdCompat.Apply<LibraryOfRuinaNextTurnStrength>(Owner, strengthToGrant, Owner, null);
    }
}

public sealed class RegretFearPower : LibraryOfRuinaPowerModel
{
    private const int TargetWeak = 4;
    private const int TriggerCountGain = 1;

    public override int DisplayAmount => TargetWeak;

    private sealed class Data
    {
        public int TriggerCount;
        public int LastTriggeredRoundNumber;
        public CombatSide LastTriggeredSide;
    }

    protected override string LegacyPowerId => "REGRET_FEAR_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<LibraryWeakPower>("Weak", TargetWeak),
        new DynamicVar("TriggerGain", TriggerCountGain)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<LibraryWeakPower>()
    ];

    protected override object InitInternalData()
    {
        return new Data();
    }

    public int TriggerCount
    {
        get => GetInternalData<Data>().TriggerCount;
        internal set
        {
            AssertMutable();
            GetInternalData<Data>().TriggerCount = Math.Max(0, value);
            Owner?.GetPower<RegretEndBeginEndPower>()?.RefreshCounterDisplay();
        }
    }

    public override async Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (Owner == null
            || amount <= 0m
            || power.Owner != Owner
            || power.GetTypeForAmount(amount) != PowerType.Debuff
            || ReferenceEquals(power, this)
            || power is LibraryWeakPower)
        {
            return;
        }

        var combatState = Owner.CombatState;
        if (combatState == null)
        {
            return;
        }

        Data data = GetInternalData<Data>();
        if (data.LastTriggeredRoundNumber == combatState.RoundNumber
            && data.LastTriggeredSide == combatState.CurrentSide)
        {
            return;
        }

        data.LastTriggeredRoundNumber = combatState.RoundNumber;
        data.LastTriggeredSide = combatState.CurrentSide;

        TriggerCount += TriggerCountGain;
        Flash();
        await TopUpWeakAsync();
    }

    public override Task AfterRemoved(Creature oldOwner)
    {
        oldOwner.GetPower<RegretEndBeginEndPower>()?.RefreshCounterDisplay();
        return Task.CompletedTask;
    }

    private async Task TopUpWeakAsync()
    {
        if (Owner == null || Owner.IsDead)
        {
            return;
        }

        int currentWeak = Owner.GetPower<LibraryWeakPower>()?.Amount ?? 0;
        if (currentWeak >= TargetWeak)
        {
            return;
        }

        int delta = TargetWeak - currentWeak;
        int weakTurns = Owner.CombatState?.CurrentSide == CombatSide.Enemy ? 1 : 0;
        await LibraryPowerCmd.Apply<LibraryWeakPower>(
            new ThrowingPlayerChoiceContext(),
            Owner,
            delta,
            weakTurns,
            IsPermanent: false,
            Owner,
            null);
    }
}

public sealed class RegretEndBeginEndPower : LibraryOfRuinaPowerModel
{
    private const int TriggerThreshold = 2;

    protected override string LegacyPowerId => "REGRET_END_BEGIN_END_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override int DisplayAmount => Owner?.GetPower<RegretFearPower>()?.TriggerCount ?? 0;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Threshold", TriggerThreshold)
    ];

    internal void RefreshCounterDisplay()
    {
        InvokeDisplayAmountChanged();
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != Owner.Side
            || Owner.IsDead
            || Owner.Monster is not TechnologyFloorRegretBoss regret)
        {
            return;
        }

        RegretFearPower? fear = Owner.GetPower<RegretFearPower>();
        if (fear == null || fear.TriggerCount < TriggerThreshold)
        {
            return;
        }

        if (regret.HasQueuedEndBeginEndSequence)
        {
            return;
        }

        fear.TriggerCount = 0;
        Flash();
        await regret.QueueEndBeginEndSequence();
    }
}
