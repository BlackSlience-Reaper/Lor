using System.Threading.Tasks;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.powers;





public abstract class LibraryOfRuinaPowerModel : LibraryPowerModel
{
    protected abstract override string? LegacyPowerId { get; }

    protected bool OwnerCanHoldCombatOpenForLockHp => Owner.IsPrimaryEnemy;

    public override LocString Title => new("powers", $"{LegacyPowerId}.title");

    public override LocString Description => new("powers", $"{LegacyPowerId}.description");

    protected override string SmartDescriptionLocKey => $"{LegacyPowerId}.smartDescription";

    protected override string RemoteDescriptionLocKey => $"{LegacyPowerId}.remoteDescription";
}

public abstract class LibraryFakeDeathPowerModel : LibraryOfRuinaPowerModel
{
    protected abstract bool IsOwnerFakeDead { get; }

    internal bool IsHealthBarLockActive => IsOwnerFakeDead;

    protected abstract bool CanEnterFakeDeath(Creature creature);

    protected abstract Task EnterFakeDeath(Creature creature);

    protected virtual bool HoldCombatOpenWhileFakeDead => OwnerCanHoldCombatOpenForLockHp;

    protected virtual bool SuppressInteractionWhileFakeDead => true;

    public override bool ShouldPowerBeRemovedAfterOwnerDeath() => false;

    public override async Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (wasRemovalPrevented || creature != Owner || IsOwnerFakeDead || !CanEnterFakeDeath(creature))
        {
            return;
        }

        await EnterFakeDeath(creature);
    }

    public override bool ShouldStopCombatFromEnding() =>
        HoldCombatOpenWhileFakeDead && IsOwnerFakeDead;

    public override bool ShouldAllowHitting(Creature creature) =>
        creature != Owner || !SuppressInteractionWhileFakeDead || !IsOwnerFakeDead;

    public override bool ShouldAllowTargeting(Creature target) =>
        target != Owner || !SuppressInteractionWhileFakeDead || !IsOwnerFakeDead;

    public override bool ShouldCreatureBeRemovedFromCombatAfterDeath(Creature creature)
    {
        if (creature != Owner)
        {
            return true;
        }

        return !IsOwnerFakeDead && !CanEnterFakeDeath(creature);
    }

    public override bool ShouldPowerBeRemovedOnDeath(PowerModel power) =>
        power.Owner != Owner || !IsOwnerFakeDead;
}
