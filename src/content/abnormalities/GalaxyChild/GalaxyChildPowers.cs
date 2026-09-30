using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.framework.powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace LibraryOfRuina.content.abnormalities.GalaxyChild;

public sealed class GalaxyChildPebblePower : LibraryOfRuinaPowerModel
{
    private const decimal HealRatio = 0.10m;
    private const int HealPercent = 10;

    protected override string LegacyPowerId => "GALAXY_CHILD_PEBBLE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("HealPercent", HealPercent),
        new DynamicVar("MinimumHp", GalaxyFriend.FakeDeathHp)
    ];

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (side != CombatSide.Player || Owner.IsDead || Owner.CurrentHp <= GalaxyFriend.FakeDeathHp)
        {
            return;
        }

        int healAmount = Math.Max(1, (int)Math.Ceiling(Owner.MaxHp * HealRatio));
        Flash();
        await CreatureCmd.Heal(Owner, healAmount);
    }
}

public sealed class GalaxyDoNotLeaveMePower : LibraryFakeDeathPowerModel
{
    protected override string LegacyPowerId => "GALAXY_DO_NOT_LEAVE_ME_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Turns", GalaxyFriend.FakeDeathStunTurns),
        new DynamicVar("Hp", GalaxyFriend.FakeDeathHp)
    ];

    protected override bool IsOwnerFakeDead =>
        Owner.Monster is GalaxyFriend { IsFakeDead: true };

    protected override bool HoldCombatOpenWhileFakeDead =>
        Owner.Monster is GalaxyFriend
        && GalaxyFriend.ShouldHoldCombatOpenForFakeDeath(Owner.CombatState);

    protected override bool CanEnterFakeDeath(Creature creature) =>
        Owner.Monster is GalaxyFriend friend && friend.CanEnterFakeDeath(creature);

    protected override Task EnterFakeDeath(Creature creature) =>
        Owner.Monster is GalaxyFriend friend ? friend.EnterFakeDeath() : Task.CompletedTask;

    public override bool ShouldOwnerDeathTriggerFatal()
    {
        return Owner.Monster is not GalaxyFriend friend
            || GalaxyFriend.AreAllOtherFriendsDeadOrFakeDead(Owner.CombatState, friend);
    }

    public override Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        return side == CombatSide.Player && Owner.Monster is GalaxyFriend friend
            ? friend.TickFakeDeathOnPlayerTurnStart()
            : Task.CompletedTask;
    }

    public override Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, CombatStateLike combatState)
    {
        if (Owner.Monster is GalaxyFriend { IsFakeDead: true } && Owner is LibraryCreature lc && lc.HasChaoResistance && lc.CurrentChaoValue != 0)
        {
            lc.SetCurrentChaoValueInternal(0m);
            lc.HealthBar?.RefreshValues();
        }

        return Task.CompletedTask;
    }
}

public sealed class GalaxyPartingTearsPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "GALAXY_PARTING_TEARS_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Hp", GalaxyFriend.FakeDeathHp)
    ];

    public override Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        return GalaxyFriend.ShouldTriggerPartingTears(combatState)
            ? GalaxyFriend.TriggerPartingTearsVictory(combatState)
            : Task.CompletedTask;
    }

    public override Task AfterDiedToDoom(PlayerChoiceContext choiceContext, IReadOnlyList<Creature> creatures)
    {
        if (Owner.Monster is not GalaxyFriend || !creatures.Any(creature => creature == Owner))
        {
            return Task.CompletedTask;
        }

        return GalaxyFriend.ShouldTriggerPartingTears(Owner.CombatState)
            ? GalaxyFriend.TriggerPartingTearsVictory(Owner.CombatState)
            : Task.CompletedTask;
    }
}
