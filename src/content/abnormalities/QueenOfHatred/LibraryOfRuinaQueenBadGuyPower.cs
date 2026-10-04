using LibraryLib.Models;
using LibraryOfRuina.framework.powers;
using LibraryLib.Utils.Resistance;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.abnormalities.QueenOfHatred;

public sealed class LibraryOfRuinaQueenBadGuyPower : LibraryOfRuinaPowerModel, ILibraryAbstractModel
{
    protected override string LegacyPowerId => "QUEEN_BAD_GUY_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override bool IsVisibleInternal => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("DamageIncrease", QueenOfHatredPageRelic.JusticeDamageIncreasePercent)
    ];

#if STS2_0_111_0
    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
#else
    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
#endif
    {
        return target == Owner && amount > 0m
            ? 1m + QueenOfHatredPageRelic.JusticeDamageIncreasePercent / 100m
            : 1m;
    }

    public decimal ModifyChaoDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay,
        LibraryDamageType type)
    {
        return target == Owner && amount > 0m
            ? 1m + QueenOfHatredPageRelic.JusticeDamageIncreasePercent / 100m
            : 1m;
    }

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
    ];
}
