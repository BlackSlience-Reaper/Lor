using LibraryOfRuina.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.FuneralOfTheDeadButterflies;

[CardPool(typeof(TokenCardPool))]
public sealed class FuneralCoffinChoiceCard : FuneralPageChoiceCardBase
{
    protected override string PortraitFileName => "funeral_coffin_choice_card.png";
}
