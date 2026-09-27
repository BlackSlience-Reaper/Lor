using LibraryOfRuina.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.HistoryFloorLiberation;

[CardPool(typeof(TokenCardPool))]
public sealed class SnowWhiteStranglingVineChoiceCard :
    SnowWhiteAppleChoiceCardBase
{
    protected override string PortraitFileName =>
        "snow_white_strangling_vine_choice_card.png";
}
