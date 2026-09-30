using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.content.abnormalities.FuneralOfTheDeadButterflies;

[CardPool(typeof(TokenCardPool))]
public sealed class FuneralMourningChoiceCard : FuneralPageChoiceCardBase
{
    public override FuneralOfTheDeadButterfliesPageMode PageMode => FuneralOfTheDeadButterfliesPageMode.Mourning;

    protected override string PortraitFileName => "funeral_mourning_choice_card.png";
}
