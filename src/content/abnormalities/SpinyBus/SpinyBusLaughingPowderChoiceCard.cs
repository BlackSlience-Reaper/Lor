using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.content.abnormalities.SpinyBus;

[CardPool(typeof(TokenCardPool))]
public sealed class SpinyBusLaughingPowderChoiceCard : SpinyBusPageChoiceCardBase
{
    public override SpinyBusPageMode PageMode => SpinyBusPageMode.LaughingPowder;

    protected override string PortraitFileName => "spiny_bus_laughing_powder_choice_card.png";
}
