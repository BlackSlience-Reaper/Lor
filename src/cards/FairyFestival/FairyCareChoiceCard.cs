using LibraryOfRuina.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.FairyFestival;

[CardPool(typeof(TokenCardPool))]
public sealed class FairyCareChoiceCard : FairyFestivalPageChoiceCardBase
{
    protected override string PortraitFileName => "fairy_care_choice_card.png";
}
