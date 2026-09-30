using System.Threading.Tasks;
using LibraryOfRuina.framework.powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.content.liberation.Natural;

public sealed class NaturalFloorTearUntargetablePower : NaturalFloorGreenPassivePower
{
    // Retain this model ID for saves; targeting is owned by UntargetablePower.
    protected override string LegacyPowerId => "NATURAL_FLOOR_TEAR_UNTARGETABLE_POWER";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Interval", NaturalFloorTearEdgeBoss.TeardropInterval),
        new DynamicVar("Plating", NaturalFloorTearEdgeBoss.PlatingAmount)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        //HoverTipFactory.FromPower<NaturalFloorTeardropPower>(),
        HoverTipFactory.FromPower<MegaCrit.Sts2.Core.Models.Powers.PlatingPower>()
    ];

    public override bool ShouldPowerBeRemovedAfterOwnerDeath() => false;
}

public sealed class NaturalFloorDespairPower : NaturalFloorGreenPassivePower
{
    protected override string LegacyPowerId => "NATURAL_FLOOR_DESPAIR_POWER";

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Turns", 1)];

    //protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<NaturalFloorTeardropPower>()];
}

public sealed class NaturalFloorDespairProtectionPower : NaturalFloorGreenPassivePower
{
    protected override string LegacyPowerId => "NATURAL_FLOOR_DESPAIR_PROTECTION_POWER";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Swords", 3),
        new DynamicVar("Recovery", 100)
    ];
}

public sealed class NaturalFloorBrokenHeartPower : NaturalFloorGreenPassivePower
{
    protected override string LegacyPowerId => "NATURAL_FLOOR_BROKEN_HEART_POWER";

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Swords", 1)];
}

public sealed class NaturalFloorTeardropPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "NATURAL_FLOOR_TEARDROP_POWER";

    public override string PackedIconPath => NaturalFloorAssets.ForgottenKnightSwordTeardropPowerIcon;

    public override string ResolvedBigIconPath => PackedIconPath;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override bool ShouldPowerBeRemovedAfterOwnerDeath() => false;

    public override Task AfterApplied(Creature? applier, CardModel? cardSource) =>
        Owner.Monster is NaturalFloorForgottenSword sword ? sword.SetTeardrop(true) : Task.CompletedTask;

    public override Task AfterRemoved(Creature oldOwner) =>
        oldOwner.Monster is NaturalFloorForgottenSword sword ? sword.SetTeardrop(false) : Task.CompletedTask;
}

public sealed class NaturalFloorSwordFalseDeathPower : LibraryFakeDeathPowerModel
{
    protected override string LegacyPowerId => "NATURAL_FLOOR_SWORD_FALSE_DEATH_POWER";

    public override string PackedIconPath => NaturalFloorAssets.LibraryPassiveGreenIcon;

    public override string ResolvedBigIconPath => PackedIconPath;

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override bool HoldCombatOpenWhileFakeDead =>
        Owner.CombatState?.Encounter is NaturalFloorLiberationEncounter
        {
            SettlementTriggered: false
        };

    protected override bool IsOwnerFakeDead => Owner.Monster is NaturalFloorForgottenSword { IsFakeDead: true };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Turns", NaturalFloorForgottenSword.FalseDeathTurns),
        new DynamicVar("Recovery", NaturalFloorForgottenSword.RecoveryPercent)
    ];

    protected override bool CanEnterFakeDeath(Creature creature) =>
        creature == Owner
        && Owner.Monster is NaturalFloorForgottenSword { IsFakeDead: false }
        && Owner.CombatState?.Encounter is NaturalFloorLiberationEncounter
        {
            SettlementTriggered: false,
            TransitionPending: false
        };

    protected override Task EnterFakeDeath(Creature creature) =>
        Owner.Monster is NaturalFloorForgottenSword sword ? sword.EnterFalseDeath() : Task.CompletedTask;

    public override Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState) =>
        Owner.Monster is NaturalFloorForgottenSword { IsFakeDead: true } sword
            ? sword.TickFalseDeath(combatState.RoundNumber)
            : Task.CompletedTask;
}

public sealed class NaturalFloorPierceDespairPower : NaturalFloorGreenPassivePower
{
    protected override string LegacyPowerId => "NATURAL_FLOOR_PIERCE_DESPAIR_POWER";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Pierce", NaturalFloorForgottenSword.PierceStabPercent),
        new DynamicVar("Slash", NaturalFloorForgottenSword.SlashStabPercent),
        new DynamicVar("Blunt", NaturalFloorForgottenSword.BluntStabPercent)
    ];
}
