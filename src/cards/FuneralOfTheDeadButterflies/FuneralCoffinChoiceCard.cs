using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.relics.FuneralOfTheDeadButterflies;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.FuneralOfTheDeadButterflies;

[CardPool(typeof(TokenCardPool))]
public sealed class FuneralCoffinChoiceCard : FuneralPageChoiceCardBase
{
    public override FuneralOfTheDeadButterfliesPageMode PageMode => FuneralOfTheDeadButterfliesPageMode.Coffin;

    protected override string PortraitFileName => "funeral_coffin_choice_card.png";
}
