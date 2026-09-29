using LibraryLib.Utils.Resistance;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.relics.NaturalFloorLiberation;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.powers.NaturalFloorLiberation;

public sealed class NihilBadGuyPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "NIHIL_BAD_GUY_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override bool IsVisibleInternal => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("DamageIncrease", QueenOfHatredEnhancedPageRelic.JusticeDamagePercent)
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
            ? 1m + QueenOfHatredEnhancedPageRelic.JusticeDamagePercent / 100m
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
            ? 1m + QueenOfHatredEnhancedPageRelic.JusticeDamagePercent / 100m
            : 1m;
    }

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
    ];
}
