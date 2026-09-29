using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.relics.TodaysShyLook;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.TodaysShyLook;

    [CardPool(typeof(TokenCardPool))]
public sealed class TodaysShyLookSocialDistanceChoiceCard : TodaysShyLookPageChoiceCardBase
{
    public override TodaysShyLookPageMode PageMode => TodaysShyLookPageMode.SocialDistance;

    protected override string PortraitFileName => "todays_shy_look_social_distance_choice_card.png";
}
