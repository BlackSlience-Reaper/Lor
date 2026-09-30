using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.content.abnormalities.QueenBee;

[CardPool(typeof(TokenCardPool))]
public sealed class QueenBeeSporeChoiceCard : QueenBeePageChoiceCardBase
{
    public override QueenBeePageMode PageMode => QueenBeePageMode.Spore;

    protected override string PortraitFileName => "queen_bee_spore_choice_card.png";
}
