using System;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.monsters.TechnologyFloorLiberation;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.powers.TechnologyFloorLiberation;

public sealed class TechnologyFloorMagicBulletPower : LibraryOfRuinaPowerModel, ILibraryAbstractModel
{
    private const int MinimumChaoValue = 1;
    private const decimal ChaoDamageCapEpsilon = 0.0001m;

    protected override string LegacyPowerId => "TECHNOLOGY_FLOOR_MAGIC_BULLET_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    public override bool ShouldPowerBeRemovedAfterOwnerDeath() => false;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("MinimumChao", MinimumChaoValue),
        new DynamicVar("HpLoss", TechnologyFloorMagicBulletBoss.HpLossOnTransition),
        new PowerVar<StrengthPower>("Strength", TechnologyFloorMagicBulletBoss.InternalPhaseTransitionStrengthGain)
    ];

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (side != Owner.Side || Owner.IsDead)
        {
            return;
        }

        if (Owner.Monster is not TechnologyFloorMagicBulletBoss boss)
        {
            return;
        }

        if (Owner is not LibraryCreature lc)
        {
            return;
        }

        if (lc.CurrentChaoValue > MinimumChaoValue)
        {
            return;
        }

        Flash();
        await boss.TriggerInternalPhaseTransition();
    }

    public override decimal ModifyChaoDamageCap(
        Creature? target,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay,
        LibraryDamageType type)
    {
        if (target != Owner
            || Owner.IsDead
            || Owner.Monster is not TechnologyFloorMagicBulletBoss { IsBypassingStaggerClamp: false }
            || target is not LibraryCreature lc
            || !lc.HasChaoResistance
            || lc.CurrentChaoValue <= 0)
        {
            return decimal.MaxValue;
        }

        decimal maxFinalLossBeforeTruncation = Math.Max(0m, lc.CurrentChaoValue - ChaoDamageCapEpsilon);
        decimal multiplier = GetChaoDamageMultiplier(lc, props, type);
        return multiplier <= 0m
            ? decimal.MaxValue
            : maxFinalLossBeforeTruncation / multiplier;
    }

    public override async Task AfterCurrentChaoValueChanged(
        Creature target,
        decimal amount,
        LibraryDamageType type)
    {
        if (target != Owner
            || amount >= 0m
            || Owner.IsDead
            || Owner.Monster is not TechnologyFloorMagicBulletBoss { IsBypassingStaggerClamp: false }
            || target is not LibraryCreature lc
            || !lc.HasChaoResistance
            || lc.CurrentChaoValue >= MinimumChaoValue)
        {
            return;
        }

        await LibraryCreatureCmd.SetCurrentChaoValue(lc, MinimumChaoValue);
    }

    private static decimal GetChaoDamageMultiplier(
        LibraryCreature target,
        ValueProp props,
        LibraryDamageType type)
    {
        if (!props.HasFlag(ValueProp.Move) || props.HasFlag(ValueProp.Unpowered))
        {
            return 1m;
        }

        return target.GetChaosResistanceLevel(type).GetMultiplier();
    }
}
