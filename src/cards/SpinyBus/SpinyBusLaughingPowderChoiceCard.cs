using LibraryOfRuina.helpers;
using LibraryOfRuina.relics.SpinyBus;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.SpinyBus;

[CardPool(typeof(TokenCardPool))]
public sealed class SpinyBusLaughingPowderChoiceCard : SpinyBusPageChoiceCardBase
{
    public override SpinyBusPageMode PageMode => SpinyBusPageMode.LaughingPowder;

    protected override string PortraitFileName => "spiny_bus_laughing_powder_choice_card.png";
}
