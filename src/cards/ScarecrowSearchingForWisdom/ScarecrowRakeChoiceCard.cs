using LibraryOfRuina.helpers;
using LibraryOfRuina.relics.ScarecrowSearchingForWisdom;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.ScarecrowSearchingForWisdom;

[CardPool(typeof(TokenCardPool))]
public sealed class ScarecrowRakeChoiceCard : ScarecrowPageChoiceCardBase
{
    public override ScarecrowPageMode PageMode => ScarecrowPageMode.Rake;

    protected override string PortraitFileName => "scarecrow_rake_choice_card.png";
}
