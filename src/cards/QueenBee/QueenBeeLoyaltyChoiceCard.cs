using LibraryOfRuina.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.QueenBee;

[CardPool(typeof(TokenCardPool))]
public sealed class QueenBeeLoyaltyChoiceCard : QueenBeePageChoiceCardBase
{
    protected override string PortraitFileName => "queen_bee_loyalty_choice_card.png";
}
