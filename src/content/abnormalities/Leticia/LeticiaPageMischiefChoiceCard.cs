using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.content.abnormalities.Leticia;

[CardPool(typeof(TokenCardPool))]
public sealed class LeticiaPageMischiefChoiceCard : LeticiaPageChoiceCardBase
{
    public override LeticiaPageMode PageMode => LeticiaPageMode.Mischief;

    protected override string PortraitFileName => "leticia_page_mischief_choice.png";
}
