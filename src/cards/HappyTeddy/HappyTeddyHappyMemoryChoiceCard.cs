using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.relics.HappyTeddy;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.HappyTeddy;

[CardPool(typeof(TokenCardPool))]
public sealed class HappyTeddyHappyMemoryChoiceCard : HappyTeddyPageChoiceCardBase
{
    public override HappyTeddyPageMode PageMode => HappyTeddyPageMode.HappyMemory;

    protected override string PortraitFileName => "happy_teddy_happy_memory_choice_card.png";
}
