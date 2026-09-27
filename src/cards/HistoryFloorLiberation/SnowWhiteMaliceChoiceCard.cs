using LibraryOfRuina.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.HistoryFloorLiberation;

[CardPool(typeof(TokenCardPool))]
public sealed class SnowWhiteMaliceChoiceCard : SnowWhiteAppleChoiceCardBase
{
    protected override string PortraitFileName =>
        "snow_white_malice_choice_card.png";
}
