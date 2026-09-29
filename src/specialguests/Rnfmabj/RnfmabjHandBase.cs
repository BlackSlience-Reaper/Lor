using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.intents;
using LibraryOfRuina.powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.specialguests.Rnfmabj;

public abstract class RnfmabjHandBase : RnfmabjMonsterBase, LibraryOfRuina.infra.helpers.IFinalHpLossClamp
{
    internal const int SurvivalHp = 1;

    private static readonly int[] NoEmotionThresholds = [];

    public bool IsFakeDead { get; private set; }

    public int PhaseThreePatternStep { get; private set; }

    protected abstract bool IsLeftHand { get; }

    protected override IReadOnlyList<int> EmotionThresholds =>
        NoEmotionThresholds;

    public override bool HasEmotionTrack => false;

    protected override int InitialIntentCapacity => 2;

    protected override bool CanPerformMoves =>
        !IsFakeDead && Boss is not { IsUnited: true };

    public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 150, 130);

    public override int MaxInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 150, 130);

    public override int DefaultChaoResistance => 0;

    public override LibraryCreatureResistanceData.Resistance?
        DefaultPhysicalResistanceData => new()
        {
            Slash = LibraryResistanceLevel.Normal,
            Pierce = LibraryResistanceLevel.Normal,
            Blunt = LibraryResistanceLevel.Normal,
        };

    public override LibraryCreatureResistanceData.Resistance?
        DefaultChaoResistanceData => new(LibraryResistanceLevel.Immune);

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        ApplySavedCombatAvailability();
        if (IsFakeDead)
        {
            await FakeDeathDebuffHelper.ClearNonPassivePowers(
                Creature,
                static power => power is MinionPower);
        }
    }

    public override bool ShouldAllowHitting(Creature creature) =>
        creature != Creature || CanPerformMoves;

    public override async Task AfterCurrentHpChanged(Creature creature, decimal delta)
    {
        await base.AfterCurrentHpChanged(creature, delta);
        await RefreshAvailabilityAfterHpChanged(creature);
    }

    public override async Task AfterCurrentHpChanged(
        Creature creature,
        decimal delta,
        LibraryDamageType type)
    {
        await base.AfterCurrentHpChanged(creature, delta, type);
        await RefreshAvailabilityAfterHpChanged(creature);
    }

    private async Task RefreshAvailabilityAfterHpChanged(Creature creature)
    {
        if (creature != Creature || Boss is not { Creature.IsAlive: true })
        {
            return;
        }

        if (RefreshCombatAvailability())
        {
            await FakeDeathDebuffHelper.ClearNonPassivePowers(
                Creature,
                static power => power is MinionPower);
            await RefreshPlanDisplay();
        }
    }

    public override bool ShouldAllowTargeting(Creature target) =>
        target != Creature || CanPerformMoves;

    public decimal ClampFinalHpLoss(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource) =>
        ClampLethalDamage(target, amount);

    public override bool ShouldDieLate(Creature creature) =>
        creature != Creature || Boss is not { Creature.IsAlive: true };

    public override Task AfterPreventingDeath(Creature creature) =>
        creature == Creature && Boss is { Creature.IsAlive: true }
            ? CreatureCmd.SetCurrentHp(Creature, SurvivalHp)
            : Task.CompletedTask;

    public override decimal ModifyChaoDamageAdditive(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay,
        LibraryDamageType type) =>
        target == Creature && amount > 0m ? -amount : 0m;

    internal bool RefreshCombatAvailability()
    {
        int threshold = ScaleSpecialGuestAmount(Rnfmabj.HandDisabledThreshold);
        bool enteredFakeDeath = !IsFakeDead
            && Creature.CurrentHp <= threshold;
        IsFakeDead = Creature.CurrentHp <= threshold;
        ApplySavedCombatAvailability();
        return enteredFakeDeath;
    }

    internal void ApplySavedCombatAvailability()
    {
        bool interactable = CanPerformMoves;
        PresentationGuard.Run(
            () => NCombatRoom.Instance?.SetCreatureIsInteractable(Creature, interactable),
            "Rnfmabj interactable state");
        if (!interactable)
        {
            ClearPlanAndHide();
        }
    }

    internal void PlanRound(int round, int phase, bool isUnited, Rng rng)
    {
        List<RnfmabjMove> pattern;
        if (phase == 3)
        {
            int mode = PhaseThreePatternStep % 2 == 0 ? 1 : 3;
            pattern = GetPattern(mode, firstTurn: false);
            PhaseThreePatternStep++;
        }
        else
        {
            int mode = Math.Clamp(PatternIndex, 0, 3);
            pattern = GetPattern(mode, !HasCompletedFirstTurn);
            PatternIndex = (mode + 1) % 4;
        }

        if (IsFakeDead || isUnited)
        {
            InstallPlan([], rng, round);
            HidePlan();
            return;
        }

        InstallPlan(pattern, rng, round);
    }

    internal void ResetPhaseThreePattern()
    {
        PhaseThreePatternStep = 0;
    }

    protected override AbstractIntent CreateIntent(RnfmabjMove move, int slot)
    {
        RnfmabjMoveDefinition definition = RnfmabjMoveDefinitions.Get(move);
        int damage = GetPlannedDamage(slot);
        int block = definition.GetHandBlockAmount(IsLeftHand);
        return move switch
        {
            RnfmabjMove.GiantPunch =>
                new CombinedAttackDefendIntent(
                    () => damage,
                    () => definition.Hits,
                    "RNFMABJ_GIANT_PUNCH.description",
                    block),
            RnfmabjMove.GiantPalm =>
                new BadgedDebuffIntent(
                    [
                        IntentBadge.FromPower<LibraryOfRuinaParalysisPower>(
                            definition.EffectAmount,
                            definition.EffectDurationTurns.ToString(
                                CultureInfo.InvariantCulture),
                            definition.EffectAmount.ToString(
                                CultureInfo.InvariantCulture)),
                        IntentBadge.FromPower<LibraryWeakPower>(
                            definition.EffectAmount,
                            definition.EffectDurationTurns.ToString(
                                CultureInfo.InvariantCulture),
                            definition.EffectAmount.ToString(
                                CultureInfo.InvariantCulture)),
                    ],
                    definition.EffectAmount,
                    "RNFMABJ_GIANT_PALM.description"),
            RnfmabjMove.OminousBrand =>
                new CombinedAttackDebuffIntent(
                    () => damage,
                    () => definition.Hits,
                    "RNFMABJ_OMINOUS_BRAND.description",
                    IntentBadge.FromPower<RnfmabjCorrosionPower>(
                        definition.EffectAmount)),
            RnfmabjMove.LockTarget =>
                new CardDebuffIntent(),
            RnfmabjMove.Flurry =>
                new MultiAttackIntent(damage, () => definition.Hits),
            RnfmabjMove.None => new HiddenIntent(),
            _ => new UnknownIntent(),
        };
    }

    protected override async Task PerformPlannedMove(int slot, RnfmabjMove move)
    {
        RnfmabjMoveDefinition definition = RnfmabjMoveDefinitions.Get(move);
        IReadOnlyList<Creature> players = await AttackAllPlayers(slot, move);
        int block = definition.GetHandBlockAmount(IsLeftHand);
        if (block > 0 && Creature.IsAlive)
        {
            if (!string.IsNullOrWhiteSpace(definition.BlockSfx))
            {
                RnfmabjBlockAudio.PlayLocalBlockSfx(definition.BlockSfx);
            }
            await CreatureCmd.GainBlock(
                Creature,
                block,
                ValueProp.Move,
                null);
        }

        await ApplyMoveEffectToPlayers(move, players);
    }

    private List<RnfmabjMove> GetPattern(int mode, bool firstTurn)
    {
        if (IsLeftHand)
        {
            return mode switch
            {
                0 => firstTurn
                    ? [RnfmabjMove.GiantPunch]
                    : [RnfmabjMove.GiantPunch, RnfmabjMove.GiantPalm],
                1 => [RnfmabjMove.OminousBrand, RnfmabjMove.Flurry],
                2 => [RnfmabjMove.GiantPalm, RnfmabjMove.Flurry],
                _ => [RnfmabjMove.GiantPunch, RnfmabjMove.LockTarget],
            };
        }

        return mode switch
        {
            0 => firstTurn
                ? [RnfmabjMove.Flurry]
                : [RnfmabjMove.Flurry, RnfmabjMove.GiantPunch],
            1 => [RnfmabjMove.GiantPunch, RnfmabjMove.GiantPalm],
            2 => [RnfmabjMove.GiantPunch, RnfmabjMove.Flurry],
            _ => [RnfmabjMove.GiantPalm, RnfmabjMove.LockTarget],
        };
    }

    private decimal ClampLethalDamage(Creature target, decimal amount)
    {
        if (target != Creature
            || amount <= 0m
            || Boss is not { Creature.IsAlive: true })
        {
            return amount;
        }

        return Math.Min(
            amount,
            Math.Max(0m, target.CurrentHp - SurvivalHp));
    }

    private Rnfmabj? Boss => Creature.CombatState?.Enemies
        .Select(static enemy => enemy.Monster)
        .OfType<Rnfmabj>()
        .FirstOrDefault();
}
