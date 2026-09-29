using System.Linq;
using LibraryOfRuina.infra.helpers;
using LibraryLib.Powers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.content.abnormalities.HappyTeddy;

[CardPool(typeof(TokenCardPool))]
public sealed class HappyTeddyLongingEmbraceChoiceCard : HappyTeddyPageChoiceCardBase
{
    public override HappyTeddyPageMode PageMode => HappyTeddyPageMode.LongingEmbrace;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        base.ExtraHoverTips.Append(HoverTipFactory.FromPower<LibraryStrongSlashPower>());

    protected override string PortraitFileName => "happy_teddy_longing_embrace_choice_card.png";
}
