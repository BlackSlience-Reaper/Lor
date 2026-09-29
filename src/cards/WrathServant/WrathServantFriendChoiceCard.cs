using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.relics.WrathServant;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.WrathServant;

[CardPool(typeof(TokenCardPool))]
public sealed class WrathServantFriendChoiceCard : WrathServantPageChoiceCardBase
{
    public override WrathServantPageMode PageMode => WrathServantPageMode.Friend;

    protected override string PortraitFileName => "wrath_servant_friend_choice_card.png";
}
