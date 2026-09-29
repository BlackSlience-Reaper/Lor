using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.monsters.HistoryFloorLiberation;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.HistoryFloorLiberation;

[CardPool(typeof(TokenCardPool))]
public sealed class MatchMarkAfterglowChoiceCard : MatchMarkChoiceCardBase
{
    public override MatchMarkMode PageMode => MatchMarkMode.Afterglow;

    protected override string PortraitFileName => "match_mark_afterglow_choice_card.png";
}
