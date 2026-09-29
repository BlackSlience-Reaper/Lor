using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.content.abnormalities.LittleRedMercenary;

[CardPool(typeof(TokenCardPool))]
public sealed class LittleRedScarChoiceCard : LittleRedMercenaryPageChoiceCardBase
{
    public override LittleRedMercenaryPageMode PageMode => LittleRedMercenaryPageMode.Scar;

    protected override string PortraitFileName => "little_red_scar_choice_card.png";
}
