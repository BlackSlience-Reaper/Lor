#if STS2_0_111_0
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
namespace LibraryOfRuina.content.liberation.Religion;
public sealed partial class ReligionFloorAwePower
{
    public override CardLocation ModifyCardPlayResultLocation(CardModel card, bool isAutoPlay, ResourceInfo resources, CardLocation location)
    {
        if (card.Owner == Owner.Player && Amount > 0 && location.pileType == PileType.Discard
            && Encounter is { Completed: false, IsSettling: false })
        {
            return location with { pileType = PileType.Play };
        }
        return location;
    }

    public override Task AfterModifyingCardPlayResultLocation(CardModel card, CardLocation location)
    {
        if (card.Owner == Owner.Player && location.pileType == PileType.Play)
        {
            Encounter?.ReserveSacrifice(card);
        }
        return Task.CompletedTask;
    }
}
#endif
