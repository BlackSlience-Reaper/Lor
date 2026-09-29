using LibraryOfRuina.helpers;
using LibraryOfRuina.relics.WrathServant;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.WrathServant;

[CardPool(typeof(TokenCardPool))]
public sealed class WrathServantVenomChoiceCard : WrathServantPageChoiceCardBase
{
    public override WrathServantPageMode PageMode => WrathServantPageMode.Venom;

    protected override string PortraitFileName => "wrath_servant_venom_choice_card.png";
}
