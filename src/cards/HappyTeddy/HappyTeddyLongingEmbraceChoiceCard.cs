using System.Linq;
using LibraryOfRuina.helpers;
using LibraryLib.Powers;
using LibraryOfRuina.relics.HappyTeddy;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.HappyTeddy;

[CardPool(typeof(TokenCardPool))]
public sealed class HappyTeddyLongingEmbraceChoiceCard : HappyTeddyPageChoiceCardBase
{
    public override HappyTeddyPageMode PageMode => HappyTeddyPageMode.LongingEmbrace;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        base.ExtraHoverTips.Append(HoverTipFactory.FromPower<LibraryStrongSlashPower>());

    protected override string PortraitFileName => "happy_teddy_longing_embrace_choice_card.png";
}
