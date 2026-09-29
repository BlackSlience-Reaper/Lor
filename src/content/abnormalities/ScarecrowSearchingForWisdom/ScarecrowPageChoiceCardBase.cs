using LibraryOfRuina.framework.cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.abnormalities.ScarecrowSearchingForWisdom;

public abstract class ScarecrowPageChoiceCardBase : PageChoiceCard<ScarecrowPageMode>
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<ScarecrowWisdomPower>(),
        ..HoverTipFactory.FromCardWithCardHoverTips<ScarecrowWisdomStatusCard>()
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Cards", ScarecrowPageRelic.RakeCardsToCopy),
        //new DynamicVar("CostIncrease", ScarecrowPageRelic.RakeCopiedCardCostIncrease),
        new DamageVar(ScarecrowPageRelic.HarvestDamageBonus, ValueProp.Unpowered),
        new DynamicVar("ChaoThresholdPercent", ScarecrowPageRelic.HarvestChaoThresholdPercent),
        new DynamicVar("PlayedCards", ScarecrowPageRelic.TornWisdomCardsPerTrigger),
        new EnergyVar(ScarecrowPageRelic.TornWisdomEnergyGain)
    ];
}
