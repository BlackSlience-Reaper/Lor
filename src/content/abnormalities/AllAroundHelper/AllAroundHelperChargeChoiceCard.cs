using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.content.abnormalities.AllAroundHelper;

[CardPool(typeof(TokenCardPool))]
public sealed class AllAroundHelperChargeChoiceCard : AllAroundHelperPageChoiceCardBase
{
    public override AllAroundHelperPageMode PageMode => AllAroundHelperPageMode.Charge;

    protected override string PortraitFileName => "all_around_helper_charge_choice_card.png";
}
