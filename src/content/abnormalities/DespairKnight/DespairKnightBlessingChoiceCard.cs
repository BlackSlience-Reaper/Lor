using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.content.abnormalities.DespairKnight;

[CardPool(typeof(TokenCardPool))]
public sealed class DespairKnightBlessingChoiceCard : DespairKnightPageChoiceCardBase
{
    public override DespairKnightPageMode PageMode => DespairKnightPageMode.Blessing;

    protected override string PortraitFileName => "despair_knight_blessing_choice_card.png";
}
