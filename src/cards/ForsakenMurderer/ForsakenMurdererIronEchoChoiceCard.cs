using LibraryOfRuina.helpers;
using LibraryOfRuina.relics.ForsakenMurderer;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.ForsakenMurderer;

[CardPool(typeof(TokenCardPool))]
public sealed class ForsakenMurdererIronEchoChoiceCard : ForsakenMurdererPageChoiceCardBase
{
    public override ForsakenMurdererPageMode PageMode => ForsakenMurdererPageMode.IronEcho;

    protected override string PortraitFileName => "forsaken_murderer_iron_echo_choice_card.png";
}
