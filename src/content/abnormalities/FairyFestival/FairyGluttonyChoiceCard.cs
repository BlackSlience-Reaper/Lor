using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.content.abnormalities.FairyFestival;

[CardPool(typeof(TokenCardPool))]
public sealed class FairyGluttonyChoiceCard : FairyFestivalPageChoiceCardBase
{
    public override FairyFestivalPageMode PageMode => FairyFestivalPageMode.Gluttony;

    protected override string PortraitFileName => "fairy_gluttony_choice_card.png";
}
