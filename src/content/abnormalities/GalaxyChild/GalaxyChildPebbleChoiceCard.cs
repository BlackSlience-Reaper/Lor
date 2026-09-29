using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.content.abnormalities.GalaxyChild;

[CardPool(typeof(TokenCardPool))]
public sealed class GalaxyChildPebbleChoiceCard : GalaxyChildPageChoiceCardBase
{
    public override GalaxyChildPageMode PageMode => GalaxyChildPageMode.Pebble;

    protected override string PortraitFileName => "galaxy_child_pebble_choice_card.png";
}
