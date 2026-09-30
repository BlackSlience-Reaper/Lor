using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Combat.HealthBars;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Content.Patches;
using STS2RitsuLib.Utils;
using EscapedBirdMonster = LibraryOfRuina.content.abnormalities.JudgementBird.EscapedBird;
using JudgementBirdMonster = LibraryOfRuina.content.abnormalities.JudgementBird.JudgementBird;

namespace LibraryOfRuina.content.abnormalities.JudgementBird;

public abstract class JudgementBirdPassivePower :
    LibraryOfRuinaPowerModel,
    IModPowerAssetOverrides
{
    internal const string GreenPassiveIconPath =
        "res://images/powers/library_passive_green.png";

    public override string PackedIconPath => GreenPassiveIconPath;

    public override string ResolvedBigIconPath => PackedIconPath;

    public PowerAssetProfile AssetProfile { get; } = new(
        GreenPassiveIconPath,
        GreenPassiveIconPath);

    public string? CustomIconPath => GreenPassiveIconPath;

    public string? CustomBigIconPath => GreenPassiveIconPath;
}

internal sealed class JudgementBirdDeadlyAscensionVar : DynamicVar
{
    private readonly int _lowValue;
    private readonly int _highValue;

    internal JudgementBirdDeadlyAscensionVar(
        string name,
        int lowValue,
        int highValue)
        : base(name, lowValue)
    {
        _lowValue = lowValue;
        _highValue = highValue;
    }

    protected override decimal GetBaseValueForIConvertible()
    {
        if (_owner is not PowerModel { IsMutable: true } power
            || power.Owner?.CombatState == null)
        {
            return _lowValue;
        }

        return power.Owner.CombatState.RunState.AscensionLevel
            >= (int)AscensionLevel.DeadlyEnemies
                ? _highValue
                : _lowValue;
    }

    public override string ToString() =>
        GetBaseValueForIConvertible().ToString(CultureInfo.InvariantCulture);
}

public sealed class JudgementBirdSinPower :
    LibraryOfRuinaPowerModel,
    IHealthBarForecastSource,
    IHealthBarVisualGraftSource
{
    private sealed class MaxHpVar()
        : DynamicVar("MaxHp", JudgementBirdMonster.HighMaxHp)
    {
        protected override decimal GetBaseValueForIConvertible()
        {
            return _owner is JudgementBirdSinPower { IsMutable: true } power
                && power.Owner is { } owner
                    ? owner.MaxHp
                    : BaseValue;
        }

        public override string ToString() =>
            GetBaseValueForIConvertible().ToString(CultureInfo.InvariantCulture);
    }

    private static readonly Color ForecastBrown =
        new(0.42f, 0.24f, 0.10f);

    internal const HealthBarForecastGrowthDirection ForecastDirection =
        HealthBarForecastGrowthDirection.FromLeft;

    private static readonly Lazy<ShaderMaterial> ForecastMaterial =
        new(CreateForecastMaterial);

    internal static Color ForecastOverlayColor => ForecastBrown;

    internal static Material ForecastOverlayMaterial =>
        ForecastMaterial.Value;

    protected override string LegacyPowerId =>
        "JUDGEMENT_BIRD_SIN_POWER";

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override bool AllowNegative => false;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new MaxHpVar()
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
            || target == Owner
            || target.IsDead
            || result.UnblockedDamage <= 0
            || !ValuePropCompat.IsPoweredAttack(props))
        {
            return;
        }

        await JudgementBirdSinService.Transfer(
            choiceContext,
            Owner,
            target,
            1,
            Owner,
            cardSource);
    }

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (Owner.IsDead
            || !TurnParticipants.IsOwnTurn(Owner, side, participants)
            || Amount < Owner.MaxHp)
        {
            return;
        }

        Flash();
        await CreatureCmd.Kill(Owner, force: true);
    }

    public override async Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (!ReferenceEquals(power, this))
        {
            return;
        }

        JudgementBirdLockedSinMarkerPower? marker =
            Owner.GetPower<JudgementBirdLockedSinMarkerPower>();
        if (marker != null && marker.Amount > Amount)
        {
            await PowerCmdCompat.ModifyAmount(
                choiceContext,
                marker,
                Amount - marker.Amount,
                applier,
                cardSource,
                silent: true);
        }

    }

    public override async Task AfterRemoved(Creature oldOwner)
    {
        if (oldOwner.GetPower<JudgementBirdLockedSinMarkerPower>() is { } marker)
        {
            await PowerCmd.Remove(marker);
        }
    }

    public IEnumerable<HealthBarForecastSegment>
        GetHealthBarForecastSegments(HealthBarForecastContext context)
    {
        if (!IsMutable || Owner.IsDead || Amount <= 0)
        {
            return [];
        }

        return HealthBarForecasts.Single(
            GetForecastAmount(),
            ForecastBrown,
            ForecastDirection,
            HealthBarForecastOrder.ForSideTurnEnd(Owner, Owner.Side),
            ForecastMaterial.Value,
            Colors.White,
            affectsHpLabel: Amount >= Owner.MaxHp);
    }

    public HealthBarVisualGraftMetrics GetHealthBarVisualGraft(
        HealthBarVisualGraftContext context)
    {
        int amountPastCurrentHp = Math.Max(
            0,
            GetForecastAmount() - Owner.CurrentHp);
        return new HealthBarVisualGraftMetrics(
            amountPastCurrentHp,
            Colors.Transparent,
            null);
    }

    internal int GetForecastAmount() =>
        Math.Min(Amount, Owner.MaxHp);

    private static ShaderMaterial CreateForecastMaterial()
    {
        var gradient = new Gradient();
        gradient.SetColor(0, ForecastBrown);
        gradient.SetColor(1, Colors.White);
        gradient.AddPoint(0.25f, Colors.White);
        gradient.AddPoint(0.50f, ForecastBrown);
        gradient.AddPoint(0.75f, Colors.White);
        return MaterialUtils.CreateDoomBarShaderMaterial(
            new GradientTexture1D { Gradient = gradient });
    }
}

public sealed class JudgementBirdLockedSinMarkerPower :
    LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId =>
        "JUDGEMENT_BIRD_LOCKED_SIN_MARKER_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override bool AllowNegative => false;

    protected override bool IsVisibleInternal => false;

}

internal static class JudgementBirdSinService
{
    internal static bool IsEncounterTransferableSinTarget(
        Creature creature) =>
        creature.IsPlayer
        || creature.Side == CombatSide.Enemy
        && creature.Monster is EscapedBirdMonster;

    internal static int GetTotal(Creature? creature) =>
        creature?.GetPower<JudgementBirdSinPower>()?.Amount ?? 0;

    internal static int GetLocked(Creature? creature) =>
        creature?.GetPower<JudgementBirdLockedSinMarkerPower>()?.Amount ?? 0;

    internal static int GetTransferable(Creature? creature) =>
        Math.Max(0, GetTotal(creature) - GetLocked(creature));

    internal static async Task ApplyTransferable(
        PlayerChoiceContext choiceContext,
        Creature target,
        int amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (target.IsDead
            || !IsEncounterTransferableSinTarget(target)
            || amount <= 0)
        {
            return;
        }

        await PowerCmdCompat.Apply<JudgementBirdSinPower>(
            choiceContext,
            target,
            amount,
            applier,
            cardSource);
    }

    internal static async Task ApplyLocked(
        PlayerChoiceContext choiceContext,
        Creature target,
        int amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (target.IsDead || amount <= 0)
        {
            return;
        }

        await PowerCmdCompat.Apply<JudgementBirdSinPower>(
            choiceContext,
            target,
            amount,
            applier,
            cardSource);
        await PowerCmdCompat.Apply<JudgementBirdLockedSinMarkerPower>(
            choiceContext,
            target,
            amount,
            applier,
            cardSource,
            silent: true);
    }

    internal static async Task<int> Transfer(
        PlayerChoiceContext choiceContext,
        Creature from,
        Creature to,
        int requested,
        Creature? applier,
        CardModel? cardSource)
    {
        if (from == to
            || from.IsDead
            || to.IsDead
            || !IsEncounterTransferableSinTarget(from)
            || !IsEncounterTransferableSinTarget(to)
            || to.Monster is JudgementBirdMonster
            || requested <= 0
            || from.GetPower<JudgementBirdSinPower>() is not { } fromPower)
        {
            return 0;
        }

        int transfer = Math.Min(requested, GetTransferable(from));
        if (transfer <= 0)
        {
            return 0;
        }

        await PowerCmdCompat.ModifyAmount(
            choiceContext,
            fromPower,
            -transfer,
            applier,
            cardSource,
            silent: true);
        await ApplyTransferable(
            choiceContext,
            to,
            transfer,
            applier,
            cardSource);
        return transfer;
    }

    internal static async Task Double(
        PlayerChoiceContext choiceContext,
        Creature target,
        Creature? applier)
    {
        int total = GetTotal(target);
        if (total <= 0 || target.IsDead)
        {
            return;
        }

        await PowerCmdCompat.Apply<JudgementBirdSinPower>(
            choiceContext,
            target,
            total,
            applier,
            null);
    }

    internal static async Task HalveWithFloorLoss(
        PlayerChoiceContext choiceContext,
        Creature target,
        Creature? applier)
    {
        int total = GetTotal(target);
        int loss = total / 2;
        if (loss <= 0)
        {
            return;
        }

        int remaining = total - loss;
        int remainingLocked = Math.Min(GetLocked(target), remaining);
        await SetAmount<JudgementBirdSinPower>(
            choiceContext,
            target,
            remaining,
            applier);
        await SetAmount<JudgementBirdLockedSinMarkerPower>(
            choiceContext,
            target,
            remainingLocked,
            applier);
    }

    private static async Task SetAmount<TPower>(
        PlayerChoiceContext choiceContext,
        Creature target,
        int amount,
        Creature? applier)
        where TPower : PowerModel
    {
        if (target.GetPower<TPower>() is not { } existing)
        {
            if (amount > 0)
            {
                await PowerCmdCompat.Apply<TPower>(
                    choiceContext,
                    target,
                    amount,
                    applier,
                    null,
                    silent: true);
            }

            return;
        }

        if (amount <= 0)
        {
            await PowerCmd.Remove(existing);
            return;
        }

        await PowerCmdCompat.ModifyAmount(
            choiceContext,
            existing,
            amount - existing.Amount,
            applier,
            null,
            silent: true);
    }
}

public sealed class JudgementBirdUnjustScalePower :
    JudgementBirdPassivePower
{
    protected override string LegacyPowerId =>
        "JUDGEMENT_BIRD_UNJUST_SCALE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new JudgementBirdDeadlyAscensionVar(
            "Sin",
            JudgementBirdMonster.UnjustScaleLowSin,
            JudgementBirdMonster.UnjustScaleHighSin)
    ];

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (Owner.IsDead || !TurnParticipants.IsRoundPlayerTurn(side))
        {
            return;
        }

        int amount = JudgementBirdMonster.UnjustScaleSin;
        foreach (Creature target in combatState.Creatures
            .Where(target =>
                target.IsAlive
                && target != Owner
                && JudgementBirdSinService
                    .IsEncounterTransferableSinTarget(target))
            .OrderBy(static target => target.CombatId ?? uint.MaxValue))
        {
            await JudgementBirdSinService.ApplyTransferable(
                choiceContext,
                target,
                amount,
                Owner,
                null);
        }
    }
}

public sealed class JudgementBirdWeightOfSinPower :
    JudgementBirdPassivePower
{
    protected override string LegacyPowerId =>
        "JUDGEMENT_BIRD_WEIGHT_OF_SIN_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("PercentPerSin", 1)
    ];

    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        if (dealer != Owner
            || target == null
            || !ValuePropCompat.IsPoweredAttack(props))
        {
            return 1m;
        }

        return 1m + JudgementBirdSinService.GetTotal(target) / 100m;
    }
}

public sealed class JudgementBirdJudgementPower :
    JudgementBirdPassivePower
{
    protected override string LegacyPowerId =>
        "JUDGEMENT_BIRD_JUDGEMENT_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    public override Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        return TurnParticipants.IsRoundPlayerTurn(side)
            && Owner.Monster is JudgementBirdMonster bird
                ? bird.QueueJudgementIfRequired()
                : Task.CompletedTask;
    }
}

public sealed class JudgementBirdFullOfEvilPower :
    JudgementBirdPassivePower
{
    protected override string LegacyPowerId =>
        "JUDGEMENT_BIRD_FULL_OF_EVIL_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("ChaoPercent", 100)
    ];

    public override Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        return side == CombatSide.Player
            && Owner.Monster is JudgementBirdMonster bird
                ? bird.ResolveFullOfEvilAtPlayerTurnStart()
                : Task.CompletedTask;
    }
}

public sealed class EscapedBirdScaryPower :
    JudgementBirdPassivePower
{
    protected override string LegacyPowerId =>
        "ESCAPED_BIRD_SCARY_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new JudgementBirdDeadlyAscensionVar(
            "ExtraSin",
            EscapedBirdMonster.ExtraTransferLow,
            EscapedBirdMonster.ExtraTransferHigh)
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
            || target == Owner
            || target.IsDead
            || result.UnblockedDamage <= 0
            || !ValuePropCompat.IsPoweredAttack(props))
        {
            return;
        }

        await JudgementBirdSinService.Transfer(
            choiceContext,
            Owner,
            target,
            EscapedBirdMonster.ExtraTransfer,
            Owner,
            cardSource);
    }
}
