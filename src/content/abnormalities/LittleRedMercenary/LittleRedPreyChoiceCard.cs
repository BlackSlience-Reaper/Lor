using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.content.abnormalities.LittleRedMercenary;

[CardPool(typeof(TokenCardPool))]
public sealed class LittleRedPreyChoiceCard : LittleRedMercenaryPageChoiceCardBase
{
    public override LittleRedMercenaryPageMode PageMode => LittleRedMercenaryPageMode.Prey;

    protected override string PortraitFileName => "little_red_prey_choice_card.png";
}
