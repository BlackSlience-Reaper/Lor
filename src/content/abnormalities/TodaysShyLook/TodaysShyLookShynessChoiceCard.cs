using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.content.abnormalities.TodaysShyLook;

[CardPool(typeof(TokenCardPool))]
public sealed class TodaysShyLookShynessChoiceCard : TodaysShyLookPageChoiceCardBase
{
    public override TodaysShyLookPageMode PageMode => TodaysShyLookPageMode.Shyness;

    protected override string PortraitFileName => "todays_shy_look_shyness_choice_card.png";
}
