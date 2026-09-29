using System.Collections.Generic;
using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.interop;
using LibraryOfRuina.powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using static LibraryOfRuina.reverberation.CryingChildren.CryingChildrenRules;

namespace LibraryOfRuina.reverberation.CryingChildren;

public abstract class CryingPassivePower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => Id.Entry;

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    public override bool AllowNegative => false;

    public override PowerInstanceType InstanceType => PowerInstanceType.None;

    public override string PackedIconPath => "res://images/powers/library_passive_purple.png";

    public override string ResolvedBigIconPath => PackedIconPath;

    protected virtual int ContactBurn => 0;

    protected virtual bool BurnsAttacker => false;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [HoverTipFactory.FromPower<LibraryBurnPower>()];

    public override Task AfterDamageGiven(PlayerChoiceContext context, Creature? dealer,
        DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
    {
        if (dealer == Owner && ContactBurn > 0 && result.TotalDamage > 0
            && target.IsAlive && ValuePropCompat.IsPoweredAttack(props))
        {
            return PowerCmdCompat.Apply<LibraryBurnPower>(context, target, ContactBurn, Owner, null);
        }
        return Task.CompletedTask;
    }

    public override Task AfterDamageReceived(PlayerChoiceContext context, Creature target,
        DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target == Owner && BurnsAttacker && ContactBurn > 0 && result.TotalDamage > 0
            && dealer is { IsAlive: true } && dealer != Owner && ValuePropCompat.IsPoweredAttack(props))
        {
            return PowerCmdCompat.Apply<LibraryBurnPower>(context, dealer, ContactBurn, Owner, null);
        }
        return Task.CompletedTask;
    }
}

public sealed class CryingNuovoFabricPower : CryingPassivePower
{
    private static int Reduction => DamageValue(FabricReduction, FabricHighReduction);

    public override string PackedIconPath => "res://images/powers/library_passive_orange.png";

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Reduction", Reduction)];

    public override bool ShouldClearBlock(Creature creature) => creature != Owner;

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource, CardPlay? cardPlay) =>
        target == Owner && ValuePropCompat.IsPoweredAttack(props) ? -Reduction : 0m;

    public override decimal ModifyChaoDamageAdditive(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource, CardPlay? cardPlay, LibraryDamageType type) =>
        target == Owner && ValuePropCompat.IsPoweredAttack(props) ? -Reduction : 0m;

    public override decimal ModifyEffectiveAmountMultiplicative(LibraryBasePowerModel power,
        decimal amount, Creature? dealer, CardModel? cardSource) =>
        power is LibraryBurnPower && power.Owner == Owner ? 0m : 1m;
}

public sealed class CryingPhilipOverheatPower : CryingPassivePower
{
    protected override int ContactBurn =>
        Owner.Monster is ReverberationPhilip { Overheated: true } ? PhilipHeatBurn : 0;

    protected override bool BurnsAttacker => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Threshold", PhilipHeatThreshold),
        new DynamicVar("Strength", HeatStrength),
        new DynamicVar("Burn", PhilipHeatBurn)
    ];
}

public sealed class CryingChildOverheatPower : CryingPassivePower
{
    protected override int ContactBurn =>
        Owner.Monster is UnspeakingChild { Overheated: true } ? ChildHeatBurn : 0;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Threshold", ChildHeatThreshold),
        new DynamicVar("Strength", HeatStrength),
        new DynamicVar("Vulnerable", ChildHeatVulnerable),
        new DynamicVar("Burn", ChildHeatBurn)
    ];
}

public abstract class CryingPhasePower : CryingPassivePower
{
    internal abstract int Phase { get; }

    internal bool IsHealthBarLockActive =>
        Owner.Monster is ReverberationPhilip { Phase: < 3 } philip
        && Owner.CurrentHp <= philip.MinimumHp;
}

public sealed class CryingPassionPower : CryingPhasePower
{
    internal override int Phase => 1;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("Threshold", FirstHpFloorPercent)];
}

public sealed class CryingSurgingHeartPower : CryingPhasePower
{
    internal override int Phase => 2;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("Threshold", SecondHpFloorPercent)];
}

public sealed class CryingBlazingBladePower : CryingPhasePower
{
    internal override int Phase => 3;

    protected override int ContactBurn => BladeBurn;

    protected override bool BurnsAttacker => true;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Burn", BladeBurn)];
}

public sealed class CryingHotHeartPower : CryingPassivePower
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Strong", ChildGrowthStrong),
        new DynamicVar("Endurance", ChildGrowthEndurance)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<LibraryBurnPower>(),
        HoverTipFactory.FromPower<LibraryStrongPower>(),
        HoverTipFactory.FromPower<LibraryEndurancePower>()
    ];

    public override decimal ModifyEffectiveAmountMultiplicative(LibraryBasePowerModel power,
        decimal amount, Creature? dealer, CardModel? cardSource) =>
        power is LibraryBurnPower && power.Owner == Owner ? ChildBurnMultiplier : 1m;
}

public sealed class CryingSwiftPower : CryingPassivePower
{
    public int ObtainedRound { get; private set; } = -1;

    public int ActiveRound { get; private set; } = -1;

    public override PowerType Type => PowerType.Buff;

    public bool IsActive => ActiveRound >= 0 && Owner?.CombatState?.RoundNumber == ActiveRound;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Strong", SwiftStrong),
        new DynamicVar("Turns", SwiftTurns),
        new DynamicVar("Burn", SwiftBurnBonus)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [HoverTipFactory.FromPower<LibraryStrongPower>(), HoverTipFactory.FromPower<LibraryBurnPower>()];

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        ObtainedRound = Owner.CombatState?.RoundNumber ?? 0;
        return Task.CompletedTask;
    }

    internal async Task ActivateForRound(int round)
    {
        if (round <= ObtainedRound || ActiveRound >= 0)
        {
            return;
        }
        ActiveRound = round;
        await LibraryPowerCmd.Apply<LibraryStrongPower>(Owner, SwiftStrong, SwiftTurns, Owner, null);
    }

    public override decimal ModifyPowerAmountGivenAdditive(PowerModel power, Creature giver,
        decimal amount, Creature? target, CardModel? cardSource) =>
        giver == Owner && power is LibraryBurnPower && amount > 0 && IsActive ? SwiftBurnBonus : 0m;

    public override async Task AfterSideTurnEndLate(PlayerChoiceContext context, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Enemy && IsActive)
        {
            await MegaCrit.Sts2.Core.Commands.PowerCmd.Remove(this);
        }
    }
}
