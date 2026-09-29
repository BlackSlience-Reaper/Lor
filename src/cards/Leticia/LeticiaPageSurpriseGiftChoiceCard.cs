using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.relics.Leticia;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.Leticia;

[CardPool(typeof(TokenCardPool))]
public sealed class LeticiaPageSurpriseGiftChoiceCard : LeticiaPageChoiceCardBase
{
    public override LeticiaPageMode PageMode => LeticiaPageMode.SurpriseGift;

    protected override string PortraitFileName => "leticia_page_surprise_gift_choice.png";
}
