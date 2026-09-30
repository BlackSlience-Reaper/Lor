using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.content.abnormalities.CosmicFragment;

[CardPool(typeof(TokenCardPool))]
public sealed class CosmicFragmentOtherworldlyEchoChoiceCard : CosmicFragmentPageChoiceCardBase
{
    public override CosmicFragmentPageMode PageMode => CosmicFragmentPageMode.OtherworldlyEcho;

    protected override string PortraitFileName => "cosmic_fragment_otherworldly_echo_choice_card.png";
}
