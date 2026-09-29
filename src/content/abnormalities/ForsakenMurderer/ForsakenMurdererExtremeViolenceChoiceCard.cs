using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.content.abnormalities.ForsakenMurderer;

[CardPool(typeof(TokenCardPool))]
public sealed class ForsakenMurdererExtremeViolenceChoiceCard : ForsakenMurdererPageChoiceCardBase
{
    public override ForsakenMurdererPageMode PageMode => ForsakenMurdererPageMode.ExtremeViolence;

    protected override string PortraitFileName => "forsaken_murderer_extreme_violence_choice_card.png";
}
