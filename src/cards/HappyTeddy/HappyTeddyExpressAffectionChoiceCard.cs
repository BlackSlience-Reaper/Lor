using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.relics.HappyTeddy;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.HappyTeddy;

[CardPool(typeof(TokenCardPool))]
public sealed class HappyTeddyExpressAffectionChoiceCard : HappyTeddyPageChoiceCardBase
{
    public override HappyTeddyPageMode PageMode => HappyTeddyPageMode.ExpressAffection;

    protected override string PortraitFileName => "happy_teddy_express_affection_choice_card.png";
}
