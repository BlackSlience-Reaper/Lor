using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.framework.powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.content.abnormalities.DespairKnight;

public sealed class DespairKnightSorrowPower : LibraryOfRuinaPowerModel
{
    private const int IntervalTurns = 2;

    protected override string LegacyPowerId => "DESPAIR_KNIGHT_SORROW_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Turns", IntervalTurns)
    ];
}

public sealed class DespairKnightDespairPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "DESPAIR_KNIGHT_DESPAIR_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("HpLossPercent", DespairKnight.TeardropFalseDeathHpLossPercent)
    ];
}

public sealed class DespairKnightProtectionPower : LibraryOfRuinaPowerModel
{
    private const int RestorePercent = 20;

    protected override string LegacyPowerId => "DESPAIR_KNIGHT_PROTECTION_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("RestorePercent", RestorePercent)
    ];
}

public sealed class DespairKnightBrokenHeartPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "DESPAIR_KNIGHT_BROKEN_HEART_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;
}

public sealed class ForgottenKnightSwordTeardropPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "FORGOTTEN_KNIGHT_SWORD_TEARDROP_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    public override bool ShouldPowerBeRemovedAfterOwnerDeath() => false;

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        if (Owner.Monster is not ForgottenKnightSword sword)
        {
            return Task.CompletedTask;
        }

        sword.MarkTeardropApplied();
        return sword.ApplyTeardropResistances(new ThrowingPlayerChoiceContext());
    }

    public override Task AfterRemoved(Creature oldOwner)
    {
        if (oldOwner.Monster is not ForgottenKnightSword sword)
        {
            return Task.CompletedTask;
        }

        if (sword.IsFakeDead)
        {
            return Task.CompletedTask;
        }

        sword.MarkTeardropRemoved();
        return sword.RestoreNormalResistances(new ThrowingPlayerChoiceContext());
    }
}

public sealed class ForgottenKnightSwordFalseDeathPower : LibraryFakeDeathPowerModel
{
    private const int Turns = ForgottenKnightSword.FalseDeathTurns;
    private const int Hp = ForgottenKnightSword.FalseDeathHp;

    protected override string LegacyPowerId => "FORGOTTEN_KNIGHT_SWORD_FALSE_DEATH_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Turns", Turns),
        new DynamicVar("Hp", Hp)
    ];

    protected override bool IsOwnerFakeDead =>
        Owner.Monster is ForgottenKnightSword { IsFakeDead: true };

    protected override bool CanEnterFakeDeath(Creature creature) =>
        Owner.Monster is ForgottenKnightSword sword && sword.CanEnterFalseDeath(creature);

    protected override Task EnterFakeDeath(Creature creature) =>
        Owner.Monster is ForgottenKnightSword sword ? sword.EnterFalseDeath() : Task.CompletedTask;

    public override Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, CombatStateLike combatState)
    {
        if (Owner.Monster is ForgottenKnightSword { IsFakeDead: true } && Owner is LibraryCreature lc && lc.HasChaoResistance && lc.CurrentChaoValue != 0)
        {
            return LibraryCreatureCmd.SetCurrentChaoValue(lc, 0m);
        }

        return Task.CompletedTask;
    }

}

public sealed class ForgottenKnightSwordPierceDespairPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "FORGOTTEN_KNIGHT_SWORD_PIERCE_DESPAIR_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;
}
