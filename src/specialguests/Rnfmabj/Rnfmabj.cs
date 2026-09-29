using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.intents;
using LibraryOfRuina.patches;
using LibraryOfRuina.powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.specialguests.Rnfmabj;

// 本体：阶段推进、双手的调度与本体自己的计划。指令（Prescript）任务在 Rnfmabj.Directives.cs，规则在 RnfmabjDirectiveTracker。
public sealed partial class Rnfmabj : RnfmabjMonsterBase
{
    internal const int BodyPhaseThreshold = 300;
    internal const int HandDisabledThreshold = 30;
    internal const int BladeCooldownClamp = 1;
    private const int HandRepairThreshold = 50;
    private const int PhaseThreeHandHp = 80;

    public static readonly string[] StaticAssetPaths = RnfmabjCombatAssets.All;

    public int Phase { get; private set; } = 1;

    public bool IsUnited { get; private set; }

    public bool PhaseThreeInitialized { get; private set; }

    public int BladeCooldown { get; private set; } = 6;

    public int LastActivatedPlanSerial { get; private set; } = -1;

    public int PlannedTargetOne { get; private set; } = -1;

    public int PlannedTargetTwo { get; private set; } = -1;

    public int PlannedTargetThree { get; private set; } = -1;

    public int PlannedTargetFour { get; private set; } = -1;

    public int PlannedTargetFive { get; private set; } = -1;

    public int DirectivePlanSerial { get; private set; } = -1;

    public int DirectiveSequenceLength { get; private set; }

    public int[] DirectiveSequenceCodes { get; private set; } = [];

    public int CurrentDirectiveTaskIndex { get; private set; }

    public bool CurrentDirectiveCompleted { get; private set; }

    public string DirectiveRequiredPlayerNetIds { get; private set; } = string.Empty;

    public string DirectiveProgressByPlayerNetId { get; private set; } = string.Empty;

    public override int MinInitialHp => 700;

    public override int MaxInitialHp => 700;

    public override int DefaultChaoResistance => 280;

    public override LibraryCreatureResistanceData.Resistance?
        DefaultPhysicalResistanceData => new()
        {
            Slash = LibraryResistanceLevel.Normal,
            Pierce = LibraryResistanceLevel.Normal,
            Blunt = LibraryResistanceLevel.Endure,
        };

    public override LibraryCreatureResistanceData.Resistance?
        DefaultChaoResistanceData => new()
        {
            Slash = LibraryResistanceLevel.Normal,
            Pierce = LibraryResistanceLevel.Normal,
            Blunt = LibraryResistanceLevel.Endure,
        };

    protected override int InitialIntentCapacity => 3;

    public override IEnumerable<string> AssetPaths => StaticAssetPaths;

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        DirectiveSequenceCodes = (int[])DirectiveSequenceCodes.Clone();
    }

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        if (Creature.GetPower<RnfmabjCounterEvadePower>() is { } legacyCounter)
        {
            await PowerCmd.Remove(legacyCounter);
        }

        foreach (RnfmabjHandBase hand in Hands())
        {
            hand.ApplySavedCombatAvailability();
        }
    }

    public override async Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        await base.AfterDeath(
            choiceContext,
            creature,
            wasRemovalPrevented,
            deathAnimLength);
        if (creature != Creature || wasRemovalPrevented)
        {
            return;
        }

        Creature[] livingHands = Hands()
            .Select(static hand => hand.Creature)
            .Where(static hand => hand.IsAlive)
            .ToArray();
        if (livingHands.Length > 0)
        {
            await CreatureCmd.Kill(livingHands, force: true);
        }
    }

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        await base.BeforeSideTurnStart(
            choiceContext,
            side,
            participants,
            combatState);
        if (side != CombatSide.Player
            || Creature.IsDead
            || CombatManager.Instance.PlayersTakingExtraTurn.Count > 0)
        {
            return;
        }

        int round = combatState.RoundNumber;
        if (LastPlannedRound == round)
        {
            EnsureDirectiveTasksForPlan(round, RunRng.MonsterAi);
            await ActivatePlanOnce();
            return;
        }

        await PlanNormalRound(round);
    }

    protected override AbstractIntent CreateIntent(RnfmabjMove move, int slot)
    {
        int damage = GetPlannedDamage(slot);
        RnfmabjMoveDefinition definition = RnfmabjMoveDefinitions.Get(move);
        return definition.Effect switch
        {
            RnfmabjMoveEffect.ExecuteAttack =>
                new DetailedBuffIntent<LibraryStrongPower>(
                    GetExecuteBuffStacks(),
                    DetailedBuffTargetScope.Self,
                    ResolveLivingHandsForIntent),
            RnfmabjMoveEffect.ExecuteGuard =>
                new DetailedBuffIntent<LibraryProtectionPower>(
                    GetExecuteBuffStacks(),
                    DetailedBuffTargetScope.Self,
                    ResolveLivingHandsForIntent),
            _ => definition.IntentKind switch
            {
                RnfmabjMoveIntentKind.Buff => new BuffIntent(),
                RnfmabjMoveIntentKind.Debuff => new DebuffIntent(),
                RnfmabjMoveIntentKind.CardDebuff => new CardDebuffIntent(),
                RnfmabjMoveIntentKind.Defend => new DefendIntent(),
                RnfmabjMoveIntentKind.Heal => new HealIntent(),
                RnfmabjMoveIntentKind.GroupAttackDebuff =>
                    new IndiscriminateAttackIntent(() => damage, () => 1, null),
                RnfmabjMoveIntentKind.Hidden => new HiddenIntent(),
                _ => new UnknownIntent(),
            },
        };
    }

    protected override async Task PerformPlannedMove(int slot, RnfmabjMove move)
    {
        RnfmabjMoveDefinition definition = RnfmabjMoveDefinitions.Get(move);
        switch (definition.Effect)
        {
            case RnfmabjMoveEffect.ExecuteAttack:
                await ApplyToLivingHands(async hand =>
                    await LibraryPowerCmd.Apply<LibraryStrongPower>(
                        hand.Creature,
                        GetExecuteBuffStacks(),
                        turns: 1,
                        Creature,
                        null));
                break;
            case RnfmabjMoveEffect.ExecuteGuard:
                await ApplyToLivingHands(async hand =>
                    await LibraryPowerCmd.Apply<LibraryProtectionPower>(
                        hand.Creature,
                        GetExecuteBuffStacks(),
                        turns: 1,
                        Creature,
                        null));
                break;
            case RnfmabjMoveEffect.ExecuteAlert:
                if (Creature.CombatState != null)
                {
                    foreach (Creature player in Creature.CombatState.PlayerCreatures
                                 .Where(static player => player.IsAlive)
                                 .OrderBy(static player => player.CombatId))
                    {
                        await PowerCmdCompat.Apply<NullifyPower>(
                            player,
                            1,
                            Creature,
                            null);
                    }
                }
                break;
            case RnfmabjMoveEffect.RepairTarget:
            {
                Creature? target = ResolvePlannedTarget(slot);
                if (target is { IsAlive: true }
                    && target.Monster is RnfmabjHandBase { IsFakeDead: false })
                {
                    await CreatureCmd.Heal(
                        target,
                        MultiplayerScalingPatchHelper.ScaleMonsterHealAmount(
                            target,
                            definition.EffectAmount));
                }
                break;
            }
            case RnfmabjMoveEffect.BlockAllGuests:
                if (!string.IsNullOrWhiteSpace(definition.ImpactSfx))
                {
                    RnfmabjBlockAudio.PlayLocalBlockSfx(definition.ImpactSfx);
                }
                Creature[] guests = LivingNonFakeDeadGuests().ToArray();
                for (int index = 0; index < guests.Length; index++)
                {
                    await CreatureCmd.GainBlock(
                        guests[index],
                        definition.EffectAmount,
                        ValueProp.Move,
                        null,
                        fast: true);
                    if (index < guests.Length - 1)
                    {
                        await RnfmabjBlockAudio.WaitForNextConsecutiveGain();
                    }
                }
                break;
            case RnfmabjMoveEffect.RepairAllGuests:
                foreach (Creature guest in LivingNonFakeDeadGuests())
                {
                    await CreatureCmd.Heal(
                        guest,
                        MultiplayerScalingPatchHelper.ScaleMonsterHealAmount(
                            guest,
                            definition.EffectAmount));
                }
                break;
            case RnfmabjMoveEffect.ApplyCorrosion:
            {
                IReadOnlyList<Creature> players = await AttackAllPlayers(slot, move);
                await ApplyMoveEffectToPlayers(move, players);
                break;
            }
        }
    }

    private async Task PlanNormalRound(int round)
    {
        IReadOnlyList<RnfmabjHandBase> hands = Hands();
        if (HasCompletedFirstTurn)
        {
            BladeCooldown = Math.Max(0, BladeCooldown - 1);
        }

        await ResolvePhaseTransition(hands);
        foreach (RnfmabjHandBase hand in hands)
        {
            if (hand.RefreshCombatAvailability())
            {
                await FakeDeathDebuffHelper.ClearNonPassivePowers(
                    hand.Creature,
                    static power => power is MinionPower);
            }
        }

        List<RnfmabjMove> pattern = BuildPattern(hands);
        int normalCount = Math.Min(pattern.Count, Math.Min(IntentCapacity, StoredIntentSlots));
        if (BladeCooldown <= 0 && normalCount > 0)
        {
            pattern[normalCount - 1] = RnfmabjMove.TwistedBlade;
            BladeCooldown = 2;
        }

        ClearPlannedTargets();
        SelectTargets(pattern, hands, RunRng.MonsterAi);
        InstallPlan(pattern, RunRng.MonsterAi, round);

        foreach (RnfmabjHandBase hand in hands)
        {
            hand.PlanRound(round, Phase, IsUnited, RunRng.MonsterAi);
        }

        EnsureDirectiveTasksForPlan(round, RunRng.MonsterAi);

        RnfmabjTwistedBladePassivePower? bladePower =
            Creature.GetPower<RnfmabjTwistedBladePassivePower>();
        bladePower?.RefreshDisplay();
        await ActivatePlanOnce();
    }

    private async Task ResolvePhaseTransition(IReadOnlyList<RnfmabjHandBase> hands)
    {
        int bodyThreshold = ScaleSpecialGuestAmount(BodyPhaseThreshold);
        int handThreshold = ScaleSpecialGuestAmount(HandDisabledThreshold);
        if (Phase < 3 && Creature.CurrentHp <= bodyThreshold)
        {
            Phase = 3;
            IntentCapacity = Math.Max(IntentCapacity, 4);
            BladeCooldown = Math.Min(BladeCooldown, BladeCooldownClamp);
            if (!PhaseThreeInitialized)
            {
                PhaseThreeInitialized = true;
                foreach (RnfmabjHandBase hand in hands)
                {
                    hand.ResetPhaseThreePattern();
                    await CreatureCmd.SetCurrentHp(
                        hand.Creature,
                        ScaleSpecialGuestAmount(PhaseThreeHandHp));
                }
            }

            if (IsUnited)
            {
                IsUnited = false;
                await PresentationGuard.RunAsync(
                    () => CreatureCmd.TriggerAnim(Creature, "Split", 0.75f),
                    "Rnfmabj split animation");
                await FakeDeathDebuffHelper.ClearDebuffs(Creature);

                if (Creature is LibraryCreature libraryCreature)
                {
                    libraryCreature.RestorePreStunResistance();
                    await LibraryCreatureCmd.SetCurrentChaoValue(
                        libraryCreature,
                        libraryCreature.MaxChaoValue);
                }
            }
            return;
        }

        if (Phase == 1
            && hands.Count > 0
            && hands.All(hand => hand.Creature.CurrentHp <= handThreshold))
        {
            Phase = 2;
            IntentCapacity = Math.Max(IntentCapacity, 4);
            BladeCooldown = Math.Min(BladeCooldown, BladeCooldownClamp);
            IsUnited = true;
            await CreatureCmd.TriggerAnim(Creature, "Union", 0.75f);
        }
    }

    private List<RnfmabjMove> BuildPattern(IReadOnlyList<RnfmabjHandBase> hands)
    {
        int activeThreshold = ScaleSpecialGuestAmount(HandDisabledThreshold);
        bool anyActive = hands.Any(hand => hand.Creature.CurrentHp > activeThreshold);
        bool anyRepairable = hands.Any(hand =>
            hand.Creature.CurrentHp <= activeThreshold);
        return Phase switch
        {
            1 =>
            [
                RnfmabjMove.ExecuteAttack,
                RnfmabjMove.ExecuteGuard,
                RnfmabjMove.ExecuteAlert,
            ],
            2 =>
            [
                RnfmabjMove.ExecuteHold,
                RnfmabjMove.ExecuteHold,
                RnfmabjMove.ExecuteHold,
                RnfmabjMove.ExecuteHold,
            ],
            _ when anyActive =>
            [
                RnfmabjMove.ExecuteAttack,
                RnfmabjMove.ExecuteGuard,
                anyRepairable
                    ? RnfmabjMove.ExecuteRepair
                    : RnfmabjMove.ExecuteGuard,
                RnfmabjMove.ExecuteAlert,
            ],
            _ =>
            [
                RnfmabjMove.RepairAll,
                RnfmabjMove.ExecuteRepair,
                RnfmabjMove.ExecuteRepair,
            ],
        };
    }

    private void SelectTargets(
        IReadOnlyList<RnfmabjMove> pattern,
        IReadOnlyList<RnfmabjHandBase> hands,
        Rng rng)
    {
        int activeThreshold = ScaleSpecialGuestAmount(HandDisabledThreshold);
        int repairThreshold = ScaleSpecialGuestAmount(HandRepairThreshold);
        int count = Math.Min(pattern.Count, Math.Min(IntentCapacity, StoredIntentSlots));
        for (int slot = 0; slot < count; slot++)
        {
            IReadOnlyList<RnfmabjHandBase> candidates = pattern[slot] switch
            {
                RnfmabjMove.ExecuteAttack or RnfmabjMove.ExecuteGuard => hands
                    .Where(hand => hand.Creature.IsAlive
                                   && hand.Creature.CurrentHp > activeThreshold)
                    .OrderBy(hand => hand.Creature.CombatId)
                    .ToArray(),
                RnfmabjMove.ExecuteRepair => hands
                    .Where(hand => hand.Creature.IsAlive
                                   && !hand.IsFakeDead
                                   && hand.Creature.CurrentHp <= repairThreshold)
                    .OrderBy(hand => hand.Creature.CombatId)
                    .ToArray(),
                _ => [],
            };
            if (candidates.Count == 0)
            {
                continue;
            }

            RnfmabjHandBase selected = candidates[rng.NextInt(candidates.Count)];
            SetPlannedTarget(slot, checked((int)(selected.Creature.CombatId ?? 0)));
        }
    }

    private Task ActivatePlanOnce()
    {
        if (LastActivatedPlanSerial == PlanSerial)
        {
            return Task.CompletedTask;
        }

        LastActivatedPlanSerial = PlanSerial;
        return Task.CompletedTask;
    }

    private static int GetExecuteBuffStacks() =>
        AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            5,
            3);

    private static IReadOnlyList<Creature> ResolveLivingHandsForIntent(
        Creature owner) =>
        owner.CombatState?.Enemies
            .Where(static enemy =>
                enemy.IsAlive
                && enemy.Monster is RnfmabjHandBase { IsFakeDead: false })
            .ToArray()
        ?? [];

    private IReadOnlyList<RnfmabjHandBase> Hands() =>
        Creature.CombatState?.Enemies
            .OrderBy(static enemy => enemy.CombatId)
            .Select(static enemy => enemy.Monster)
            .OfType<RnfmabjHandBase>()
            .ToArray()
        ?? Array.Empty<RnfmabjHandBase>();

    private async Task ApplyToLivingHands(Func<RnfmabjHandBase, Task> apply)
    {
        foreach (RnfmabjHandBase hand in Hands().Where(static hand =>
                     hand.Creature.IsAlive && !hand.IsFakeDead))
        {
            await apply(hand);
        }
    }

    private IEnumerable<Creature> LivingNonFakeDeadGuests() =>
        Creature.CombatState?.Enemies
            .Where(static guest =>
                guest.IsAlive
                && guest.Monster is not RnfmabjHandBase { IsFakeDead: true })
            .OrderBy(static guest => guest.CombatId)
            .ToArray()
        ?? Array.Empty<Creature>();

    private Creature? ResolvePlannedTarget(int slot)
    {
        int targetId = GetPlannedTarget(slot);
        return targetId < 0
            ? null
            : Creature.CombatState?.Enemies.FirstOrDefault(
                target => target.CombatId == (uint)targetId);
    }

    private int GetPlannedTarget(int slot) => slot switch
    {
        0 => PlannedTargetOne,
        1 => PlannedTargetTwo,
        2 => PlannedTargetThree,
        3 => PlannedTargetFour,
        4 => PlannedTargetFive,
        _ => -1,
    };

    private void SetPlannedTarget(int slot, int combatId)
    {
        switch (slot)
        {
            case 0: PlannedTargetOne = combatId; break;
            case 1: PlannedTargetTwo = combatId; break;
            case 2: PlannedTargetThree = combatId; break;
            case 3: PlannedTargetFour = combatId; break;
            case 4: PlannedTargetFive = combatId; break;
        }
    }

    private void ClearPlannedTargets()
    {
        for (int slot = 0; slot < StoredIntentSlots; slot++)
        {
            SetPlannedTarget(slot, -1);
        }
    }
}
