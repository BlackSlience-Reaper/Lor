using LibraryOfRuina.helpers;
using LibraryOfRuina.relics.TodaysShyLook;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.TodaysShyLook;

[CardPool(typeof(TokenCardPool))]
public sealed class TodaysShyLookShynessChoiceCard : TodaysShyLookPageChoiceCardBase
{
    public override TodaysShyLookPageMode PageMode => TodaysShyLookPageMode.Shyness;

    protected override string PortraitFileName => "todays_shy_look_shyness_choice_card.png";
}
