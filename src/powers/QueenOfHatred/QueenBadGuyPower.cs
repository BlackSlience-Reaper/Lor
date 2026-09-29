using LibraryOfRuina.framework.powers;
using LibraryOfRuina.relics.QueenOfHatred;
using LibraryLib.Utils.Resistance;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.powers.QueenOfHatred;

public sealed class LibraryOfRuinaQueenBadGuyPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "QUEEN_BAD_GUY_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override bool IsVisibleInternal => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("DamageIncrease", QueenOfHatredPageRelic.JusticeDamageIncreasePercent)
    ];

    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        return target == Owner && amount > 0m
            ? 1m + QueenOfHatredPageRelic.JusticeDamageIncreasePercent / 100m
            : 1m;
    }

    public override decimal ModifyChaoDamageMultiplicative(
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
