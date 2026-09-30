using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Powers;

namespace LibraryOfRuina.content.guests.DawnOffice;

public sealed class LibraryOfRuinaNextTurnStrength : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "NEXT_TURN_STRENGTH_POWER";

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new[] { HoverTipFactory.FromPower<StrengthPower>() };

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext _, Player player)
    {
        if (Owner.IsDead)
        {
            return;
        }

        if (Owner.IsPlayer && player != Owner.Player)
        {
            return;
        }

        int nextTurnStrength = AmountOnTurnStart != 0 ? AmountOnTurnStart : Amount;
        if (nextTurnStrength <= 0)
        {
            return;
        }

        await PowerCmdCompat.Apply<StrengthPower>(Owner, nextTurnStrength, Owner, null);
        await PowerCmd.Remove(this);
        await RefreshOwnerMonsterIntents();
    }

    private async Task RefreshOwnerMonsterIntents()
    {
        if (!Owner.IsMonster || !Owner.IsAlive || Owner.CombatState == null)
        {
            return;
        }

        if (CombatQueries.CreatureNodeOf(this) is { } creatureNode)
        {
            await creatureNode.RefreshIntents();
        }
    }
}
