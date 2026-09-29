using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace LibraryOfRuina.framework.powers;

/// <summary>积累待生效活力，在下一回合开始时一次性转化，避免同回合后续攻击提前受益。</summary>
public sealed class NextTurnVigorPower : LibraryOfRuinaPowerModel
{
    private const int DelayRounds = 1; // 下回合活力：施加后的回合延迟。

    public int ActivationRound { get; private set; }

    protected override string LegacyPowerId => "NEXT_TURN_VIGOR_POWER";

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerInstanceType InstanceType => PowerInstanceType.None;

    public override bool AllowNegative => false;

    public override string PackedIconPath => ModelDb.Power<VigorPower>().PackedIconPath;

    public override string ResolvedBigIconPath => ModelDb.Power<VigorPower>().ResolvedBigIconPath;

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<VigorPower>()];

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        ActivationRound = (Owner.CombatState?.RoundNumber ?? 0) + DelayRounds;
        return Task.CompletedTask;
    }

    public override async Task BeforeSideTurnStart(PlayerChoiceContext context, CombatSide side,
        IReadOnlyList<Creature> participants, CombatStateLike state)
    {
        if (side != CombatSide.Player || !Owner.IsAlive || Amount <= 0
            || state.RoundNumber < ActivationRound
            || CombatManager.Instance.PlayersTakingExtraTurn.Count > 0)
        {
            return;
        }
        Creature owner = Owner;
        Creature applier = Applier is { IsAlive: true } livingApplier ? livingApplier : owner;
        int vigor = Amount;
        Flash();
        await PowerCmd.Remove(this);
        await PowerCmdCompat.Apply<VigorPower>(context, owner, vigor, applier, null);
        if (owner.IsMonster && NCombatRoom.Instance?.GetCreatureNode(owner) is { } node)
        {
            await node.RefreshIntents();
        }
    }
}
