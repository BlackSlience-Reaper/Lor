using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
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
using STS2RitsuLib.Combat.HealthBars;
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
        new DynamicVar("Reduction", ReligionFloorRules.DamageReductionPercent),
        new DynamicVar("Minimum", ReligionFloorRules.FirstPhaseMinimumHp),
        new DynamicVar("Half", ReligionFloorRules.SecondPhaseMinimumPercent),
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
                return ReligionFloorRules.SecondPhaseMinimumHp(Owner.MaxHp);
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
            amount *= (100m - ReligionFloorRules.DamageReductionPercent) / 100m;
        }
        return Math.Min(amount, Math.Max(0m, Owner.CurrentHp - MinimumHp));
    }

    public override bool ShouldDieLate(Creature creature) => creature != Owner || Owner.Monster is ReligionFloorApostle { IsFakeDead: true }
        || Encounter is not { IsSettling: false, Completed: false };

    public override Task AfterPreventingDeath(Creature creature) => creature == Owner
        ? CreatureCmd.SetCurrentHp(Owner, MinimumHp)
        : Task.CompletedTask;

    public override bool ShouldStopCombatFromEnding() => Owner.Monster is ReligionFloorLostParadise
        && Encounter is { IsSettling: true, Completed: false }
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
            || Encounter is not { IsSettling: true, Completed: false };
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

public sealed class ReligionFloorImmunityPower : ReligionFloorHpFloorPower, IHealthBarForecastSource
{
    protected override bool IsParadise => true;

    public IEnumerable<HealthBarForecastSegment> GetHealthBarForecastSegments(HealthBarForecastContext context)
    {
        if (!IsMutable || Encounter is not { Phase: 2, Completed: false }
            || Owner.IsDead || Owner.CurrentHp <= 0)
        {
            return [];
        }

        // 白色从血条左端覆盖最大生命的一半；到达阈值后覆盖全部剩余生命。
        int whiteHp = Math.Min(Owner.CurrentHp, ReligionFloorRules.SecondPhaseMinimumHp(Owner.MaxHp));
        return HealthBarForecasts.Single(
            whiteHp,
            Colors.White,
            HealthBarForecastGrowthDirection.FromLeft,
            order: 0,
            overlayMaterial: null,
            overlaySelfModulate: null,
            affectsHpLabel: false);
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
        new DynamicVar("Hp", ReligionFloorRules.SurvivalHp)
    ];
}

public sealed class ReligionFloorTrialPower : ReligionFloorGreenPassivePower
{
    public override PowerStackType StackType => PowerStackType.Counter;

    public override int DisplayAmount => Encounter?.RemainingTrialTurns ?? ReligionFloorRules.TrialTurns;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Turns", ReligionFloorRules.TrialTurns),
        new DynamicVar("Half", ReligionFloorRules.SecondPhaseMinimumPercent),
        new DynamicVar("Damage", 0)
    ];

    public override void AddVariablesToDescription(LocString description, int? amountOverride = null)
    {
        UpdateDescriptionVariables();
        description.Add("Damage", Encounter?.ExplosionDamage ?? 0);
        description.Add("Remaining", DisplayAmount);
    }

    internal void RefreshDisplayedState()
    {
        UpdateDescriptionVariables();
        InvokeDisplayAmountChanged();
    }

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        RefreshDisplayedState();
        return base.AfterApplied(applier, cardSource);
    }

    private void UpdateDescriptionVariables()
    {
        if (!IsMutable)
        {
            return;
        }

        // 原版在 AddVariablesToDescription 后注入 DynamicVars，因此必须更新变量本身。
        DynamicVars["Damage"].BaseValue = Encounter?.ExplosionDamage ?? 0;
        DynamicVars["Turns"].BaseValue = DisplayAmount;
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
