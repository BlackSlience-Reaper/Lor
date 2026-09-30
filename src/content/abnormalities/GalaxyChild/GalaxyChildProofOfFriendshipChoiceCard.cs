using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.content.abnormalities.GalaxyChild;

[CardPool(typeof(TokenCardPool))]
public sealed class GalaxyChildProofOfFriendshipChoiceCard : GalaxyChildPageChoiceCardBase
{
    public override GalaxyChildPageMode PageMode => GalaxyChildPageMode.ProofOfFriendship;

    protected override string PortraitFileName => "galaxy_child_proof_of_friendship_choice_card.png";
}
