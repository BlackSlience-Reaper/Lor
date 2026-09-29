using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.relics.FuneralOfTheDeadButterflies;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.FuneralOfTheDeadButterflies;

[CardPool(typeof(TokenCardPool))]
public sealed class FuneralRestChoiceCard : FuneralPageChoiceCardBase
{
    public override FuneralOfTheDeadButterfliesPageMode PageMode => FuneralOfTheDeadButterfliesPageMode.Rest;

    protected override string PortraitFileName => "funeral_rest_choice_card.png";
}
