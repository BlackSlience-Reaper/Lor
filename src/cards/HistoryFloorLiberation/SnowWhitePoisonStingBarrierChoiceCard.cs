using LibraryOfRuina.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.HistoryFloorLiberation;

[CardPool(typeof(TokenCardPool))]
public sealed class SnowWhitePoisonStingBarrierChoiceCard :
    SnowWhiteAppleChoiceCardBase
{
    protected override string PortraitFileName =>
        "snow_white_poison_sting_barrier_choice_card.png";
}
