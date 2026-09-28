using LibraryOfRuina.helpers;
using LibraryOfRuina.relics.ScarecrowSearchingForWisdom;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.ScarecrowSearchingForWisdom;

[CardPool(typeof(TokenCardPool))]
public sealed class ScarecrowHarvestChoiceCard : ScarecrowPageChoiceCardBase
{
    public override ScarecrowPageMode PageMode => ScarecrowPageMode.Harvest;

    protected override string PortraitFileName => "scarecrow_harvest_choice_card.png";
}
