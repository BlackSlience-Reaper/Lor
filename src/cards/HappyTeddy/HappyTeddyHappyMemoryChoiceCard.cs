using LibraryOfRuina.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.HappyTeddy;

[CardPool(typeof(TokenCardPool))]
public sealed class HappyTeddyHappyMemoryChoiceCard : HappyTeddyPageChoiceCardBase
{
    protected override string PortraitFileName => "happy_teddy_happy_memory_choice_card.png";
}
