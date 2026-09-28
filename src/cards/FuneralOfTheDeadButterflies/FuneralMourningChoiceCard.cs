using LibraryOfRuina.helpers;
using LibraryOfRuina.relics.FuneralOfTheDeadButterflies;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.FuneralOfTheDeadButterflies;

[CardPool(typeof(TokenCardPool))]
public sealed class FuneralMourningChoiceCard : FuneralPageChoiceCardBase
{
    public override FuneralOfTheDeadButterfliesPageMode PageMode => FuneralOfTheDeadButterfliesPageMode.Mourning;

    protected override string PortraitFileName => "funeral_mourning_choice_card.png";
}
