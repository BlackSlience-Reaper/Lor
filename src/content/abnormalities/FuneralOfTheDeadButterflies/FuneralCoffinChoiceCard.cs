using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.content.abnormalities.FuneralOfTheDeadButterflies;

[CardPool(typeof(TokenCardPool))]
public sealed class FuneralCoffinChoiceCard : FuneralPageChoiceCardBase
{
    public override FuneralOfTheDeadButterfliesPageMode PageMode => FuneralOfTheDeadButterfliesPageMode.Coffin;

    protected override string PortraitFileName => "funeral_coffin_choice_card.png";
}
