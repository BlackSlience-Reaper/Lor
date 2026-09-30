using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.content.abnormalities.SpiderBud;

[CardPool(typeof(TokenCardPool))]
public sealed class SpiderBudVigilanceChoiceCard : SpiderBudPageChoiceCardBase
{
    public override SpiderBudPageMode PageMode => SpiderBudPageMode.Vigilance;

    protected override string PortraitFileName => "spider_bud_vigilance_choice_card.png";
}
