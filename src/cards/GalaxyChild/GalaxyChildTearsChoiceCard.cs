using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.relics.GalaxyChild;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.GalaxyChild;

[CardPool(typeof(TokenCardPool))]
public sealed class GalaxyChildTearsChoiceCard : GalaxyChildPageChoiceCardBase
{
    public override GalaxyChildPageMode PageMode => GalaxyChildPageMode.Tears;

    protected override string PortraitFileName => "galaxy_child_tears_choice_card.png";
}
