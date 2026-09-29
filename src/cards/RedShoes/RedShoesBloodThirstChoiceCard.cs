using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.relics.RedShoes;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.RedShoes;

[CardPool(typeof(TokenCardPool))]
public sealed class RedShoesBloodThirstChoiceCard : RedShoesPageChoiceCardBase
{
    public override RedShoesPageMode PageMode => RedShoesPageMode.BloodThirst;

    protected override string PortraitFileName => "red_shoes_blood_thirst_choice_card.png";
}
