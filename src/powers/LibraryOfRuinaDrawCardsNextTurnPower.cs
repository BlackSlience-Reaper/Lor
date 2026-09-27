using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace LibraryOfRuina.powers;

public sealed class LibraryOfRuinaDrawCardsNextTurnPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "LIBRARY_OF_RUINA_DRAW_CARDS_NEXT_TURN_POWER";

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override decimal ModifyHandDraw(Player player, decimal count)
    {
        if (player != Owner.Player)
        {
            return count;
        }

        if (AmountOnTurnStart == 0)
        {
            return count;
        }

        return count - Amount;
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (Owner.IsDead)
        {
            return;
        }

        if (Owner.IsPlayer && player != Owner.Player)
        {
            return;
        }

        if (AmountOnTurnStart != 0)
        {
            await PowerCmd.Remove(this);
        }
    }
}
