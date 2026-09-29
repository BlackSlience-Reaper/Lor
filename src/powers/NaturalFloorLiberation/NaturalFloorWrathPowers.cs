using System;
using System.Threading.Tasks;
using LibraryOfRuina.encounters.NaturalFloorLiberation;
using LibraryOfRuina.monsters.NaturalFloorLiberation;
using LibraryOfRuina.powers.WrathServant;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.powers.NaturalFloorLiberation;

public sealed class NaturalFloorLiberationControllerPower : NaturalFloorGreenPassivePower
{
    protected override string LegacyPowerId => "NATURAL_FLOOR_LIBERATION_CONTROLLER_POWER";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new NaturalFloorNameVar("FloorName", "encounters", "NATURAL_FLOOR_LIBERATION_ENCOUNTER.floorName")];

    protected override bool IsVisibleInternal => false;

    public override bool ShouldPowerBeRemovedAfterOwnerDeath() => false;

    private NaturalFloorLiberationEncounter? Encounter => Owner.CombatState?.Encounter as NaturalFloorLiberationEncounter;

    public override bool ShouldStopCombatFromEnding() => Encounter?.KeepCombatOpen() == true;

    public override bool ShouldCreatureBeRemovedFromCombatAfterDeath(Creature creature) =>
        Encounter?.ShouldKeepPhaseBossAfterDeath(creature) != true;

    public override bool ShouldAllowHitting(Creature creature) => Encounter?.SuppressPhaseInteraction(creature) != true;

    public override bool ShouldAllowTargeting(Creature target) => Encounter?.SuppressPhaseInteraction(target) != true;

    public override bool ShouldDieLate(Creature creature) =>
        Encounter?.ShouldPreventPlayerDeath(creature) != true
        && Encounter?.SuppressTransition(creature) != true;

    public override Task AfterPreventingDeath(Creature creature) => Encounter?.OnPreventingDeath(creature) ?? Task.CompletedTask;

    public override Task BeforeSideTurnStart(
        PlayerChoiceContext context,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState) =>
        Encounter?.OnBeforeSideTurnStart(side) ?? Task.CompletedTask;

    public override Task AfterAttack(PlayerChoiceContext context, MegaCrit.Sts2.Core.Commands.Builders.AttackCommand command) =>
        Encounter?.ReplaceBrokenNihilStatues() ?? Task.CompletedTask;

    public override Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay) =>
        Encounter?.ReplaceBrokenNihilStatues() ?? Task.CompletedTask;

    public override Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState) =>
        side == CombatSide.Player
            ? Encounter?.GrantNihilDeferredBlock() ?? Task.CompletedTask
            : Task.CompletedTask;

    public override Task AfterDeath(
        PlayerChoiceContext context,
        Creature creature,
        bool prevented,
        float deathAnimLength) =>
        Encounter?.OnSecondPhaseDeath(creature, prevented) ?? Task.CompletedTask;

    public override Task AfterSideTurnEndLate(
        PlayerChoiceContext context,
        CombatSide side,
        IEnumerable<Creature> participants) =>
        Encounter?.OnAfterSideTurnEnd() ?? Task.CompletedTask;
}

public sealed class NaturalFloorTodayPlayPower : NaturalFloorGreenPassivePower
{
    protected override string LegacyPowerId => "NATURAL_FLOOR_TODAY_PLAY_POWER";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("HpLossPercent", NaturalFloorLiberationEncounter.FailureHpLossPercent),
        new NaturalFloorNameVar("HermitName", "monsters", "NATURAL_FLOOR_GREEN_STEM_HERMIT.name")
    ];
}

public sealed class NaturalFloorBlindRagePower : NaturalFloorGreenPassivePower
{
    protected override string LegacyPowerId => "NATURAL_FLOOR_BLIND_RAGE_POWER";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new NaturalFloorNameVar("StaffName", "monsters", "NATURAL_FLOOR_HERMIT_STAFF.name"),
        new NaturalFloorNameVar("HermitName", "monsters", "NATURAL_FLOOR_GREEN_STEM_HERMIT.name")
    ];
}

public sealed class NaturalFloorSinnerPower : NaturalFloorGreenPassivePower
{
    protected override string LegacyPowerId => "NATURAL_FLOOR_SINNER_POWER";

    public override PowerStackType StackType => PowerStackType.Counter;

    public override int DisplayAmount => Math.Clamp(Amount - 1, 0, NaturalFloorBlindRageBoss.DamageThreshold);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Threshold", NaturalFloorBlindRageBoss.DamageThreshold),
        new DynamicVar("Interval", 2),
        new NaturalFloorNameVar("StaffName", "monsters", "NATURAL_FLOOR_HERMIT_STAFF.name")
    ];

    public override Task AfterCurrentHpChanged(Creature creature, decimal delta)
    {
        if (creature == Owner && Owner.Monster is NaturalFloorBlindRageBoss rage)
        {
            rage.CountHpLoss(delta);
        }

        return Task.CompletedTask;
    }
}

public sealed class NaturalFloorExploitedPower : NaturalFloorGreenPassivePower, LibraryOfRuina.infra.helpers.IFinalHpLossClamp
{
    protected override string LegacyPowerId => "NATURAL_FLOOR_EXPLOITED_POWER";

    public const int MinimumHp = 30;

    public const int RecoveryPercent = 25;

    internal bool IsHealthBarLockActive => Owner.CurrentHp <= MinimumHp;

    private sealed class Data
    {
        internal bool LethalRageDamage;
    }

    protected override object InitInternalData() => new Data();

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("MinHp", MinimumHp),
        new DynamicVar("RecoveryPercent", RecoveryPercent)
    ];

    public decimal ClampFinalHpLoss(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (target != Owner || amount <= 0 || dealer?.Monster is NaturalFloorBlindRageBoss)
        {
            return amount;
        }

        return Math.Min(amount, Math.Max(0, Owner.CurrentHp - MinimumHp));
    }

    public override Task AfterDamageGiven(
        PlayerChoiceContext context,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        if (target == Owner)
        {
            // Both damage pipelines report the actual result before resolving death.
            // HP-loss modifiers also run during health-bar previews and must stay pure.
            GetInternalData<Data>().LethalRageDamage =
                dealer?.Monster is NaturalFloorBlindRageBoss && result.WasTargetKilled;
        }

        return Task.CompletedTask;
    }

    public override bool ShouldDieLate(Creature creature) => creature != Owner
        || Owner.CombatState?.Encounter is NaturalFloorLiberationEncounter { SettlementTriggered: true }
        || GetInternalData<Data>().LethalRageDamage;

    public override Task AfterPreventingDeath(Creature creature) =>
        creature == Owner ? CreatureCmd.SetCurrentHp(Owner, MinimumHp) : Task.CompletedTask;

    public override Task AfterCurrentHpChanged(Creature creature, decimal delta)
    {
        if (creature == Owner && delta > 0)
        {
            GetInternalData<Data>().LethalRageDamage = false;
        }

        return Task.CompletedTask;
    }

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext context,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Enemy && Owner.IsAlive && Owner.CurrentHp <= MinimumHp)
        {
            Flash();
            await CreatureCmd.Heal(Owner, Math.Ceiling(Owner.MaxHp * RecoveryPercent / 100m));
        }
    }
}

public sealed class NaturalFloorDearFriendPower : NaturalFloorGreenPassivePower
{
    protected override string LegacyPowerId => "NATURAL_FLOOR_DEAR_FRIEND_POWER";

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<LibraryBurnPower>(),
        HoverTipFactory.FromPower<LibraryBleedingPower>(),
        HoverTipFactory.FromPower<WrathServantCorrosionPower>()
    ];

    public override bool TryModifyPowerAmountReceived(
        PowerModel canonicalPower,
        Creature target,
        decimal amount,
        Creature? applier,
        out decimal modifiedAmount)
    {
        modifiedAmount = amount;
        if (target != Owner || amount <= 0 || !IsImmunePower(canonicalPower))
        {
            return false;
        }

        modifiedAmount = 0;
        return true;
    }

    public override Task AfterModifyingPowerAmountReceived(PowerModel power)
    {
        Flash();
        return Task.CompletedTask;
    }

    private static bool IsImmunePower(PowerModel power) => power is LibraryBurnPower or LibraryBleedingPower
        or WrathServantCorrosionPower or WrathServantNextTurnCorrosionPower;
}

public sealed class NaturalFloorTwoWorldsPower : NaturalFloorGreenPassivePower
{
    protected override string LegacyPowerId => "NATURAL_FLOOR_TWO_WORLDS_POWER";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new NaturalFloorNameVar("StaffName", "monsters", "NATURAL_FLOOR_HERMIT_STAFF.name"),
        new NaturalFloorNameVar("RageName", "monsters", "NATURAL_FLOOR_BLIND_RAGE_BOSS.name")
    ];
}

public sealed class NaturalFloorStaffPower : NaturalFloorGreenPassivePower
{
    protected override string LegacyPowerId => "NATURAL_FLOOR_STAFF_POWER";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new NaturalFloorNameVar("RageName", "monsters", "NATURAL_FLOOR_BLIND_RAGE_BOSS.name")];
}
