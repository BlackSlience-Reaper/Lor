using LibraryOfRuina.helpers;
using LibraryOfRuina.relics.Leticia;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.Leticia;

[CardPool(typeof(TokenCardPool))]
public sealed class LeticiaPageMischiefChoiceCard : LeticiaPageChoiceCardBase
{
    public override LeticiaPageMode PageMode => LeticiaPageMode.Mischief;

    protected override string PortraitFileName => "leticia_page_mischief_choice.png";
}
