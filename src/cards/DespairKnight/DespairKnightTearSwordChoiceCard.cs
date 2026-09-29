using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.relics.DespairKnight;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.DespairKnight;

[CardPool(typeof(TokenCardPool))]
public sealed class DespairKnightTearSwordChoiceCard : DespairKnightPageChoiceCardBase
{
    public override DespairKnightPageMode PageMode => DespairKnightPageMode.TearSword;

    protected override string PortraitFileName => "despair_knight_tear_sword_choice_card.png";
}
