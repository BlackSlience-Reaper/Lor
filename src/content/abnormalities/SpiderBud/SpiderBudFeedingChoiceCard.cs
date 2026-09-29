using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.content.abnormalities.SpiderBud;

[CardPool(typeof(TokenCardPool))]
public sealed class SpiderBudFeedingChoiceCard : SpiderBudPageChoiceCardBase
{
    public override SpiderBudPageMode PageMode => SpiderBudPageMode.Feeding;

    protected override string PortraitFileName => "spider_bud_feeding_choice_card.png";
}
