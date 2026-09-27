using LibraryOfRuina.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.DespairKnight;

[CardPool(typeof(TokenCardPool))]
public sealed class DespairKnightBlessingChoiceCard : DespairKnightPageChoiceCardBase
{
    protected override string PortraitFileName => "despair_knight_blessing_choice_card.png";
}
