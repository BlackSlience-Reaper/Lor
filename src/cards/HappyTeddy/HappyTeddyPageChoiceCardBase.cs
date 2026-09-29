using LibraryOfRuina.framework.cards;
using LibraryOfRuina.relics.HappyTeddy;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.cards.HappyTeddy;

public abstract class HappyTeddyPageChoiceCardBase : PageChoiceCard<HappyTeddyPageMode>
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.Static(StaticHoverTip.Block)
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(HappyTeddyPageRelic.LongingEmbraceBlockGain, ValueProp.Unpowered),
        new DynamicVar("SlashPower", HappyTeddyPageRelic.LongingEmbraceSlashPower),
        new DynamicVar("CostReduction", HappyTeddyPageRelic.HappyMemoryCostReduction),
        new DynamicVar("TurnInterval", HappyTeddyPageRelic.ExpressAffectionTurnInterval),
        new DynamicVar("BlockPercent", HappyTeddyPageRelic.ExpressAffectionBlockPercent)
    ];
}
