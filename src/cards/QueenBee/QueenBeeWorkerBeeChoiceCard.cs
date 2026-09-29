using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.relics.QueenBee;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.QueenBee;

[CardPool(typeof(TokenCardPool))]
public sealed class QueenBeeWorkerBeeChoiceCard : QueenBeePageChoiceCardBase
{
    public override QueenBeePageMode PageMode => QueenBeePageMode.WorkerBee;

    protected override string PortraitFileName => "queen_bee_worker_bee_choice_card.png";
}
