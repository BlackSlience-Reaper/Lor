using LibraryOfRuina.helpers;
using LibraryOfRuina.relics.Leticia;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.Leticia;

[CardPool(typeof(TokenCardPool))]
public sealed class LeticiaPageBuddyChoiceCard : LeticiaPageChoiceCardBase
{
    public override LeticiaPageMode PageMode => LeticiaPageMode.Buddy;

    protected override string PortraitFileName => "leticia_page_buddy_choice.png";
}
