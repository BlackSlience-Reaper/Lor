using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.content.abnormalities.RedShoes;

[CardPool(typeof(TokenCardPool))]
public sealed class RedShoesAxeChoiceCard : RedShoesPageChoiceCardBase
{
    public override RedShoesPageMode PageMode => RedShoesPageMode.Axe;

    protected override string PortraitFileName => "red_shoes_axe_choice_card.png";
}
