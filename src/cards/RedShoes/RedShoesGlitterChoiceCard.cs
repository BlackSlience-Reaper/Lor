using LibraryOfRuina.helpers;
using LibraryOfRuina.relics.RedShoes;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.RedShoes;

[CardPool(typeof(TokenCardPool))]
public sealed class RedShoesGlitterChoiceCard : RedShoesPageChoiceCardBase
{
    public override RedShoesPageMode PageMode => RedShoesPageMode.Glitter;

    protected override string PortraitFileName => "red_shoes_glitter_choice_card.png";
}
