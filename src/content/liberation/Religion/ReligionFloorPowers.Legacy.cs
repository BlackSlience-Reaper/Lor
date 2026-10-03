#if STS2_0_107_1
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
namespace LibraryOfRuina.content.liberation.Religion;
public sealed partial class ReligionFloorAwePower
{
    public override (PileType, CardPilePosition) ModifyCardPlayResultPileTypeAndPosition(CardModel card, bool isAutoPlay, ResourceInfo resources, PileType pileType, CardPilePosition position)
    {
        if (card.Owner == Owner.Player && Amount > 0 && pileType == PileType.Discard
            && Encounter is { Completed: false, IsSettling: false })
        {
            return (PileType.Play, position);
        }
        return (pileType, position);
    }

    public override Task AfterModifyingCardPlayResultPileOrPosition(CardModel card, PileType pileType, CardPilePosition position)
    {
        if (card.Owner == Owner.Player && pileType == PileType.Play)
        {
            Encounter?.ReserveSacrifice(card);
        }
        return Task.CompletedTask;
    }
}
#endif
