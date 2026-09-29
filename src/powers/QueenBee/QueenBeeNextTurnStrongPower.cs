using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using LibraryOfRuina.framework.powers;

namespace LibraryOfRuina.powers.QueenBee;

/// <summary>
/// 下回合强壮：不立刻生效；在下个玩家回合开始时移除自身并给予等量强壮。
/// 参考下回合力量（NEXT_TURN_STRENGTH_POWER）的转换时机。
/// </summary>
public sealed class QueenBeeNextTurnStrongPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId =>
        "QUEEN_BEE_NEXT_TURN_STRONG_POWER";

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new[] { HoverTipFactory.FromPower<LibraryStrongPower>() };

    public override async Task AfterPlayerTurnStart(
        PlayerChoiceContext _,
        Player player)
    {
        if (Owner.IsDead)
        {
            return;
        }

        if (Owner.IsPlayer && player != Owner.Player)
        {
            return;
        }

        int nextTurnAmount =
            AmountOnTurnStart != 0 ? AmountOnTurnStart : Amount;
        if (nextTurnAmount <= 0)
        {
            return;
        }

        Creature? applier = Applier;
        Flash();
        await PowerCmd.Remove(this);
        await LibraryPowerCmd.Apply<LibraryStrongPower>(
            Owner,
            nextTurnAmount,
            0,
            applier ?? Owner,
            null);
        await RefreshOwnerMonsterIntents();
    }

    private async Task RefreshOwnerMonsterIntents()
    {
        if (!Owner.IsMonster
            || !Owner.IsAlive
            || Owner.CombatState == null)
        {
            return;
        }

        if (NCombatRoom.Instance?.GetCreatureNode(Owner) is { } creatureNode)
        {
            await creatureNode.RefreshIntents();
        }
    }
}
