using LibraryOfRuina.helpers;
using LibraryOfRuina.relics.CosmicFragment;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.CosmicFragment;

[CardPool(typeof(TokenCardPool))]
public sealed class CosmicFragmentIncomprehensibleChoiceCard : CosmicFragmentPageChoiceCardBase
{
    public override CosmicFragmentPageMode PageMode => CosmicFragmentPageMode.Incomprehensible;

    protected override string PortraitFileName => "cosmic_fragment_incomprehensible_choice_card.png";
}
