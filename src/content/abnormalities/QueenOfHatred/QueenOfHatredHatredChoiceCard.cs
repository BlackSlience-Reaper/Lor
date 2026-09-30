using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.content.abnormalities.QueenOfHatred;

[CardPool(typeof(TokenCardPool))]
public sealed class QueenOfHatredHatredChoiceCard : QueenOfHatredPageChoiceCardBase
{
    public override QueenOfHatredPageMode PageMode => QueenOfHatredPageMode.Hatred;

    protected override string PortraitFileName => "queen_of_hatred_hatred_choice_card.png";
}
