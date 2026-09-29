using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.content.liberation.History;

[CardPool(typeof(TokenCardPool))]
public sealed class MatchMarkAfterglowChoiceCard : MatchMarkChoiceCardBase
{
    public override MatchMarkMode PageMode => MatchMarkMode.Afterglow;

    protected override string PortraitFileName => "match_mark_afterglow_choice_card.png";
}
