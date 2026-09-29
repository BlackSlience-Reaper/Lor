using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.relics.HistoryFloorLiberation;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.HistoryFloorLiberation;

[CardPool(typeof(TokenCardPool))]
public sealed class SnowWhiteStranglingVineChoiceCard :
    SnowWhiteAppleChoiceCardBase
{
    public override SnowWhiteApplePageMode PageMode => SnowWhiteApplePageMode.StranglingVine;

    protected override string PortraitFileName =>
        "snow_white_strangling_vine_choice_card.png";
}
