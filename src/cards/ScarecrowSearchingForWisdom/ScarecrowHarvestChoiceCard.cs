using LibraryOfRuina.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.ScarecrowSearchingForWisdom;

[CardPool(typeof(TokenCardPool))]
public sealed class ScarecrowHarvestChoiceCard : ScarecrowPageChoiceCardBase
{
    protected override string PortraitFileName => "scarecrow_harvest_choice_card.png";
}
