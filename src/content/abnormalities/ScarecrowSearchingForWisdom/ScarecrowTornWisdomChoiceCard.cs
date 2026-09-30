using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.content.abnormalities.ScarecrowSearchingForWisdom;

[CardPool(typeof(TokenCardPool))]
public sealed class ScarecrowTornWisdomChoiceCard : ScarecrowPageChoiceCardBase
{
    public override ScarecrowPageMode PageMode => ScarecrowPageMode.TornWisdom;

    protected override string PortraitFileName => "scarecrow_torn_wisdom_choice_card.png";
}
