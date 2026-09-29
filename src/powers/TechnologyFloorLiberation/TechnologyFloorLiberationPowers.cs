using System.Threading.Tasks;
using LibraryOfRuina.encounters.TechnologyFloorLiberation;
using LibraryOfRuina.framework.powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace LibraryOfRuina.powers.TechnologyFloorLiberation;

public sealed class TechnologyFloorErosionPower : LibraryOfRuinaPowerModel
{
    public const int PhaseTransitionHp = 0;

    protected override string LegacyPowerId => "TECHNOLOGY_FLOOR_EROSION_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("TransitionHp", PhaseTransitionHp)
    ];

    public override bool ShouldPowerBeRemovedAfterOwnerDeath() => false;
}

public sealed class TechnologyFloorLiberationControllerPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "TECHNOLOGY_FLOOR_LIBERATION_CONTROLLER_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override bool IsVisibleInternal => false;

    public override bool ShouldPowerBeRemovedAfterOwnerDeath() => false;

    public override Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (combatState.Encounter is TechnologyFloorLiberationEncounter encounter)
        {
            return encounter.OnBeforeSideTurnStart(side, combatState);
        }

        return Task.CompletedTask;
    }

    public override bool ShouldDieLate(Creature creature)
    {
        if (Owner?.CombatState?.Encounter is TechnologyFloorLiberationEncounter encounter)
        {
            if (encounter.ShouldPreventPlayerDeath(creature))
                return false;
            if (encounter.ShouldPreventTransitionBossDeath(creature))
                return false;
        }

        return true;
    }

    public override Task AfterPreventingDeath(Creature creature)
    {
        if (Owner?.CombatState?.Encounter is TechnologyFloorLiberationEncounter encounter)
        {
            return encounter.OnPreventingDeath(creature);
        }

        return Task.CompletedTask;
    }

    public override bool ShouldStopCombatFromEnding()
    {
        return Owner?.CombatState is { } combatState
            && combatState.Encounter
                is TechnologyFloorLiberationEncounter encounter
            && encounter.ShouldKeepCombatOpen(combatState);
    }

    public override bool ShouldCreatureBeRemovedFromCombatAfterDeath(Creature creature)
    {
        if (Owner?.CombatState?.Encounter is TechnologyFloorLiberationEncounter encounter
            && encounter.ShouldKeepPhaseBossAfterDeath(creature))
        {
            return false;
        }

        return true;
    }

    public override bool ShouldAllowHitting(Creature creature)
    {
        if (Owner?.CombatState?.Encounter is TechnologyFloorLiberationEncounter encounter
            && encounter.ShouldSuppressTransitionBossInteraction(creature))
        {
            return false;
        }

        return true;
    }
}
