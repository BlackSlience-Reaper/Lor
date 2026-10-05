using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using LibraryOfRuina.framework.powers;

namespace LibraryOfRuina.content.liberation.Religion;

public abstract class ReligionFloorPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => Id.Entry;

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override string RemoteDescriptionLocKey => SmartDescriptionLocKey;

    protected ReligionFloorLiberationEncounter? Encounter => IsMutable ? Owner.CombatState?.Encounter as ReligionFloorLiberationEncounter : null;
}

public abstract class ReligionFloorGreenPassivePower : ReligionFloorPower
{
    public override string PackedIconPath => LibraryOfRuina.framework.assets.SharedAssets.LibraryPassiveGreenIcon;
}

public abstract class ReligionFloorHpFloorPower : ReligionFloorGreenPassivePower
{
    protected abstract bool IsParadise { get; }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Reduction", ReligionFloorRules.FirstPhaseDamageReductionPercent),
        new DynamicVar("Minimum", ReligionFloorRules.FirstPhaseMinimumHp),
        new DynamicVar("Threshold", ReligionFloorRules.FalseDeathThreshold)
    ];

    protected override string SmartDescriptionLocKey => IsParadise
        ? Id.Entry + (Encounter?.Phase == 2 ? ".phase2" : ".phase1")
        : Id.Entry + ".smartDescription";

    public override bool ShouldPowerBeRemovedAfterOwnerDeath() => false;

    private decimal MinimumHp
    {
        get
        {
            if (Owner.Monster is ReligionFloorLostParadise && Encounter?.Phase == 2)
            {
                return ReligionFloorRules.SalvationHp;
            }
            return ReligionFloorRules.FirstPhaseMinimumHp;
        }
    }

    public override decimal ModifyHpLostAfterOstyLate(Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Owner || Encounter is not { IsSettling: false, Completed: false })
        {
            return amount;
        }

        if (Owner.Monster is ReligionFloorLostParadise)
        {
            int reduction = Encounter.Phase == 2
                ? ReligionFloorRules.SecondPhaseDamageReductionPercent
                : ReligionFloorRules.FirstPhaseDamageReductionPercent;
            amount *= (100m - reduction) / 100m;
        }
        return Math.Min(amount, Math.Max(0m, Owner.CurrentHp - MinimumHp));
    }

    public override bool ShouldDieLate(Creature creature) => creature != Owner || Owner.Monster is ReligionFloorApostle { IsFakeDead: true }
        || Owner.Monster is ReligionFloorLostParadise && Encounter?.Phase == 2
        || Encounter is not { IsSettling: false, Completed: false };

    public override Task AfterPreventingDeath(Creature creature) => creature == Owner
        ? CreatureCmd.SetCurrentHp(Owner, MinimumHp)
        : Task.CompletedTask;

    public override bool ShouldStopCombatFromEnding() => Owner.Monster is ReligionFloorLostParadise
        && Encounter is { Completed: false } encounter
        && (encounter.IsSettling || encounter.Outcome == ReligionFloorOutcome.Salvation)
        && Owner.CombatState?.PlayerCreatures.Any(static player => player.IsAlive) == true;

    public override bool ShouldCreatureBeRemovedFromCombatAfterDeath(Creature creature)
    {
        if (creature != Owner)
        {
            return true;
        }
        if (Owner.Monster is ReligionFloorApostle { IsFakeDead: true }
            && Encounter is { IsSettling: false, Completed: false })
        {
            return false;
        }
        return Owner.Monster is not ReligionFloorLostParadise
            || Encounter is not { Completed: false };
    }

    public override bool ShouldAllowHitting(Creature creature) =>
        creature != Owner || Owner.Monster is not ReligionFloorApostle { IsFakeDead: true };

    public override bool ShouldAllowTargeting(Creature target) =>
        target != Owner || Owner.Monster is not ReligionFloorApostle { IsFakeDead: true };

    public override bool ShouldOwnerDeathTriggerFatal() =>
        Owner.Monster is not ReligionFloorApostle { IsFakeDead: true };

    public override bool ShouldPowerBeRemovedOnDeath(PowerModel power) =>
        power.Owner != Owner || Owner.Monster is not ReligionFloorApostle { IsFakeDead: true }
        || power is not MinionPower;

    public override async Task AfterCurrentHpChanged(Creature creature, decimal delta)
    {
        if (creature != Owner || Owner.Monster is ReligionFloorApostle { IsFakeDead: true }
            || Encounter is not { IsSettling: false, Completed: false } encounter)
        {
            return;
        }

        if (Owner.CurrentHp < MinimumHp)
        {
            await CreatureCmd.SetCurrentHp(Owner, MinimumHp);
        }
        if (Owner.Monster is ReligionFloorApostle apostle && Owner.CurrentHp <= ReligionFloorRules.FalseDeathThreshold)
        {
            await apostle.EnterFalseDeath();
        }
        else if (Owner.Monster is ReligionFloorLostParadise)
        {
            encounter.CheckSalvation();
            if (delta < 0)
            {
                await encounter.RecordBossHpLoss(-delta);
            }
        }
    }
}

public sealed class ReligionFloorImmortalityPower : ReligionFloorHpFloorPower
{
    protected override bool IsParadise => false;
}

public sealed class ReligionFloorImmunityPower : ReligionFloorHpFloorPower
{
    protected override bool IsParadise => true;

    public override void AddVariablesToDescription(LocString description, int? amountOverride = null)
    {
        RefreshDisplayedState();
    }

    internal void RefreshDisplayedState()
    {
        if (IsMutable)
        {
            DynamicVars["Reduction"].BaseValue = Encounter?.Phase == 2
                ? ReligionFloorRules.SecondPhaseDamageReductionPercent
                : ReligionFloorRules.FirstPhaseDamageReductionPercent;
        }
    }
}

public sealed class ReligionFloorFalseDeathPower : ReligionFloorPower
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<ReligionFloorImmortalityPower>()];
}

public sealed class ReligionFloorRipeTimePower : ReligionFloorGreenPassivePower
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Kills", ReligionFloorRules.RequiredKills),
        new DynamicVar("Loss", ReligionFloorRules.RipeTimeHpLossPercent)
    ];
}

public sealed class ReligionFloorTrialPower : ReligionFloorGreenPassivePower
{
    public override PowerStackType StackType => PowerStackType.Counter;

    public override int DisplayAmount => Encounter?.RemainingTrialTurns ?? ReligionFloorRules.TrialTurns;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Turns", ReligionFloorRules.TrialTurns),
        new DynamicVar("Minimum", ReligionFloorRules.TrialFailureMinimumHp),
        new DynamicVar("Multiplier", ReligionFloorRules.ExplosionMultiplier)
    ];

    internal void RefreshDisplayedState() => InvokeDisplayAmountChanged();

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        RefreshDisplayedState();
        return base.AfterApplied(applier, cardSource);
    }
}

public sealed partial class ReligionFloorAwePower : ReligionFloorPower
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Heal", ReligionFloorRules.SacrificeHealPercent),
        new DynamicVar("Threshold", ReligionFloorRules.SoloReclaimPercent)
    ];

    public override void AddVariablesToDescription(LocString description, int? amountOverride = null) =>
        description.Add("Threshold", Encounter?.ReclaimPercent ?? ReligionFloorRules.SoloReclaimPercent);

    public override async Task BeforeCardPlayed(CardPlay play)
    {
        if (play.Card.Owner == Owner.Player && play.PlayIndex == 0)
        {
            await PowerCmd.ModifyAmount(new ThrowingPlayerChoiceContext(), this, -1, Owner, play.Card);
        }
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext context, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Player)
        {
            await PowerCmd.Remove(this);
        }
    }
}

public sealed class ReligionFloorCrownPower : ReligionFloorPower
{
    public override PowerType Type => PowerType.Buff;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Protection", ReligionFloorRules.CrownProtection)];

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<LibraryProtectionPower>()];
}
