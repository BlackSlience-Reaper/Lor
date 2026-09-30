using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.content.specialguests.Xiao;

public sealed class XiaoYaziVengeancePower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId =>
        "XIAO_YAZI_VENGEANCE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override bool AllowNegative => false;

    public override string PackedIconPath =>
        ImageHelper.GetImagePath("powers/xiao_yazi_vengeance_power.png");

    public override string ResolvedBigIconPath => PackedIconPath;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<LibraryBurnPower>()
    ];

    public override async Task AfterCardPlayed(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        if (Amount <= 0
            || Owner.IsDead
            || cardPlay.Card.Owner.Creature != Owner
            || Owner.CombatState == null)
        {
            return;
        }

        Creature[] targets = Owner.CombatState.HittableEnemies
            .Where(static target => target.IsAlive)
            .OrderBy(static target => target.CombatId)
            .ToArray();
        if (targets.Length == 0)
        {
            return;
        }

        Flash();
        await PowerCmdCompat.Apply<LibraryBurnPower>(
            choiceContext,
            targets,
            Amount,
            Owner,
            cardPlay.Card);
    }

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        _ = choiceContext;
        _ = participants;
        if (side == Owner.Side)
        {
            await PowerCmd.Remove(this);
        }
    }
}

public sealed class XiaoTaotieFeastPower : LibraryOfRuinaPowerModel
{
    private bool _isEchoingBurn;

    protected override string LegacyPowerId =>
        "XIAO_TAOTIE_FEAST_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    public override string PackedIconPath =>
        ImageHelper.GetImagePath("powers/xiao_starfire_power.png");

    public override string ResolvedBigIconPath => PackedIconPath;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<LibraryBurnPower>()
    ];

    public override async Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (_isEchoingBurn
            || amount <= 0m
            || power is not LibraryBurnPower
            || applier != Owner
            || Owner.IsDead
            || Owner.CombatState == null)
        {
            return;
        }

        Creature[] targets = Owner.CombatState.HittableEnemies
            .Where(static target => target.IsAlive)
            .OrderBy(static target => target.CombatId)
            .ToArray();
        if (targets.Length == 0)
        {
            return;
        }

        _isEchoingBurn = true;
        try
        {
            Flash();
            await PowerCmdCompat.Apply<LibraryBurnPower>(
                choiceContext,
                targets,
                amount,
                Owner,
                cardSource);
        }
        finally
        {
            _isEchoingBurn = false;
        }
    }
}
