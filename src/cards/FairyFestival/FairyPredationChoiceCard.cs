using LibraryOfRuina.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.FairyFestival;

[CardPool(typeof(TokenCardPool))]
public sealed class FairyPredationChoiceCard : FairyFestivalPageChoiceCardBase
{
    protected override string PortraitFileName => "fairy_predation_choice_card.png";
}
