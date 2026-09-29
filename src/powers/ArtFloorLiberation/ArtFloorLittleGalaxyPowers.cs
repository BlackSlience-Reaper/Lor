using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.monsters.ArtFloorLiberation;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace LibraryOfRuina.powers.ArtFloorLiberation;

public sealed class ArtFloorLittleGalaxyEternalFarewellPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "ART_FLOOR_LITTLE_GALAXY_ETERNAL_FAREWELL_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("FakeDeathThreshold", 1)
    ];

    public override Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (wasRemovalPrevented
            || creature.Monster is not ArtFloorGalaxyFriend friend
            || friend.IsFakeDead
            || friend.CanEnterFakeDeath(creature)
            || Owner.Monster is not ArtFloorLittleGalaxyBoss boss)
        {
            return Task.CompletedTask;
        }

        if (Owner.CombatState?.Enemies.Any(static enemy =>
                enemy.Monster is ArtFloorGalaxyFriend friend && (enemy.IsAlive || friend.IsFakeDead)) == true)
        {
            return Task.CompletedTask;
        }

        return boss.QueueAllFriendsDeadEgo();
    }

    public override Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        return side == CombatSide.Player && Owner.Monster is ArtFloorLittleGalaxyBoss boss
            ? boss.RefreshTurnStartState()
            : Task.CompletedTask;
    }
}

public sealed class ArtFloorLittleGalaxyPebblePower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "ART_FLOOR_LITTLE_GALAXY_PEBBLE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("HealPercent", 20)
    ];

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (side != CombatSide.Player
            || Owner.IsDead
            || Owner.Monster is not ArtFloorLittleGalaxyBoss boss
            || !boss.IsHealingForm)
        {
            return;
        }

        IReadOnlyList<Creature> targets = combatState.Enemies
            .Where(static enemy => enemy.IsAlive
                && enemy.Monster is ArtFloorLittleGalaxyBoss or ArtFloorGalaxyFriend)
            .ToArray();
        if (targets.Count == 0)
        {
            return;
        }

        Flash();
        foreach (Creature target in targets)
        {
            int healAmount = Math.Max(1, (int)Math.Ceiling(target.MaxHp * boss.PebbleHealPercentage / 100m));
            await CreatureCmd.Heal(target, healAmount);
        }
    }
}

public sealed class ArtFloorGalaxyDoNotLeaveMePower : LibraryFakeDeathPowerModel
{
    protected override string LegacyPowerId => "ART_FLOOR_GALAXY_DO_NOT_LEAVE_ME_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Turns", ArtFloorGalaxyFriend.FakeDeathStunTurns),
        new DynamicVar("Hp", ArtFloorGalaxyFriend.FakeDeathHp),
        new DynamicVar("RecoveryPercent", 80)
    ];

    protected override bool IsOwnerFakeDead =>
        Owner.Monster is ArtFloorGalaxyFriend { IsFakeDead: true };

    protected override bool HoldCombatOpenWhileFakeDead =>
        Owner.Monster is ArtFloorGalaxyFriend
        && ArtFloorGalaxyFriend.ShouldHoldCombatOpenForFakeDeath(Owner.CombatState);

    protected override bool CanEnterFakeDeath(Creature creature) =>
        Owner.Monster is ArtFloorGalaxyFriend friend && friend.CanEnterFakeDeath(creature);

    protected override Task EnterFakeDeath(Creature creature) =>
        Owner.Monster is ArtFloorGalaxyFriend friend ? friend.EnterFakeDeath() : Task.CompletedTask;

    public override bool ShouldOwnerDeathTriggerFatal()
    {
        return Owner.Monster is not ArtFloorGalaxyFriend friend
            || ArtFloorGalaxyFriend.AreAllOtherFriendsDeadOrFakeDead(Owner.CombatState, friend);
    }

    public override Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        return side == CombatSide.Player && Owner.Monster is ArtFloorGalaxyFriend friend
            ? friend.TickFakeDeathOnPlayerTurnStart()
            : Task.CompletedTask;
    }

    public override Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (Owner.Monster is ArtFloorGalaxyFriend { IsFakeDead: true }
            && Owner is LibraryCreature lc
            && lc.HasChaoResistance
            && lc.CurrentChaoValue != 0)
        {
            lc.SetCurrentChaoValueInternal(0m);
            lc.HealthBar?.RefreshValues();
        }

        return Task.CompletedTask;
    }
}
