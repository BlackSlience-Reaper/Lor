using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.content.abnormalities.QueenOfHatred;

[CardPool(typeof(TokenCardPool))]
public sealed class QueenOfHatredPhilanthropyChoiceCard : QueenOfHatredPageChoiceCardBase
{
    public override QueenOfHatredPageMode PageMode => QueenOfHatredPageMode.Philanthropy;

    protected override string PortraitFileName => "queen_of_hatred_philanthropy_choice_card.png";
}
