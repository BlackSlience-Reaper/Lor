using LibraryOfRuina.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.LittleRedMercenary;

[CardPool(typeof(TokenCardPool))]
public sealed class LittleRedPreyChoiceCard : LittleRedMercenaryPageChoiceCardBase
{
    protected override string PortraitFileName => "little_red_prey_choice_card.png";
}
