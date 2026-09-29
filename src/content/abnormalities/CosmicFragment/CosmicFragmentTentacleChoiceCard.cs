using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.content.abnormalities.CosmicFragment;

[CardPool(typeof(TokenCardPool))]
public sealed class CosmicFragmentTentacleChoiceCard : CosmicFragmentPageChoiceCardBase
{
    public override CosmicFragmentPageMode PageMode => CosmicFragmentPageMode.Tentacle;

    protected override string PortraitFileName => "cosmic_fragment_tentacle_choice_card.png";
}
