using LibraryOfRuina.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.AllAroundHelper;

[CardPool(typeof(TokenCardPool))]
public sealed class AllAroundHelperChargeChoiceCard : AllAroundHelperPageChoiceCardBase
{
    protected override string PortraitFileName => "all_around_helper_charge_choice_card.png";
}
