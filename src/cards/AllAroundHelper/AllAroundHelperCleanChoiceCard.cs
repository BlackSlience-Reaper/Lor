using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.relics.AllAroundHelper;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.AllAroundHelper;

[CardPool(typeof(TokenCardPool))]
public sealed class AllAroundHelperCleanChoiceCard : AllAroundHelperPageChoiceCardBase
{
    public override AllAroundHelperPageMode PageMode => AllAroundHelperPageMode.Clean;

    protected override string PortraitFileName => "all_around_helper_clean_choice_card.png";
}
