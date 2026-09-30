using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.content.liberation.History;

[CardPool(typeof(TokenCardPool))]
public sealed class MatchMarkEmberChoiceCard : MatchMarkChoiceCardBase
{
    public override MatchMarkMode PageMode => MatchMarkMode.Ember;

    protected override string PortraitFileName => "match_mark_ember_choice_card.png";
}
