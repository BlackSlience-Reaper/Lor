using LibraryOfRuina.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.WrathServant;

[CardPool(typeof(TokenCardPool))]
public sealed class WrathServantWrathChoiceCard : WrathServantPageChoiceCardBase
{
    protected override string PortraitFileName => "wrath_servant_wrath_choice_card.png";
}
