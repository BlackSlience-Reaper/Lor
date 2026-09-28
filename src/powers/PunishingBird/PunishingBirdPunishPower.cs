using System;
using System.Globalization;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Combat.HealthBars;
using Color = Godot.Color;

namespace LibraryOfRuina.powers.PunishingBird;

public sealed class PunishingBirdPunishPower : PunishingBirdBasePower, IHealthBarForecastSource
{
    private const int HpLossPercent = 5;
    private const int CanonicalHpLossThreshold = 30;

    private sealed class HpLossThresholdVar() : DynamicVar("HpLossThreshold", CanonicalHpLossThreshold)
    {
        protected override decimal GetBaseValueForIConvertible()
        {
            if (_owner is not PunishingBirdPunishPower { IsMutable: true } power
                || power.Owner is not { } owner)
            {
                return BaseValue;
            }

            return CalculateHpLossThreshold(owner.MaxHp);
        }

        public override string ToString() =>
            GetBaseValueForIConvertible().ToString(CultureInfo.InvariantCulture);
    }

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Counter;
    
    // Canonical models (compendium, other mods iterating ModelDb) have no Owner; reading it throws.
    public override int DisplayAmount =>
        IsMutable ? Math.Max(0, CalculateHpLossThreshold(Owner.MaxHp) - AccumulatedPlayerHpLoss) : 0;

    protected override string? LegacyPowerId => "PUNISHING_BIRD_PUNISH_POWER";

    protected override string IconFileName => "punishing_bird_punish_power.png";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new HpLossThresholdVar()
    ];

    internal bool PendingPunish { get; private set; }

    internal int AccumulatedPlayerHpLoss { get; private set; }

    public override Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (!Owner.IsDead
            && target == Owner
            && result.UnblockedDamage > 0
            && Owner.CombatState?.CurrentSide == CombatSide.Player
            && dealer is { IsPlayer: true}
            && cardSource != null
            && !PendingPunish)
        {
            AccumulatedPlayerHpLoss = (int)Math.Min(
                int.MaxValue,
                (long)AccumulatedPlayerHpLoss + result.UnblockedDamage);
            PendingPunish = AccumulatedPlayerHpLoss > CalculateHpLossThreshold(Owner.MaxHp);
            InvokeDisplayAmountChanged();
        }

        return Task.CompletedTask;
    }

    internal bool TryConsumePendingPunish()
    {
        if (!PendingPunish)
        {
            return false;
        }

        ClearPunishTrigger();
        return true;
    }

    internal void ClearPunishTrigger()
    {
        PendingPunish = false;
        AccumulatedPlayerHpLoss = 0;
        InvokeDisplayAmountChanged();
    }

    internal static int CalculateHpLossThreshold(int maxHp) =>
        Math.Max(0, (int)Math.Floor(maxHp * HpLossPercent / 100m));

    public IEnumerable<HealthBarForecastSegment> GetHealthBarForecastSegments(HealthBarForecastContext context)
    {
        return HealthBarForecasts.Single(CalculateHpLossThreshold(Owner.MaxHp) - AccumulatedPlayerHpLoss,
            new Color(0.529f, 0.808f, 0.922f), HealthBarForecastGrowthDirection.FromRight);
    }
}
