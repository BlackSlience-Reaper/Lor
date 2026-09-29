using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.content.abnormalities.SpinyBus;

[CardPool(typeof(TokenCardPool))]
public sealed class SpinyBusPleasureChoiceCard : SpinyBusPageChoiceCardBase
{
    public override SpinyBusPageMode PageMode => SpinyBusPageMode.Pleasure;

    protected override string PortraitFileName => "spiny_bus_pleasure_choice_card.png";
}
