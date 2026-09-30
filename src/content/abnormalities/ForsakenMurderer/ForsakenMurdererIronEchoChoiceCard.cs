using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.content.abnormalities.ForsakenMurderer;

[CardPool(typeof(TokenCardPool))]
public sealed class ForsakenMurdererIronEchoChoiceCard : ForsakenMurdererPageChoiceCardBase
{
    public override ForsakenMurdererPageMode PageMode => ForsakenMurdererPageMode.IronEcho;

    protected override string PortraitFileName => "forsaken_murderer_iron_echo_choice_card.png";
}
