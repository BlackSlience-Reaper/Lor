using LibraryOfRuina.helpers;
using LibraryOfRuina.relics.QueenOfHatred;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.QueenOfHatred;

[CardPool(typeof(TokenCardPool))]
public sealed class QueenOfHatredJusticeChoiceCard : QueenOfHatredPageChoiceCardBase
{
    public override QueenOfHatredPageMode PageMode => QueenOfHatredPageMode.Justice;

    protected override string PortraitFileName => "queen_of_hatred_justice_choice_card.png";
}
