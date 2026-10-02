using System;
using System.Threading.Tasks;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.abnormalities.ScarecrowSearchingForWisdom;

public sealed class ScarecrowWisdomPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override bool AllowNegative => false;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        ..HoverTipFactory.FromCardWithCardHoverTips<ScarecrowWisdomStatusCard>()
    ];

#if STS2_0_111_0
    public override decimal ModifyDamageAdditive(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
#else
    public override decimal ModifyDamageAdditive(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
#endif
    {
        if (target != Owner
            || Amount <= 0
            || dealer?.Monster is not ScarecrowSearchingForWisdom scarecrow
            || !scarecrow.IsHarvestWisdomMoveQueued()
            || !ValuePropCompat.IsPoweredAttack(props))
        {
            return 0m;
        }

        return -Math.Min(amount, Amount);
    }

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        return RefreshScarecrowIntents();
    }

    public override Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        return ReferenceEquals(power, this)
            ? RefreshScarecrowIntents()
            : Task.CompletedTask;
    }

    private async Task RefreshScarecrowIntents()
    {
        if (NCombatRoom.Instance == null || Owner.CombatState == null)
        {
            return;
        }

        foreach (Creature enemy in Owner.CombatState.Enemies)
        {
            if (!enemy.IsAlive
                || enemy.Monster is not ScarecrowSearchingForWisdom
                || NCombatRoom.Instance.GetCreatureNode(enemy) is not { } creatureNode)
            {
                continue;
            }

            await creatureNode.RefreshIntents();
        }
    }
}
