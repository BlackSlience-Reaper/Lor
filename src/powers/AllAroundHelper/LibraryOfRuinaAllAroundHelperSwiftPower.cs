using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using LibraryOfRuina.framework.powers;

namespace LibraryOfRuina.powers.AllAroundHelper;

public sealed class LibraryOfRuinaAllAroundHelperSwiftPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "ALL_AROUND_HELPER_SWIFT_POWER";

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

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

        int drawAmount = AmountOnTurnStart != 0 ? AmountOnTurnStart : Amount;
        if (drawAmount > 0 && Owner.Player != null)
        {
            await CardPileCmd.Draw(choiceContext, drawAmount, Owner.Player);
        }

        await PowerCmd.Remove(this);
    }
}
