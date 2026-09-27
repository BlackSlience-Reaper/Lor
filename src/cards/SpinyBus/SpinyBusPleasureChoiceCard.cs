using LibraryOfRuina.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.SpinyBus;

[CardPool(typeof(TokenCardPool))]
public sealed class SpinyBusPleasureChoiceCard : SpinyBusPageChoiceCardBase
{
    protected override string PortraitFileName => "spiny_bus_pleasure_choice_card.png";
}
