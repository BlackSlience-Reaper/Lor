using LibraryOfRuina.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.FuneralOfTheDeadButterflies;

[CardPool(typeof(TokenCardPool))]
public sealed class FuneralMourningChoiceCard : FuneralPageChoiceCardBase
{
    protected override string PortraitFileName => "funeral_mourning_choice_card.png";
}
