using System.Threading.Tasks;
using LibraryOfRuina.framework.powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace LibraryOfRuina.content.abnormalities.SmilingBodies;

public sealed class SmilingBodiesFindCorpsesPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "SMILING_BODIES_FIND_CORPSES_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("HealPercent", 30)
    ];
}

public sealed class SmilingBodiesDissolvingCorpsesPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "SMILING_BODIES_DISSOLVING_CORPSES_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("ThresholdPercent", 25),
        new DynamicVar("MaxCorpses", SmilingBodies.MaxCorpseCount)
    ];

    public override Task AfterCurrentHpChanged(Creature creature, decimal delta)
    {
        if (delta < 0m && creature == Owner && Owner.Monster is SmilingBodies boss)
        {
            boss.QueueCorpseSpawnForThresholds();
        }

        return Task.CompletedTask;
    }

    public override Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (side == CombatSide.Player && Owner.Monster is SmilingBodies boss)
        {
            return boss.ResolvePendingCorpseSpawn(choiceContext, combatState);
        }

        return Task.CompletedTask;
    }
}

public sealed class SmilingBodiesSplitPower : LibraryFakeDeathPowerModel
{
    protected override string LegacyPowerId => "SMILING_BODIES_SPLIT_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override bool IsOwnerFakeDead =>
        Owner.Monster is SmilingBodies { IsFakeDead: true };

    protected override bool CanEnterFakeDeath(Creature creature) =>
        Owner.Monster is SmilingBodies boss && boss.CanEnterFakeDeath(creature);

    protected override Task EnterFakeDeath(Creature creature) =>
        Owner.Monster is SmilingBodies boss ? boss.EnterFakeDeathFromDeath() : Task.CompletedTask;
}

public sealed class SmilingBodiesFusionPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "SMILING_BODIES_FUSION_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    public override Task AfterCurrentHpChanged(Creature creature, decimal delta)
    {
        return creature == Owner && delta > 0m && Owner.Monster is SmilingBodies boss
            ? boss.TryPromoteAtFullHealth()
            : Task.CompletedTask;
    }
}

public sealed class SmilingBodiesSplitAndFusionPower : LibraryFakeDeathPowerModel
{
    protected override string LegacyPowerId => "SMILING_BODIES_SPLIT_AND_FUSION_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override bool IsOwnerFakeDead =>
        Owner.Monster is SmilingBodies { IsFakeDead: true };

    protected override bool CanEnterFakeDeath(Creature creature) =>
        Owner.Monster is SmilingBodies boss && boss.CanEnterFakeDeath(creature);

    protected override Task EnterFakeDeath(Creature creature) =>
        Owner.Monster is SmilingBodies boss ? boss.EnterFakeDeathFromDeath() : Task.CompletedTask;

    public override Task AfterCurrentHpChanged(Creature creature, decimal delta)
    {
        return creature == Owner && delta > 0m && Owner.Monster is SmilingBodies boss
            ? boss.TryPromoteAtFullHealth()
            : Task.CompletedTask;
    }
}

public sealed class SmilingBodiesScreamPower : LibraryOfRuinaPowerModel
{
    private const int StrengthGain = 1;

    protected override string LegacyPowerId => "SMILING_BODIES_SCREAM_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Strength", StrengthGain)
    ];

    public override Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        return side == CombatSide.Enemy && Owner.Monster is SmilingBodies boss && boss.Phase == SmilingBodiesPhase.Second
            ? boss.GainPhaseStrength(choiceContext, StrengthGain)
            : Task.CompletedTask;
    }
}

public sealed class SmilingBodiesVomitPower : LibraryOfRuinaPowerModel
{
    private const int StrengthGain = 2;

    protected override string LegacyPowerId => "SMILING_BODIES_VOMIT_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Strength", StrengthGain)
    ];

    public override Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        return side == CombatSide.Enemy && Owner.Monster is SmilingBodies boss && boss.Phase == SmilingBodiesPhase.Third
            ? boss.GainPhaseStrength(choiceContext, StrengthGain)
            : Task.CompletedTask;
    }
}
