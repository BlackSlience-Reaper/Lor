using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.content.abnormalities.FairyFestival;

[CardPool(typeof(TokenCardPool))]
public sealed class FairyCareChoiceCard : FairyFestivalPageChoiceCardBase
{
    public override FairyFestivalPageMode PageMode => FairyFestivalPageMode.FairyCare;

    protected override string PortraitFileName => "fairy_care_choice_card.png";
}
