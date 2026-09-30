using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.content.abnormalities.RedShoes;

[CardPool(typeof(TokenCardPool))]
public sealed class RedShoesGlitterChoiceCard : RedShoesPageChoiceCardBase
{
    public override RedShoesPageMode PageMode => RedShoesPageMode.Glitter;

    protected override string PortraitFileName => "red_shoes_glitter_choice_card.png";
}
