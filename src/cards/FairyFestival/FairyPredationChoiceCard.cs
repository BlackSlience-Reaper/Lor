using LibraryOfRuina.helpers;
using LibraryOfRuina.relics.FairyFestival;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.FairyFestival;

[CardPool(typeof(TokenCardPool))]
public sealed class FairyPredationChoiceCard : FairyFestivalPageChoiceCardBase
{
    public override FairyFestivalPageMode PageMode => FairyFestivalPageMode.Predation;

    protected override string PortraitFileName => "fairy_predation_choice_card.png";
}
