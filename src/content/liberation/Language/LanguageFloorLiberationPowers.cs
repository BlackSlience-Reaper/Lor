using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.infra.lifecycle;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.liberation.Language;

public sealed class LanguageFloorHuntMarkPower : LibraryDurationPowerModel
{
    private const decimal ScarletDamageMultiplier = 1.5m;
    public const int DefaultTurns = 2;

    protected override string LegacyPowerId => "LANGUAGE_FLOOR_HUNT_MARK_POWER";

    public override PowerType Type => PowerType.None;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new DynamicVar("Multiplier", (ScarletDamageMultiplier - 1) * 100),
            new DynamicVar("Turns", DefaultTurns)
        ];

    protected override bool ShouldSkipInitialDurationTick(
        CombatSide applicationSide,
        CombatSide decaySide,
        Creature target)
    {
        return applicationSide == decaySide;
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
            || Applier == null
            || dealer != Applier
            || !ValuePropCompat.IsPoweredAttack(props)
            || Amount <= 0)
        {
            return 1m;
        }

        return ScarletDamageMultiplier;
    }
}

public sealed class LanguageFloorScarPower : LibraryOfRuinaPowerModel
{
    public const int DamagePerStack = 1;
    public const int ExtraDamageThreshold = 4;
    public const int ExtraDamageAtFourStacks = 2;
    public const int BleedPerStackAtTurnStart = 1;
    public const int BurstThreshold = 6;
    public const int BurstHpLossPercent = 20;
    public const int BurstRemainingPercent = 80;

    protected override string LegacyPowerId => "LANGUAGE_FLOOR_SCAR_POWER";

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [HoverTipFactory.FromPower<LibraryBleedingPower>()];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new DynamicVar("ExtraDamageAtFour", ExtraDamageAtFourStacks),
            new DynamicVar("ExtraDamageThreshold", ExtraDamageThreshold),
            new DynamicVar("DamagePerStack", DamagePerStack),
            new DynamicVar("BleedPerStack", BleedPerStackAtTurnStart),
            new DynamicVar("BurstThreshold", BurstThreshold),
            new DynamicVar("BurstHpLossPercent", BurstHpLossPercent),
            new DynamicVar("BurstRemainingPercent", BurstRemainingPercent)
        ];

    public override decimal ModifyDamageAdditive(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        if (target != Owner
            || dealer == null
            || !LanguageFloorCobaltWolfPowerRules.IsScarDamageDealer(dealer)
            || !ValuePropCompat.IsPoweredAttack(props)
            || Amount <= 0)
        {
            return 0m;
        }

        int bonus = Amount * DamagePerStack
            + (Amount >= ExtraDamageThreshold ? ExtraDamageAtFourStacks : 0);
        return bonus;
    }

    public override async Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        await base.AfterSideTurnStart(side, participants, combatState);
        if (!TurnParticipants.IsOwnTurn(Owner, side, participants) || Owner.IsDead || Amount <= 0)
        {
            return;
        }

        int stacks = Amount;
        await PowerCmdCompat.Apply<LibraryBleedingPower>(Owner, stacks, Owner, null);
        if (Owner.IsAlive && stacks >= BurstThreshold)
        {
            int remainingHp = (int)Math.Ceiling(Owner.CurrentHp * BurstRemainingPercent / 100m);
            await CreatureCmd.SetCurrentHp(Owner, Math.Max(1, remainingHp));
            SetAmount(0, silent: true);
            await PowerCmd.Remove(this);
        }
    }
}

public sealed class LanguageFloorRagePower : LibraryDurationPowerModel
{
    public const int DefaultTurns = 2;
    public const int StrongPerTurn = 2;

    protected override string LegacyPowerId => "LANGUAGE_FLOOR_RAGE_POWER";

    public override PowerType Type => PowerType.None;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [HoverTipFactory.FromPower<LibraryStrongPower>()];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new DynamicVar("Turns", DefaultTurns),
            new PowerVar<LibraryStrongPower>("Strong", StrongPerTurn)
        ];

    public static async Task<LanguageFloorRagePower?> ApplyWithDuration(
        Creature target,
        Creature applier)
    {
        LanguageFloorRagePower? power = await ApplyWithDuration<LanguageFloorRagePower>(
            target,
            1m,
            DefaultTurns,
            applier,
            null);
        if (power != null && target.CombatState?.CurrentSide == target.Side)
        {
            power.SkipNextDurationTick = true;
        }

        return power;
    }

    public override async Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        await base.AfterSideTurnStart(side, participants, combatState);
        if (TurnParticipants.IsRoundPlayerTurn(side) && Owner.IsAlive)
        {
            await LibraryPowerCmd.Apply<LibraryStrongPower>(
                new ThrowingPlayerChoiceContext(),
                Owner,
                StrongPerTurn,
                0,
                IsPermanent: false,
                Owner,
                null);
        }
    }

    protected override Task OnExpired(PlayerChoiceContext choiceContext)
    {
        if (Owner.Monster is LanguageFloorScarletScar scar)
        {
            scar.OnRageEnded();
        }

        return base.OnExpired(choiceContext);
    }
}

public sealed class LanguageFloorUnrelievedAngerPower : LibraryOfRuinaPowerModel
{
    public const int PermanentStrongPerTurn = 4;

    protected override string LegacyPowerId => "LANGUAGE_FLOOR_UNRELIEVED_ANGER_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [HoverTipFactory.FromPower<LibraryStrongPower>()];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new PowerVar<LibraryStrongPower>("Strong", PermanentStrongPerTurn)];

    public override async Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        await base.AfterSideTurnStart(side, participants, combatState);
        if (side == Owner.Side && Owner.IsAlive)
        {
            LibraryStrongPower? existing = Owner.GetPowerInstances<LibraryStrongPower>()
                .FirstOrDefault(static power => power.AmountPlan.Count == 0);
            if (existing == null)
            {
                await LibraryPowerCmd.Apply<LibraryStrongPower>(
                    new ThrowingPlayerChoiceContext(),
                    Owner,
                    PermanentStrongPerTurn,
                    0,
                    true,
                    Owner,
                    null);
            }
            else
            {
                await PowerCmd.ModifyAmount(
                    new ThrowingPlayerChoiceContext(),
                    existing,
                    PermanentStrongPerTurn,
                    Owner,
                    null);
            }
        }
    }
}

public sealed class LanguageFloorRevengePassivePower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "LANGUAGE_FLOOR_REVENGE_PASSIVE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;
}

public sealed class LanguageFloorWolfHowlPassivePower : LibraryOfRuinaPowerModel
{
    public const int HealthThresholdPercent = 50;
    public const int HowlInterval = 3;

    protected override string LegacyPowerId => "LANGUAGE_FLOOR_WOLF_HOWL_PASSIVE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new DynamicVar("HealthThreshold", HealthThresholdPercent),
            new DynamicVar("HowlInterval", HowlInterval)
        ];
}

public sealed class LanguageFloorWolfHowlingNightmarePassivePower : LibraryOfRuinaPowerModel
{
    public const int HealthThresholdPercent = 50;
    public const int HowlInterval = 2;
    public const int InitialIntentCount = 2;
    public const int BonusIntentCount = 1;
    public const int IntentCount = InitialIntentCount + BonusIntentCount;

    protected override string LegacyPowerId =>
        "LANGUAGE_FLOOR_WOLF_HOWLING_NIGHTMARE_PASSIVE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new DynamicVar("HealthThreshold", HealthThresholdPercent),
            new DynamicVar("HowlInterval", HowlInterval),
            new DynamicVar("InitialIntentCount", InitialIntentCount),
            new DynamicVar("BonusIntentCount", BonusIntentCount),
            new DynamicVar("IntentCount", IntentCount)
        ];
}

public sealed class LanguageFloorRipOpenClawPassivePower : LibraryOfRuinaPowerModel
{
    public const int ScarPerHit = 1;

    protected override string LegacyPowerId => "LANGUAGE_FLOOR_RIP_OPEN_CLAW_PASSIVE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [HoverTipFactory.FromPower<LanguageFloorScarPower>()];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new DynamicVar("ScarPerHit", ScarPerHit),
            new DynamicVar("DamagePerStack", LanguageFloorScarPower.DamagePerStack),
            new DynamicVar("ExtraDamageThreshold", LanguageFloorScarPower.ExtraDamageThreshold),
            new DynamicVar("ExtraDamage", LanguageFloorScarPower.ExtraDamageAtFourStacks)
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
            || target.IsDead
            || result.UnblockedDamage <= 0
            || !ValuePropCompat.IsPoweredAttack(props))
        {
            return;
        }

        Flash();
        await PowerCmdCompat.Apply<LanguageFloorScarPower>(
            target,
            ScarPerHit,
            Owner,
            cardSource);
    }
}

public sealed class LanguageFloorPunishEvilPassivePower : LibraryOfRuinaPowerModel
{
    public const int ChaoResistanceIncrease = 60;
    public const int SpitHpLossPercent = 30;
    public const int SpatCardCostIncrease = 1;

    protected override string LegacyPowerId => "LANGUAGE_FLOOR_PUNISH_EVIL_PASSIVE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new DynamicVar("ChaoResistanceIncrease", ChaoResistanceIncrease),
            new DynamicVar("HpLossPercent", SpitHpLossPercent),
            new EnergyVar("CostIncrease", SpatCardCostIncrease)
        ];
}

public sealed class LanguageFloorDestinedBigBadWolfPassivePower : LibraryOfRuinaPowerModel
{
    public const int TransformHpPercent = 50;

    protected override string LegacyPowerId =>
        "LANGUAGE_FLOOR_DESTINED_BIG_BAD_WOLF_PASSIVE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("HpPercent", TransformHpPercent)];

    internal static decimal TransformHpThreshold(decimal maxHp) =>
        Math.Max(1m, Math.Ceiling(maxHp * TransformHpPercent / 100m));
}

public sealed class LanguageFloorHideInDarknessPassivePower : LibraryOfRuinaPowerModel
{
    public const int DamageThresholdPercent = 25;
    public const int ShadowTurns = 2;

    protected override string LegacyPowerId =>
        "LANGUAGE_FLOOR_HIDE_IN_DARKNESS_PASSIVE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new DynamicVar("DamageThresholdPercent", DamageThresholdPercent),
            new DynamicVar("ShadowTurns", ShadowTurns)
        ];

    internal static bool ExceedsDamageThreshold(int maxHp, int damageTaken) =>
        damageTaken * 100m >= maxHp * DamageThresholdPercent;
}

public sealed class LanguageFloorShadowAmbushPassivePower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId =>
        "LANGUAGE_FLOOR_SHADOW_AMBUSH_PASSIVE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("CardLimit", LanguageFloorCobaltScar.ShadowIntentCount)];

    internal void RefreshCardLimit()
    {
        DynamicVars["CardLimit"].BaseValue = CurrentIntentCardLimit(Owner);
        InvokeDisplayAmountChanged();
    }

    internal static int CurrentIntentCardLimit(Creature owner) =>
        Math.Max(0, owner.Monster?.NextMove.Intents.Count ?? 0);

    internal static bool IsWithinCardLimit(int cardsPlayed, int cardLimit) =>
        cardsPlayed < cardLimit;
}

public sealed class LanguageFloorExhaustionPassivePower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId =>
        "LANGUAGE_FLOOR_EXHAUSTION_PASSIVE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;
}

public sealed class LanguageFloorShadowWolfPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "LANGUAGE_FLOOR_SHADOW_WOLF_POWER";

    public override PowerType Type => PowerType.None;

    protected override bool IsVisibleInternal => false;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override bool ShouldPlay(CardModel card, AutoPlayType _)
    {
        if (!TryGetCobaltScar(card, out LanguageFloorCobaltScar cobalt))
        {
            return true;
        }

        return LanguageFloorShadowAmbushPassivePower.IsWithinCardLimit(
            cobalt.GetShadowCardsPlayed(card.Owner),
            LanguageFloorShadowAmbushPassivePower.CurrentIntentCardLimit(Owner));
    }

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        // As in the base game's SlothPower, count a card at commit time so the next
        // CanPlay/ShouldPlay evaluation blocks the first card over the limit. Replays
        // are extra executions of the same card, not additional cards for this limit.
        if (cardPlay.IsFirstInSeries
            && TryGetCobaltScar(cardPlay.Card, out LanguageFloorCobaltScar cobalt))
        {
            cobalt.RecordShadowCardPlayed(cardPlay.Card.Owner);
        }

        return Task.CompletedTask;
    }

    public override Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (side == CombatSide.Player
            && Owner.Monster is LanguageFloorCobaltScar cobalt)
        {
            cobalt.ResetShadowCardCounters();
        }

        return Task.CompletedTask;
    }

    private bool TryGetCobaltScar(
        CardModel card,
        out LanguageFloorCobaltScar cobalt)
    {
        if (Owner.Monster is LanguageFloorCobaltScar candidate
            && candidate.IsShadowCardRestrictionActive
            && card.Owner.Creature.IsPlayer
            && Owner.CombatState?.CurrentSide == CombatSide.Player)
        {
            cobalt = candidate;
            return true;
        }

        cobalt = null!;
        return false;
    }
}

public sealed class LanguageFloorAngerGaugePower : LibraryOfRuinaPowerModel
{
    public const int MaxAnger = 100;
    public const int WolfAttackAnger = 17;
    public const int WolfRoarAnger = 30;
    public const int ScarletHitLoss = 15;

    protected override string LegacyPowerId => "LANGUAGE_FLOOR_ANGER_GAUGE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Counter;

    //protected override bool IsVisibleInternal => false;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new DynamicVar("MaxAnger", MaxAnger),
            new DynamicVar("WolfAttackAnger", WolfAttackAnger),
            new DynamicVar("WolfRoarAnger", WolfRoarAnger),
            new DynamicVar("ScarletHitLoss", ScarletHitLoss)
        ];

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        SetAmount(0, silent: true);
        return Task.CompletedTask;
    }

    public async Task ChangeAnger(int delta)
    {
        if (Owner.IsDead || delta == 0)
        {
            return;
        }

        int next = Math.Clamp(Amount + delta, 0, MaxAnger);
        if (next == Amount)
        {
            return;
        }

        SetAmount(next, silent: true);
        if (next >= MaxAnger && Owner.Monster is LanguageFloorScarletScar scar)
        {
            await scar.EnterRage();
        }
    }

    public void ResetAnger()
    {
        SetAmount(0, silent: true);
    }
}

public sealed class LanguageFloorLiberationControllerPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId =>
        "LANGUAGE_FLOOR_LIBERATION_CONTROLLER_POWER";

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
            is LanguageFloorLiberationEncounter encounter
                ? encounter.OnBeforeSideTurnStart(side, combatState)
                : Task.CompletedTask;
    }

    public override bool ShouldStopCombatFromEnding()
    {
        return Owner?.CombatState is { } combatState
            && combatState.Encounter
                is LanguageFloorLiberationEncounter encounter
            && encounter.ShouldKeepCombatOpen(combatState);
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        if (Owner?.CombatState is { } combatState
            && combatState.Encounter is LanguageFloorLiberationEncounter encounter)
        {
            encounter.RecoverMissingTerminalPhaseBossAtCombatEnd(combatState);
        }

        return Task.CompletedTask;
    }

    public override bool ShouldDieLate(Creature creature)
    {
        if (Owner?.CombatState?.Encounter
            is LanguageFloorLiberationEncounter encounter)
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
        return Owner?.CombatState?.Encounter
            is LanguageFloorLiberationEncounter encounter
                ? encounter.OnPreventingDeath(creature)
                : Task.CompletedTask;
    }

    public override bool ShouldCreatureBeRemovedFromCombatAfterDeath(
        Creature creature)
    {
        return Owner?.CombatState?.Encounter
                   is not LanguageFloorLiberationEncounter encounter
               || !encounter.ShouldKeepPhaseBossAfterDeath(creature);
    }

    public override bool ShouldAllowHitting(Creature creature)
    {
        return Owner?.CombatState?.Encounter
                   is not LanguageFloorLiberationEncounter encounter
               || !encounter.ShouldSuppressPhaseBossInteraction(creature);
    }

    public override bool ShouldAllowTargeting(Creature target)
    {
        return Owner?.CombatState?.Encounter
                   is not LanguageFloorLiberationEncounter encounter
               || !encounter.ShouldSuppressPhaseBossInteraction(target);
    }

    public override Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        if (result.WasTargetKilled
            && target.Monster is LanguageFloorScarletScar
                or LanguageFloorLostEverythingWolf
                or LanguageFloorCobaltScar)
        {
            LanguageFloorDeathContext.Record(target, dealer);
        }

        return Task.CompletedTask;
    }

    public override async Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (wasRemovalPrevented
            || creature.CombatState?.Encounter
                is not LanguageFloorLiberationEncounter encounter)
        {
            return;
        }

        if (creature.Monster is LanguageFloorSmilingFace)
        {
            await encounter.ResolvePhaseThreeCreatureDeath(creature);
            return;
        }

        if (creature.Monster is LanguageFloorDipsia)
        {
            await encounter.ResolvePhaseFourDeath(creature);
            return;
        }

        if (creature.Monster is LanguageFloorMimicry)
        {
            await encounter.ResolvePhaseFiveDeath(creature);
            return;
        }

        if (creature.Monster is LanguageFloorCobaltScar)
        {
            LanguageFloorDeathContext.Consume(creature);
            await encounter.ResolvePhaseTwoDeath(creature);
            return;
        }

        if (creature.Monster is LanguageFloorScarletScar
            or LanguageFloorLostEverythingWolf)
        {
            Creature? dealer = LanguageFloorDeathContext.Consume(creature);
            await encounter.ResolvePhaseOneDeath(creature, dealer);
        }
    }
}

public sealed class LanguageFloorDeathTrackerPower : LibraryOfRuinaPowerModel
{
    public const int PlayerCurrentHpLossPercent = 70;
    public const int PlayerCurrentHpRemainingPercent = 100 - PlayerCurrentHpLossPercent;

    protected override string LegacyPowerId => "LANGUAGE_FLOOR_DEATH_TRACKER_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new DynamicVar("HpLossPercent", PlayerCurrentHpLossPercent)
        ];

    public override Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        if (result.WasTargetKilled
            && target.Monster is LanguageFloorScarletScar
                or LanguageFloorLostEverythingWolf
                or LanguageFloorCobaltScar)
        {
            LanguageFloorDeathContext.Record(target, dealer);
        }

        return Task.CompletedTask;
    }

    public override async Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (wasRemovalPrevented
            || creature.Monster is not (LanguageFloorScarletScar
                or LanguageFloorLostEverythingWolf
                or LanguageFloorCobaltScar)
            || creature.CombatState?.Encounter is not LanguageFloorLiberationEncounter encounter)
        {
            return;
        }

        if (creature.Monster is LanguageFloorCobaltScar)
        {
            LanguageFloorDeathContext.Consume(creature);
            await encounter.ResolvePhaseTwoDeath(creature);
            return;
        }

        Creature? dealer = LanguageFloorDeathContext.Consume(creature);
        await encounter.ResolvePhaseOneDeath(creature, dealer);
    }
}

internal static class LanguageFloorDeathContext
{
    // 致死者按死亡生物弱键存放：死亡被阻止、没有走到 AfterDeath 的条目不会把整场战斗留在内存里，离开本局时也会清空。
    private static readonly CombatScoped<Creature, Creature?> DealersByDeadCreature = new();

    public static void Clear() => DealersByDeadCreature.Clear();

    public static void Record(Creature creature, Creature? dealer) =>
        DealersByDeadCreature.Set(creature, dealer);

    public static Creature? GetDealer(Creature creature) =>
        DealersByDeadCreature.GetValueOrDefault(creature, null);

    public static Creature? Consume(Creature creature)
    {
        DealersByDeadCreature.TryGetValue(creature, out Creature? dealer);
        DealersByDeadCreature.Remove(creature);
        return dealer;
    }
}

internal static class LanguageFloorCobaltWolfPowerRules
{
    public static bool IsScarDamageDealer(Creature dealer) =>
        dealer.Monster is LanguageFloorCobaltScar
        || dealer.GetPower<LanguageFloorRipOpenClawPassivePower>() != null
        || dealer.GetPower<LanguageFloorHideInDarknessPassivePower>() != null;
}
