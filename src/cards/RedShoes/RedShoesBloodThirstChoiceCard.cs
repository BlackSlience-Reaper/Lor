using LibraryOfRuina.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.RedShoes;

[CardPool(typeof(TokenCardPool))]
public sealed class RedShoesBloodThirstChoiceCard : RedShoesPageChoiceCardBase
{
    protected override string PortraitFileName => "red_shoes_blood_thirst_choice_card.png";
}
