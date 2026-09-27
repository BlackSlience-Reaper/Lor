using LibraryOfRuina.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.WrathServant;

[CardPool(typeof(TokenCardPool))]
public sealed class WrathServantFriendChoiceCard : WrathServantPageChoiceCardBase
{
    protected override string PortraitFileName => "wrath_servant_friend_choice_card.png";
}
