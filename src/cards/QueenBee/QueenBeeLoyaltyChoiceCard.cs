using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.relics.QueenBee;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.QueenBee;

[CardPool(typeof(TokenCardPool))]
public sealed class QueenBeeLoyaltyChoiceCard : QueenBeePageChoiceCardBase
{
    public override QueenBeePageMode PageMode => QueenBeePageMode.Loyalty;

    protected override string PortraitFileName => "queen_bee_loyalty_choice_card.png";
}
