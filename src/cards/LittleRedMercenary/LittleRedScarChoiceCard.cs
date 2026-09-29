using LibraryOfRuina.helpers;
using LibraryOfRuina.relics.LittleRedMercenary;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.LittleRedMercenary;

[CardPool(typeof(TokenCardPool))]
public sealed class LittleRedScarChoiceCard : LittleRedMercenaryPageChoiceCardBase
{
    public override LittleRedMercenaryPageMode PageMode => LittleRedMercenaryPageMode.Scar;

    protected override string PortraitFileName => "little_red_scar_choice_card.png";
}
