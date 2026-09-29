using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.relics.ForsakenMurderer;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.ForsakenMurderer;

[CardPool(typeof(TokenCardPool))]
public sealed class ForsakenMurdererBoundWrathChoiceCard : ForsakenMurdererPageChoiceCardBase
{
    public override ForsakenMurdererPageMode PageMode => ForsakenMurdererPageMode.BoundWrath;

    protected override string PortraitFileName => "forsaken_murderer_bound_wrath_choice_card.png";
}
