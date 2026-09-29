using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.relics.LittleRedMercenary;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.LittleRedMercenary;

[CardPool(typeof(TokenCardPool))]
public sealed class LittleRedRevengeChoiceCard : LittleRedMercenaryPageChoiceCardBase
{
    public override LittleRedMercenaryPageMode PageMode => LittleRedMercenaryPageMode.Revenge;

    protected override string PortraitFileName => "little_red_revenge_choice_card.png";
}
