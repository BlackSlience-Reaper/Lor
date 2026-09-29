using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.content.abnormalities.Leticia;

[CardPool(typeof(TokenCardPool))]
public sealed class LeticiaPageBuddyChoiceCard : LeticiaPageChoiceCardBase
{
    public override LeticiaPageMode PageMode => LeticiaPageMode.Buddy;

    protected override string PortraitFileName => "leticia_page_buddy_choice.png";
}
