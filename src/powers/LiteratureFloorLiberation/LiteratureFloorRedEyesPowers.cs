using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.encounters.LiteratureFloorLiberation;
using LibraryOfRuina.interop;
using LibraryOfRuina.monsters.LiteratureFloorLiberation;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.powers.LiteratureFloorLiberation;

public sealed class LiteratureFloorRedEyesStartHuntingPassivePower :
    LiteratureFloorGreenPassivePower
{
    public const int ActionWindow = 2;

    protected override string LegacyPowerId =>
        "LITERATURE_FLOOR_RED_EYES_START_HUNTING_PASSIVE_POWER";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("Actions", ActionWindow)];
}

public sealed class LiteratureFloorRedEyesVigilancePassivePower :
    LiteratureFloorGreenPassivePower
{
    public const int Interval = 2;
    public const int Protection = 4;
    public const int ProtectionTurns = 1;

    protected override string LegacyPowerId =>
        "LITERATURE_FLOOR_RED_EYES_VIGILANCE_PASSIVE_POWER";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Interval", Interval),
        new DynamicVar("Protection", Protection),
        new DynamicVar("Turns", ProtectionTurns)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [HoverTipFactory.FromPower<LibraryProtectionPower>()];

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (side != CombatSide.Enemy
            || Owner.IsDead
            || Owner.Monster is not LiteratureFloorRedEyesBoss boss)
        {
            return;
        }

        if (Amount < Interval)
        {
            SetAmount(Amount + 1, silent: true);
            return;
        }

        SetAmount(1, silent: true);
        Creature[] spiders = combatState.Enemies
            .Where(static enemy =>
                enemy.IsAlive
                && enemy.Monster
                    is LiteratureFloorEnhancedSmallSpider)
            .ToArray();
        if (spiders.Length == 0)
        {
            return;
        }

        Flash();
        boss.PlayVigilanceSfx();
        foreach (Creature spider in spiders)
        {
            await LibraryPowerCmd.Apply<LibraryProtectionPower>(
                spider,
                Protection,
                ProtectionTurns,
                Owner,
                null);
        }
    }
}

public sealed class LiteratureFloorRedEyesHuntingWindowPower :
    LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId =>
        "LITERATURE_FLOOR_RED_EYES_HUNTING_WINDOW_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override bool IsVisibleInternal => false;
}

public sealed class LiteratureFloorLiberationControllerPower :
    LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId =>
        "LITERATURE_FLOOR_LIBERATION_CONTROLLER_POWER";

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
        return combatState.Encounter
            is LiteratureFloorLiberationEncounter encounter
                ? encounter.OnBeforeSideTurnStart(side, combatState)
                : Task.CompletedTask;
    }

    public override bool ShouldStopCombatFromEnding()
    {
        return Owner.CombatState is { } combatState
            && combatState.Encounter
                is LiteratureFloorLiberationEncounter encounter
            && encounter.ShouldKeepCombatOpen(combatState);
    }

    public override bool ShouldDieLate(Creature creature)
    {
        if (Owner.CombatState?.Encounter
            is LiteratureFloorLiberationEncounter encounter)
        {
            if (encounter.ShouldPreventPlayerDeath(creature))
            {
                return false;
            }

            if (encounter.ShouldPreventTransitionBossDeath(creature))
            {
                return false;
            }
        }

        return true;
    }

    public override Task AfterPreventingDeath(Creature creature)
    {
        return Owner.CombatState?.Encounter
            is LiteratureFloorLiberationEncounter encounter
                ? encounter.OnPreventingDeath(creature)
                : Task.CompletedTask;
    }

    public override bool ShouldCreatureBeRemovedFromCombatAfterDeath(
        Creature creature)
    {
        return Owner.CombatState?.Encounter
                   is not LiteratureFloorLiberationEncounter encounter
               || !encounter.ShouldKeepPhaseBossAfterDeath(creature);
    }

    public override bool ShouldAllowHitting(Creature creature)
    {
        return Owner.CombatState?.Encounter
                   is not LiteratureFloorLiberationEncounter encounter
               || !encounter.ShouldSuppressTransitionBossInteraction(
                   creature);
    }

    public override bool ShouldAllowTargeting(Creature target)
    {
        return Owner.CombatState?.Encounter
                   is not LiteratureFloorLiberationEncounter encounter
               || !encounter.ShouldSuppressTransitionBossInteraction(target);
    }
}

public sealed class LiteratureFloorCocoonBindPower :
    LibraryDurationPowerModel
{
    public const int PresenceOffset = 1;
    public const int DamageIncreasePercentPerAttack = 50;
    public const int Duration = 1;

    private sealed class CardsPlayedVar(
        LiteratureFloorCocoonBindPower power) :
        DynamicVar("CardsPlayed", 0m)
    {
        protected override decimal GetBaseValueForIConvertible() =>
            power.CardsPlayed;

        public override string ToString() =>
            GetBaseValueForIConvertible().ToString();
    }

    private sealed class DamageIncreaseVar(
        LiteratureFloorCocoonBindPower power) :
        DynamicVar("Percent", 0m)
    {
        protected override decimal GetBaseValueForIConvertible() =>
            power.CurrentDamageIncreasePercent;

        public override string ToString() =>
            GetBaseValueForIConvertible().ToString();
    }

    protected override string LegacyPowerId =>
        "LITERATURE_FLOOR_COCOON_BIND_POWER";

    public override PowerType Type => PowerType.Debuff;

    public override int DisplayAmount => CardsPlayed;

    public override bool ShowSecondaryDisplayAmount => false;

    public int CardsPlayed => Math.Max(0, Amount - PresenceOffset);

    public int CurrentDamageIncreasePercent =>
        CalculateDamageIncreasePercent(CardsPlayed);

    internal static int CalculateDamageIncreasePercent(int cardsPlayed) =>
        Math.Max(0, cardsPlayed) * DamageIncreasePercentPerAttack;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsPlayedVar(this),
        new DamageIncreaseVar(this),
        new DynamicVar(
            "PercentPerAttack",
            DamageIncreasePercentPerAttack)
    ];

    protected override CombatSide GetDecaySide(Creature owner) =>
        CombatSide.Enemy;

    public static async Task ApplyOrRefresh(
        Creature target,
        Creature applier)
    {
        LiteratureFloorCocoonBindPower? existing =
            target.GetPower<LiteratureFloorCocoonBindPower>();
        if (existing == null)
        {
            await LibraryPowerCmd.Apply<LiteratureFloorCocoonBindPower>(
                target,
                PresenceOffset,
                Duration,
                applier,
                null);
            return;
        }

        await LibraryPowerCmd.Apply<LiteratureFloorCocoonBindPower>(
            target,
            0m,
            Duration,
            applier,
            null);
        existing.Flash();
    }

    public override async Task AfterCardPlayed(
        PlayerChoiceContext context,
        CardPlay cardPlay)
    {
        if (Owner.IsDead
            || cardPlay.Card.Owner.Creature != Owner
            || cardPlay.Card.Type != CardType.Attack)
        {
            return;
        }

        Flash();
        await PowerCmdCompat.ModifyAmount(
            context,
            this,
            1m,
            Owner,
            null,
            silent: true);
    }

    public override Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        return Task.CompletedTask;
    }

    protected override Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants,
        object? _ = null)
    {
        if (side == CombatSide.Enemy
            && SkipNextDurationTick
            && Amount != PresenceOffset)
        {
            SetAmount(PresenceOffset, silent: true);
            InvokeDisplayAmountChanged();
        }

        return Task.CompletedTask;
    }

    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        if (target != Owner
            || dealer?.Side != CombatSide.Enemy
            || !ValuePropCompat.IsPoweredAttack(props)
            || CardsPlayed <= 0)
        {
            return 1m;
        }

        return 1m + CurrentDamageIncreasePercent / 100m;
    }
}
