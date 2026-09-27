using LibraryOfRuina.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.AllAroundHelper;

[CardPool(typeof(TokenCardPool))]
public sealed class AllAroundHelperCleanChoiceCard : AllAroundHelperPageChoiceCardBase
{
    protected override string PortraitFileName => "all_around_helper_clean_choice_card.png";
}
