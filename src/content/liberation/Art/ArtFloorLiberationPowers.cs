using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.liberation.Art;

public sealed class MostBeautifulPerformancePower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "MOST_BEAUTIFUL_PERFORMANCE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Turns", ArtFloorDaCapoBoss.StageTurnCount)
    ];

    public override bool ShouldPowerBeRemovedAfterOwnerDeath() => false;

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (side != CombatSide.Player
            || Owner.IsDead
            || combatState.Encounter is not ArtFloorLiberationEncounter encounter
            || combatState.RoundNumber <= ArtFloorDaCapoBoss.StageTurnCount)
        {
            return;
        }

        Flash();
        await encounter.TriggerPlaceholderPhaseEnd(combatState, fromTurnLimit: true);
    }
}

public sealed class ArtFloorEnsemblePower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "ART_FLOOR_ENSEMBLE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<LibraryStrongPower>("Strength", 1),
        new PowerVar<ArtFloorQuicknessPower>("Quickness", 1),
        new PowerVar<LibraryEndurancePower>("Endurance", 1),
        new PowerVar<LibraryProtectionPower>("Protection", 1)
    ];

    // protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    // [
    //     HoverTipFactory.FromPower<LibraryStrongPower>(),
    //     HoverTipFactory.FromPower<ArtFloorQuicknessPower>(),
    //     HoverTipFactory.FromPower<LibraryEndurancePower>(),
    //     HoverTipFactory.FromPower<LibraryProtectionPower>()
    // ];

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (!TurnParticipants.IsRoundPlayerTurn(side) || Owner.IsDead)
        {
            return;
        }

        int livingAllies = 0;
        foreach (Creature enemy in combatState.Enemies)
        {
            if (enemy.IsAlive)
            {
                livingAllies++;
            }
        }

        if (livingAllies <= 0)
        {
            return;
        }

        Flash();
        await LibraryPowerCmd.Apply<LibraryStrongPower>(
            new ThrowingPlayerChoiceContext(),
            Owner,
            livingAllies,
            0,
            IsPermanent: false,
            Owner,
            null);
        //await LibraryDurationPowerModel.ApplyWithDuration<LibraryQuicknessPower>(Owner, livingAllies, Owner, null);
        await LibraryPowerCmd.Apply<LibraryEndurancePower>(
            new ThrowingPlayerChoiceContext(),
            Owner,
            livingAllies,
            0,
            IsPermanent: false,
            Owner,
            null);
        await LibraryPowerCmd.Apply<LibraryProtectionPower>(
            new ThrowingPlayerChoiceContext(),
            Owner,
            livingAllies,
            0,
            IsPermanent: false,
            Owner,
            null);
    }
}

public sealed class ArtFloorQuicknessPower : LibraryDurationPowerModel
{
    protected override string? LegacyPowerId => "ART_FLOOR_QUICKNESS_POWER";

    public override PowerType Type => PowerType.Buff;
}

public sealed class ArtFloorErosionPower : LibraryOfRuinaPowerModel
{
    public const int PhaseTransitionHp = 0;

    protected override string LegacyPowerId => "ART_FLOOR_EROSION_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("TransitionHp", PhaseTransitionHp)
    ];

    public override bool ShouldPowerBeRemovedAfterOwnerDeath() => false;
}

public sealed class BeyondFragmentTentaclePower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "BEYOND_FRAGMENT_TENTACLE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Percent", BoundaryThornPower.PercentPerStack),
        new DynamicVar("MaxStacks", BoundaryThornPower.MaxStacks)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<BoundaryThornPower>()
    ];

    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        if (dealer != Owner
            || !target.IsPlayer
            || result.UnblockedDamage <= 0
            || !ValuePropCompat.IsPoweredAttack(props))
        {
            return;
        }

        BoundaryThornPower? thorn = target.GetPower<BoundaryThornPower>();
        if ((thorn?.Amount ?? 0) >= BoundaryThornPower.MaxStacks)
        {
            return;
        }

        Flash();
        await PowerCmdCompat.Apply<BoundaryThornPower>(
            choiceContext,
            target,
            1m,
            Owner,
            cardSource);
    }
}

public sealed class BoundaryThornPower : LibraryOfRuinaPowerModel
{
    public const int PercentPerStack = 5;
    public const int MaxStacks = 10;

    protected override string LegacyPowerId => "BOUNDARY_THORN_POWER";

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override int DisplayAmount => Math.Clamp(Amount, 0, MaxStacks) * PercentPerStack;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PercentReductionVar(this),
        new DynamicVar("Percent", PercentPerStack),
        new DynamicVar("MaxPercent", PercentPerStack * MaxStacks)
    ];

    private sealed class PercentReductionVar(BoundaryThornPower power) : DynamicVar("PercentReduction", 0m)
    {
        protected override decimal GetBaseValueForIConvertible()
        {
            return Math.Clamp(power.Amount, 0, MaxStacks) * PercentPerStack;
        }

        public override string ToString()
        {
            return GetBaseValueForIConvertible().ToString();
        }
    }

    public override bool TryModifyPowerAmountReceived(
        PowerModel canonicalPower,
        Creature target,
        decimal amount,
        Creature? applier,
        out decimal modifiedAmount)
    {
        modifiedAmount = amount;
        if (canonicalPower is not BoundaryThornPower || target != Owner || amount <= 0)
        {
            return false;
        }

        modifiedAmount = Math.Max(0m, Math.Min(amount, MaxStacks - Math.Clamp(Amount, 0, MaxStacks)));
        return true;
    }

#if STS2_0_111_0
    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource, CardPlay? cardPlay)
#else
    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
#endif
    {
        if (dealer != Owner || amount <= 0 || !ValuePropCompat.IsPoweredAttack(props))
        {
            return 1m;
        }

        return Math.Max(0m, 1m - Math.Clamp(Amount, 0, MaxStacks) * PercentPerStack / 100m);
    }
}

public sealed class BeyondFragmentIncomprehensiblePower : LibraryOfRuinaPowerModel
{
    private const int TriggerTurns = 3;

    private sealed class Data
    {
        public int NoDamageTurns;
    }

    protected override string LegacyPowerId => "BEYOND_FRAGMENT_INCOMPREHENSIBLE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Counter;

    // Internal data only exists on mutable instances; canonical models show the full count.
    public override int DisplayAmount =>
        IsMutable ? Math.Max(0, TriggerTurns - GetInternalData<Data>().NoDamageTurns) : TriggerTurns;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Turns", TriggerTurns)
    ];

    protected override object InitInternalData() => new Data();

    public override Task AfterAttack(PlayerChoiceContext choiceContext, AttackCommand command)
    {
        if (Owner.IsDead || command.Attacker != Owner)
        {
            return Task.CompletedTask;
        }

        if (AttackCommandCompat.Results(command).Any(static result =>
            result.Receiver.IsPlayer && result.UnblockedDamage > 0))
        {
            GetInternalData<Data>().NoDamageTurns = 0;
            InvokeDisplayAmountChanged();
        }

        return Task.CompletedTask;
    }

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != Owner.Side || Owner.IsDead)
        {
            return;
        }

        Data data = GetInternalData<Data>();
        data.NoDamageTurns++;
        InvokeDisplayAmountChanged();

        if (data.NoDamageTurns < TriggerTurns || Owner.Monster is not ArtFloorBeyondFragmentBoss boss)
        {
            return;
        }

        Flash();
        data.NoDamageTurns = 0;
        InvokeDisplayAmountChanged();
        await boss.QueueBeyondFragmentEgo();
    }
}

public sealed class SilentPerformancePower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "SILENT_PERFORMANCE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;
}

public sealed class FanaticWorshipPower : LibraryOfRuinaPowerModel
{
    private decimal _pendingMaxHpLoss;
    private PlayerChoiceContext? _pendingChoiceContext;
    private bool _convertingMaxHpLoss;

    protected override string LegacyPowerId => "FANATIC_WORSHIP_POWER";

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Amount", 1)
    ];

    public override decimal ModifyHpLostAfterOstyLate(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (_convertingMaxHpLoss
            || amount <= 0
            || target != Owner
            || dealer == null
            || !dealer.IsPlayer
            || dealer == Owner)
        {
            return amount;
        }

        if (dealer.HasPower<FanaticWorshipPower>())
        {
            return amount;
        }

        _pendingMaxHpLoss += amount;
        return 0m;
    }

    public override async Task AfterModifyingHpLostAfterOsty()
    {
        if (_pendingMaxHpLoss <= 0 || _convertingMaxHpLoss)
        {
            return;
        }

        decimal amount = _pendingMaxHpLoss;
        _pendingMaxHpLoss = 0;

        try
        {
            _convertingMaxHpLoss = true;
            await CreatureCmd.LoseMaxHp(
                _pendingChoiceContext ?? new ThrowingPlayerChoiceContext(),
                Owner,
                amount,
                isFromCard: true);
        }
        finally
        {
            _convertingMaxHpLoss = false;
            _pendingChoiceContext = null;
        }
    }

    public override Task BeforeDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (target == Owner && dealer?.IsPlayer == true && !dealer.HasPower<FanaticWorshipPower>())
        {
            _pendingChoiceContext = choiceContext;
        }

        return Task.CompletedTask;
    }
}

public sealed class ArtFloorLiberationControllerPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "ART_FLOOR_LIBERATION_CONTROLLER_POWER";

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
        return combatState.Encounter is ArtFloorLiberationEncounter encounter
            ? encounter.OnBeforeSideTurnStart(side, combatState)
            : Task.CompletedTask;
    }

    public override bool ShouldStopCombatFromEnding()
    {
        return Owner?.CombatState is { } combatState
            && combatState.Encounter is ArtFloorLiberationEncounter encounter
            && encounter.ShouldKeepCombatOpen(combatState);
    }

    public override bool ShouldDieLate(Creature creature)
    {
        if (Owner?.CombatState?.Encounter is ArtFloorLiberationEncounter encounter)
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
        if (Owner?.CombatState?.Encounter is ArtFloorLiberationEncounter encounter)
        {
            return encounter.OnPreventingDeath(creature);
        }

        return Task.CompletedTask;
    }

    public override bool ShouldCreatureBeRemovedFromCombatAfterDeath(Creature creature)
    {
        if (Owner?.CombatState?.Encounter is ArtFloorLiberationEncounter encounter
            && encounter.ShouldKeepPhaseBossAfterDeath(creature))
        {
            return false;
        }

        return true;
    }

    public override bool ShouldAllowHitting(Creature creature)
    {
        if (Owner?.CombatState?.Encounter is ArtFloorLiberationEncounter encounter
            && encounter.ShouldSuppressTransitionBossInteraction(creature))
        {
            return false;
        }

        return true;
    }
}
