using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace LibraryOfRuina.content.abnormalities.QueenBee;

/// <summary>
/// 下回合迅捷：不立刻生效；在下个玩家回合开始时移除自身并给予等量迅捷。
/// 参考下回合力量（NEXT_TURN_STRENGTH_POWER）的转换时机。
/// </summary>
public sealed class QueenBeeNextTurnQuicknessPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId =>
        "QUEEN_BEE_NEXT_TURN_QUICKNESS_POWER";

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new[] { HoverTipFactory.FromPower<LibraryQuicknessPower>() };

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
        await PowerCmdCompat.Apply<LibraryQuicknessPower>(
            Owner,
            nextTurnAmount,
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
