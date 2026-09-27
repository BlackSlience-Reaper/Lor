using LibraryOfRuina.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.SpiderBud;

[CardPool(typeof(TokenCardPool))]
public sealed class SpiderBudFeedingChoiceCard : SpiderBudPageChoiceCardBase
{
    protected override string PortraitFileName => "spider_bud_feeding_choice_card.png";
}
