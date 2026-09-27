using LibraryOfRuina.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.QueenBee;

[CardPool(typeof(TokenCardPool))]
public sealed class QueenBeeSporeChoiceCard : QueenBeePageChoiceCardBase
{
    protected override string PortraitFileName => "queen_bee_spore_choice_card.png";
}
