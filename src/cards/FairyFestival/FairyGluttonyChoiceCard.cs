using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.relics.FairyFestival;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.FairyFestival;

[CardPool(typeof(TokenCardPool))]
public sealed class FairyGluttonyChoiceCard : FairyFestivalPageChoiceCardBase
{
    public override FairyFestivalPageMode PageMode => FairyFestivalPageMode.Gluttony;

    protected override string PortraitFileName => "fairy_gluttony_choice_card.png";
}
