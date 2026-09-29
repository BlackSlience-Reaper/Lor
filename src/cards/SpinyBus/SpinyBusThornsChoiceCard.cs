using LibraryOfRuina.helpers;
using LibraryOfRuina.relics.SpinyBus;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.SpinyBus;

[CardPool(typeof(TokenCardPool))]
public sealed class SpinyBusThornsChoiceCard : SpinyBusPageChoiceCardBase
{
    public override SpinyBusPageMode PageMode => SpinyBusPageMode.Thorns;

    protected override string PortraitFileName => "spiny_bus_thorns_choice_card.png";
}
