using LibraryOfRuina.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.WrathServant;

[CardPool(typeof(TokenCardPool))]
public sealed class WrathServantVenomChoiceCard : WrathServantPageChoiceCardBase
{
    protected override string PortraitFileName => "wrath_servant_venom_choice_card.png";
}
