using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.content.abnormalities.QueenBee;

[CardPool(typeof(TokenCardPool))]
public sealed class QueenBeeLoyaltyChoiceCard : QueenBeePageChoiceCardBase
{
    public override QueenBeePageMode PageMode => QueenBeePageMode.Loyalty;

    protected override string PortraitFileName => "queen_bee_loyalty_choice_card.png";
}
