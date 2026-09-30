using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.content.abnormalities.FairyFestival;

[CardPool(typeof(TokenCardPool))]
public sealed class FairyPredationChoiceCard : FairyFestivalPageChoiceCardBase
{
    public override FairyFestivalPageMode PageMode => FairyFestivalPageMode.Predation;

    protected override string PortraitFileName => "fairy_predation_choice_card.png";
}
