using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.content.liberation.History;

[CardPool(typeof(TokenCardPool))]
public sealed class MatchMarkFootstepsChoiceCard : MatchMarkChoiceCardBase
{
    public override MatchMarkMode PageMode => MatchMarkMode.Footsteps;

    protected override string PortraitFileName => "match_mark_footsteps_choice_card.png";
}
