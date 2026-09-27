using LibraryOfRuina.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.DespairKnight;

[CardPool(typeof(TokenCardPool))]
public sealed class DespairKnightDespairChoiceCard : DespairKnightPageChoiceCardBase
{
    protected override string PortraitFileName => "despair_knight_despair_choice_card.png";
}
