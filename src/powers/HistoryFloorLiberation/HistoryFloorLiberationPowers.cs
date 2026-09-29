using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.encounters.HistoryFloorLiberation;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.monsters.HistoryFloorLiberation;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.powers.HistoryFloorLiberation;

public sealed class HistoryFloorCorrosionPower : LibraryOfRuinaPowerModel
{
    public const int PhaseTransitionHp = 0;

    protected override string LegacyPowerId => "HISTORY_FLOOR_CORROSION_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("TransitionHp", PhaseTransitionHp)
    ];

    public override bool ShouldPowerBeRemovedAfterOwnerDeath() => false;
}

public sealed class ForgottenAffectionPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "FORGOTTEN_AFFECTION_POWER";

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<StrengthPower>()
    ];

    public static async Task ResetAffectionStacks(IEnumerable<Creature> creatures)
    {
        foreach (Creature creature in creatures)
        {
            ForgottenAffectionPower? affection = creature.GetPower<ForgottenAffectionPower>();
            if (affection != null)
            {
                await PowerCmd.Remove(affection);
            }
        }
    }
}

public sealed class ForgottenAffectionAttackPower : LibraryOfRuinaPowerModel
{
    public const int AffectionPerHit = 1;

    protected override string LegacyPowerId => "FORGOTTEN_AFFECTION_ATTACK_POWER";

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Affection", AffectionPerHit)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<ForgottenAffectionPower>()
    ];
}

public sealed class ForgottenLongingEmbracePower : LibraryOfRuinaPowerModel
{
    private const int TriggerAffection = 5;

    protected override string LegacyPowerId => "FORGOTTEN_LONGING_EMBRACE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Affection", TriggerAffection)
    ];

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != Owner.Side || Owner.Monster is not HistoryFloorForgottenBoss forgotten)
        {
            return;
        }

        if (Owner.CombatState?.PlayerCreatures.Any(static player =>
            player.GetPower<ForgottenAffectionPower>() is { Amount: >= TriggerAffection }) == true)
        {
            Flash();
            await forgotten.QueueLongingEmbraceFromAffection();
        }
    }
}

public sealed class HistoryFloorRekindledSparkPower : LibraryOfRuinaPowerModel
{
    private const int HpLossPercent = 25;
    private const int ChaoLossPercent = 50;

    protected override string LegacyPowerId => "HISTORY_FLOOR_REKINDLED_SPARK_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    public override bool ShouldPowerBeRemovedAfterOwnerDeath() => false;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("HpLossPercent", HpLossPercent),
        new DynamicVar("ChaoLossPercent", ChaoLossPercent)
    ];

    public override async Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        Creature owner = Owner;
        CombatStateLike? combatState = owner.CombatState;
        if (wasRemovalPrevented
            || combatState == null
            || owner.IsDead
            || creature == owner
            || creature.CombatState != combatState
            || creature.Side != owner.Side
            || creature.Monster is not HistoryFloorLastMatch)
        {
            return;
        }

        Flash();
        int hpLoss = Math.Max(1, (int)Math.Ceiling(owner.MaxHp * HpLossPercent / 100m));
        await CreatureCmd.SetCurrentHp(owner, Math.Max(0m, owner.CurrentHp - hpLoss));

        if (owner.IsDead || owner.CombatState != combatState)
        {
            return;
        }

        if (owner is LibraryCreature lc && lc.MaxChaoValue > 0)
        {
            int chaosLoss = Math.Max(1, (int)Math.Ceiling(lc.MaxChaoValue * ChaoLossPercent / 100m));
            await LibraryCreatureCmd.ChaoDamage(
                choiceContext,
                [lc],
                chaosLoss,
                ValueProp.Unblockable | ValueProp.Unpowered,
                owner,
                null,
                null);
        }
    }
}

public sealed class HistoryFloorLiberationControllerPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "HISTORY_FLOOR_LIBERATION_CONTROLLER_POWER";

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
        if (combatState.Encounter is HistoryFloorLiberationEncounter encounter)
        {
            return encounter.OnBeforeSideTurnStart(side, combatState);
        }

        return Task.CompletedTask;
    }

    public override bool ShouldDieLate(Creature creature)
    {
        if (Owner?.CombatState?.Encounter is HistoryFloorLiberationEncounter encounter)
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
        if (Owner?.CombatState?.Encounter is HistoryFloorLiberationEncounter encounter)
        {
            return encounter.OnPreventingPlayerDeath(creature);
        }

        return Task.CompletedTask;
    }

    public override bool ShouldStopCombatFromEnding()
    {
        return Owner?.CombatState is { } combatState
            && combatState.Encounter
                is HistoryFloorLiberationEncounter encounter
            && encounter.ShouldKeepCombatOpen(combatState);
    }

    public override bool ShouldCreatureBeRemovedFromCombatAfterDeath(Creature creature)
    {
        if (Owner?.CombatState?.Encounter is HistoryFloorLiberationEncounter encounter
            && encounter.ShouldKeepPhaseBossAfterDeath(creature))
        {
            return false;
        }

        return true;
    }

    public override bool ShouldAllowHitting(Creature creature)
    {
        if (Owner?.CombatState?.Encounter is HistoryFloorLiberationEncounter encounter
            && encounter.ShouldSuppressTransitionBossInteraction(creature))
        {
            return false;
        }

        return true;
    }
}
