using System;
using System.Globalization;
using System.Threading.Tasks;
using LibraryLib.Combat.HealthBars;
using LibraryOfRuina.compat;
using LibraryOfRuina.interop;
using LibraryOfRuina.patches;
using LibraryOfRuina.powers;
using LibraryOfRuina.ui;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.specialguests.Rnfmabj;

/// <summary>
/// Legacy save carrier only. New rnfmabj plans never apply this power and the
/// boss removes deserialized instances as soon as it enters the room.
/// </summary>
public sealed class RnfmabjCounterEvadePower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "RNFMABJ_COUNTER_EVADE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override bool IsVisibleInternal => false;

    public override string PackedIconPath =>
        ImageHelper.GetImagePath("powers/rnfmabj_counter_evade_power.png");

    public override string ResolvedBigIconPath => PackedIconPath;
}

public sealed class RnfmabjCorrosionPower :
    LibraryOfRuinaPowerModel,
    ILibraryHealthBarDamageForecastSource,
    ICorrosionFollowUpPower
{
    protected override string LegacyPowerId => "RNFMABJ_CORROSION_POWER";

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public IEnumerable<LibraryHealthBarDamageForecast>
        GetLibraryHealthBarDamageForecasts(
            LibraryHealthBarForecastContext context) =>
        [
            new(
                Amount,
                LibraryOfRuina.ui.LibraryHealthBarForecastColors.RnfmabjCorrosion,
                CorrosionFollowUpRules.DamageProps,
                Order: 10,
                Dealer: GetTurnEndDamageDealer())
        ];

    public Creature? GetTurnEndDamageDealer() => Applier ?? Owner;

    public Creature? GetHitFollowUpDealer(Creature? attacker) => Applier ?? attacker ?? Owner;

    public IDisposable EnterDamageSourceScope() => CorrosionFollowUpRules.NoSourceScope;

    public override string PackedIconPath =>
        ImageHelper.GetImagePath("powers/rnfmabj_corrosion_power.png");

    public override string ResolvedBigIconPath => PackedIconPath;

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        _ = participants;
        if (Owner.IsDead || side != Owner.Side || Amount <= 0)
        {
            return;
        }

        Flash();
        await CreatureCmdCompat.Damage(
            choiceContext,
            Owner,
            Amount,
            CorrosionFollowUpRules.DamageProps,
            GetTurnEndDamageDealer(),
            null);
        await PowerCmd.Decrement(this);
    }

    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        _ = cardSource;
        if (target != Owner
            || Owner.IsDead
            || !CorrosionFollowUpRules.TriggersOnHit(Amount, result.TotalDamage, props))
        {
            return;
        }

        Flash();
        await CreatureCmdCompat.Damage(
            choiceContext,
            Owner,
            Amount,
            CorrosionFollowUpRules.DamageProps,
            GetHitFollowUpDealer(dealer),
            null);
    }
}

public sealed class RnfmabjMechanicsPower : LibraryOfRuinaPowerModel
{
    private const int PreUnionDamageReductionPercent = 80;
    private const decimal PreUnionDamageMultiplier = 0.2m;

    protected override string LegacyPowerId => "RNFMABJ_MECHANICS_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new RnfmabjScaledHpThresholdVar(
            "UnionBodyThreshold",
            Rnfmabj.BodyPhaseThreshold,
            afterScaleOffset: 1),
        new RnfmabjScaledHpThresholdVar(
            "HandThreshold",
            Rnfmabj.HandDisabledThreshold),
        new RnfmabjScaledHpThresholdVar(
            "BodyThreshold",
            Rnfmabj.BodyPhaseThreshold),
        new DynamicVar("DamageReduction", PreUnionDamageReductionPercent),
    ];

    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        _ = props;
        _ = dealer;
        _ = cardSource;
        _ = cardPlay;

        return target == Owner
            && amount > 0m
            && Owner.Monster is Rnfmabj { Phase: 1, IsUnited: false }
                ? PreUnionDamageMultiplier
                : 1m;
    }

    public override string PackedIconPath =>
        ImageHelper.GetImagePath("powers/rnfmabj_mechanics_power.png");

    public override string ResolvedBigIconPath => PackedIconPath;
}

public sealed class RnfmabjTwistedBladePassivePower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "RNFMABJ_TWISTED_BLADE_PASSIVE_POWER";

    public override PowerType Type => PowerType.None;

    // Counter stack type enables NPower's amount label so the blade cooldown
    // (DisplayAmount) renders as a counter on the power icon.
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new RnfmabjScaledHpThresholdVar(
            "BodyThreshold",
            Rnfmabj.BodyPhaseThreshold),
        new DynamicVar("CooldownLimit", Rnfmabj.BladeCooldownClamp),
    ];

    public override int DisplayAmount =>
        IsMutable && Owner?.Monster is Rnfmabj boss ? Math.Max(0, boss.BladeCooldown) : 0;

    public override string PackedIconPath =>
        ImageHelper.GetImagePath("powers/rnfmabj_twisted_blade_passive_power.png");

    public override string ResolvedBigIconPath => PackedIconPath;

    internal void RefreshDisplay() => InvokeDisplayAmountChanged();
}

public sealed class RnfmabjHandMechanicsPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "RNFMABJ_HAND_MECHANICS_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("SurvivalHp", RnfmabjHandBase.SurvivalHp),
        new RnfmabjScaledHpThresholdVar(
            "HandThreshold",
            Rnfmabj.HandDisabledThreshold),
    ];

    public override string PackedIconPath =>
        ImageHelper.GetImagePath("powers/rnfmabj_hand_mechanics_power.png");

    public override string ResolvedBigIconPath => PackedIconPath;
}

internal sealed class RnfmabjScaledHpThresholdVar : DynamicVar
{
    private readonly int _baseThreshold;
    private readonly int _afterScaleOffset;

    public RnfmabjScaledHpThresholdVar(
        string name,
        int baseThreshold,
        int afterScaleOffset = 0)
        : base(name, baseThreshold + afterScaleOffset)
    {
        _baseThreshold = baseThreshold;
        _afterScaleOffset = afterScaleOffset;
    }

    protected override decimal GetBaseValueForIConvertible()
    {
        if (_owner is not PowerModel { IsMutable: true } power
            || power.Owner is not { Monster: not null } owner)
        {
            return BaseValue;
        }

        return Math.Ceiling(
                   MultiplayerScalingPatchHelper.ScaleHpAmount(
                       owner.CombatState,
                       owner.Monster,
                       _baseThreshold))
               + _afterScaleOffset;
    }

    public override string ToString() =>
        GetBaseValueForIConvertible().ToString(CultureInfo.InvariantCulture);
}

internal static class RnfmabjPassiveInstaller
{
    public static async Task EnsureFor(RnfmabjMonsterBase monster)
    {
        Creature owner = monster.Creature;
        if (monster is Rnfmabj)
        {
            if (owner.GetPower<RnfmabjMechanicsPower>() == null)
            {
                await PowerCmdCompat.Apply<RnfmabjMechanicsPower>(
                    owner,
                    1,
                    owner,
                    null,
                    silent: true);
            }

            if (owner.GetPower<RnfmabjTwistedBladePassivePower>() == null)
            {
                await PowerCmdCompat.Apply<RnfmabjTwistedBladePassivePower>(
                    owner,
                    1,
                    owner,
                    null,
                    silent: true);
            }
        }
        else if (owner.GetPower<RnfmabjHandMechanicsPower>() == null)
        {
            await PowerCmdCompat.Apply<RnfmabjHandMechanicsPower>(
                owner,
                1,
                owner,
                null,
                silent: true);
        }
    }
}
