using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.powers.BigBadWolf;

/// <summary>
/// 凶残利爪赋予的“下回合无法被敌方选中”状态，己方回合结束时自移除。
/// </summary>
public sealed class BigBadWolfUntargetablePower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "BIG_BAD_WOLF_UNTARGETABLE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    public override bool ShouldAllowHitting(Creature creature)
    {
        return creature != Owner || CombatState.CurrentSide == Owner.Side;
    }

    public override bool ShouldAllowTargeting(Creature target)
    {
        return target != Owner || CombatState.CurrentSide == Owner.Side;
    }

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Enemy)
        {
            await PowerCmd.Remove(this);
        }
    }
}

/// <summary>
/// 凶残利爪赋予的“该回合每次攻击对目标施加流血”状态，己方回合结束时自移除。
/// </summary>
public sealed class BigBadWolfCruelClawsPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "BIG_BAD_WOLF_CRUEL_CLAWS_POWER";

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    private HashSet<(CardModel Card, uint? TargetCombatId)> _bleedAppliedPairs = [];

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _bleedAppliedPairs = [.. _bleedAppliedPairs];
    }

    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        if (Amount <= 0
            || cardSource == null
            || cardSource.Type != CardType.Attack
            || !IsOwnerDamageSource(dealer)
            || target.Side == Owner.Side
            || LibraryOfRuina.framework.combat.AllyTurnRegistry.IsFriendlyAlly(target))
        {
            return;
        }

        if (!_bleedAppliedPairs.Add((cardSource, target.CombatId)))
        {
            return;
        }

        await PowerCmdCompat.ApplyDebuff<LibraryBleedingPower>(
            target,
            Amount,
            Owner,
            cardSource);
    }

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Enemy)
        {
            _bleedAppliedPairs.Clear();
            await PowerCmd.Remove(this);
        }
    }

    private bool IsOwnerDamageSource(Creature? dealer)
    {
        if (dealer == null)
        {
            return false;
        }

        if (dealer == Owner)
        {
            return true;
        }

        Player? ownerPlayer = Owner.Player ?? Owner.PetOwner;
        return ownerPlayer?.Osty == dealer;
    }
}
