using LibraryOfRuina.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.FuneralOfTheDeadButterflies;

[CardPool(typeof(TokenCardPool))]
public sealed class FuneralRestChoiceCard : FuneralPageChoiceCardBase
{
    protected override string PortraitFileName => "funeral_rest_choice_card.png";
}
