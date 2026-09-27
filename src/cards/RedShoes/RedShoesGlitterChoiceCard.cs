using LibraryOfRuina.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.RedShoes;

[CardPool(typeof(TokenCardPool))]
public sealed class RedShoesGlitterChoiceCard : RedShoesPageChoiceCardBase
{
    protected override string PortraitFileName => "red_shoes_glitter_choice_card.png";
}
