#if STS2_0_111_0
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
namespace LibraryOfRuina.content.specialguests.Iori;
public sealed partial class IoriUnpredictableEnchantment
{
    public override CardLocation ModifyCardPlayResultLocation(CardModel card, bool isAutoPlay, ResourceInfo resources, CardLocation location)
    {
        if (card != Card || !isAutoPlay) return location;
        location.player = Card.Owner;
        location.pileType = PileType.Hand;
        location.position = CardPilePosition.Top;
        return location;
    }
}
#endif
