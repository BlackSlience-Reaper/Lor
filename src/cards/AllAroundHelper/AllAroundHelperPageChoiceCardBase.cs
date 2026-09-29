using LibraryOfRuina.framework.cards;
using LibraryOfRuina.relics.AllAroundHelper;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;

namespace LibraryOfRuina.cards.AllAroundHelper;

public abstract class AllAroundHelperPageChoiceCardBase : PageChoiceCard<AllAroundHelperPageMode>
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        EnergyHoverTip,
        HoverTipFactory.FromPower<DrawCardsNextTurnPower>()
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("HandThreshold", AllAroundHelperPageRelic.ChargeHandThreshold),
        new EnergyVar(AllAroundHelperPageRelic.ChargeEnergyNextTurn),
        new CardsVar(AllAroundHelperPageRelic.RecognitionCardsPerSwift),
        new DynamicVar("Swift", AllAroundHelperPageRelic.RecognitionSwift)
    ];
}
