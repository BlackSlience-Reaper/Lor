using LibraryOfRuina.helpers;
using LibraryOfRuina.relics.SpiderBud;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.SpiderBud;

[CardPool(typeof(TokenCardPool))]
public sealed class SpiderBudCocoonBindChoiceCard : SpiderBudPageChoiceCardBase
{
    public override SpiderBudPageMode PageMode => SpiderBudPageMode.CocoonBind;

    protected override string PortraitFileName => "spider_bud_cocoon_bind_choice_card.png";
}
