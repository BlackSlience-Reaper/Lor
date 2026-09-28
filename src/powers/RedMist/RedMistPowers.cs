using System.Threading.Tasks;
using LibraryOfRuina.specialguests.Kali;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.powers.RedMist;

public sealed class KaliPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "KALI_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override bool IsVisibleInternal => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Buffer", 1m),
        new DynamicVar("HpThreshold", Kali.EgoHpThreshold),
        new DynamicVar("ReturnTurns", Kali.EgoReturnTurns)
    ];

    public override Task BeforeApplied(Creature target, decimal amount, Creature? applier, CardModel? cardSource)
    {
        DynamicVars["HpThreshold"].BaseValue = Kali.ResolveScaledEgoHpThreshold(target);
        return Task.CompletedTask;
    }
}

public sealed class RedMistStrongestOnePower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "RED_MIST_STRONGEST_ONE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override bool IsVisibleInternal => true;
}

public sealed class RedMistEgoPower : LibraryOfRuinaPowerModel
{
    private sealed class ScaledMinimumDirectDamageVar : DynamicVar
    {
        public ScaledMinimumDirectDamageVar()
            : base("MinDamage", Kali.MinimumDirectDamagePerRound)
        {
        }

        protected override decimal GetBaseValueForIConvertible()
        {
            return _owner is RedMistEgoPower { IsMutable: true } power
                   && power.Owner.Monster is Kali kali
                ? kali.ScaledMinimumDirectDamagePerRound
                : base.GetBaseValueForIConvertible();
        }

        public override string ToString() =>
            GetBaseValueForIConvertible().ToString();
    }

    protected override string LegacyPowerId => "RED_MIST_EGO_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override bool IsVisibleInternal => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Strong", Kali.EgoBaseBuffAmount),
        new DynamicVar("Endurance", Kali.EgoBaseBuffAmount),
        new DynamicVar("BloodMistMax", Kali.BloodMistMaxStacks),
        new ScaledMinimumDirectDamageVar(),
        new DynamicVar("ChaoLossPercent", Kali.ChaoLossPercent)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<LibraryStrongPower>(),
        HoverTipFactory.FromPower<LibraryEndurancePower>()
    ];
}

public sealed class RedMistBloodMistPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "RED_MIST_BLOOD_MIST_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override bool IsVisibleInternal => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Max", Kali.BloodMistMaxStacks)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<LibraryStrongPower>(),
        HoverTipFactory.FromPower<LibraryEndurancePower>()
    ];
}
