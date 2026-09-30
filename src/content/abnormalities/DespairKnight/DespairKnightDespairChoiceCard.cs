using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.content.abnormalities.DespairKnight;

[CardPool(typeof(TokenCardPool))]
public sealed class DespairKnightDespairChoiceCard : DespairKnightPageChoiceCardBase
{
    public override DespairKnightPageMode PageMode => DespairKnightPageMode.Despair;

    protected override string PortraitFileName => "despair_knight_despair_choice_card.png";
}
