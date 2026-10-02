#if STS2_0_107_1
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
namespace LibraryOfRuina.content.specialguests.Iori;
public sealed partial class IoriUnpredictableEnchantment
{
    public override (PileType, CardPilePosition) ModifyCardPlayResultPileTypeAndPosition(CardModel card, bool isAutoPlay, ResourceInfo resources, PileType pileType, CardPilePosition position)
        => card == Card && isAutoPlay ? (PileType.Hand, CardPilePosition.Top) : (pileType, position);
}
#endif
