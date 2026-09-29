using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.content.abnormalities.SpinyBus;

[CardPool(typeof(TokenCardPool))]
public sealed class SpinyBusThornsChoiceCard : SpinyBusPageChoiceCardBase
{
    public override SpinyBusPageMode PageMode => SpinyBusPageMode.Thorns;

    protected override string PortraitFileName => "spiny_bus_thorns_choice_card.png";
}
