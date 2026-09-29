using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.content.abnormalities.ScarecrowSearchingForWisdom;

[CardPool(typeof(TokenCardPool))]
public sealed class ScarecrowHarvestChoiceCard : ScarecrowPageChoiceCardBase
{
    public override ScarecrowPageMode PageMode => ScarecrowPageMode.Harvest;

    protected override string PortraitFileName => "scarecrow_harvest_choice_card.png";
}
