using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.compat;
using LibraryOfRuina.guests.DawnOffice;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace LibraryOfRuina.powers.DawnOffice;

public sealed class LibraryOfRuinaInkOverPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "INK_OVER_POWER";

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override async Task BeforeSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Player || Owner.IsDead)
        {
            return;
        }

        int handCount = CombatState.Players
            .Select(player => PileType.Hand.GetPile(player).Cards.Count)
            .DefaultIfEmpty(0)
            .Max();
        if (handCount <= 0)
        {
            return;
        }

        await PowerCmdCompat.Apply<LibraryOfRuinaNextTurnStrength>(Owner, handCount, Owner, null);
    }
}
