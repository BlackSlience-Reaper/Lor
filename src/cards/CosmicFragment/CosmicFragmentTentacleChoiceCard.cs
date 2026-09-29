using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.relics.CosmicFragment;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.CosmicFragment;

[CardPool(typeof(TokenCardPool))]
public sealed class CosmicFragmentTentacleChoiceCard : CosmicFragmentPageChoiceCardBase
{
    public override CosmicFragmentPageMode PageMode => CosmicFragmentPageMode.Tentacle;

    protected override string PortraitFileName => "cosmic_fragment_tentacle_choice_card.png";
}
