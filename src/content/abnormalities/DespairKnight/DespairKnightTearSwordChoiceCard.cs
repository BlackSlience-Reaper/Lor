using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.content.abnormalities.DespairKnight;

[CardPool(typeof(TokenCardPool))]
public sealed class DespairKnightTearSwordChoiceCard : DespairKnightPageChoiceCardBase
{
    public override DespairKnightPageMode PageMode => DespairKnightPageMode.TearSword;

    protected override string PortraitFileName => "despair_knight_tear_sword_choice_card.png";
}
