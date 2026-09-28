using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.audio;
using LibraryOfRuina.compat;
using LibraryOfRuina.helpers;
using LibraryOfRuina.intents;
using LibraryOfRuina.monsters;
using LibraryOfRuina.patches;
using LibraryOfRuina.powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.specialguests.Rnfmabj;

public abstract class RnfmabjMonsterBase : SpecialGuestMonsterBase
{
    private const string CompositeMoveId = "RNFMABJ_COMPOSITE";
    private const string RouterMoveId = "RNFMABJ_ROUTER";
    private const string HiddenMoveId = "RNFMABJ_HIDDEN";
    private const int DamageValuesPerSlot = 3;

    private AbstractIntent[]? _plannedIntents;
    private MoveState? _compositeState;
    private MoveState? _hiddenState;

    [SavedProperty]
    public int LastPlannedRound { get; private set; } = -1;

    [SavedProperty]
    public int PlanSerial { get; protected set; }

    [SavedProperty]
    public int[] PlannedDamageValues { get; private set; } =
        Enumerable.Repeat(-1, StoredIntentSlots * DamageValuesPerSlot).ToArray();

    protected virtual bool CanPerformMoves => true;

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await RnfmabjPassiveInstaller.EnsureFor(this);
        await InstallFormationPowers();
    }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _plannedIntents = null;
        _compositeState = null;
        _hiddenState = null;
        PlannedDamageValues = (int[])PlannedDamageValues.Clone();
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        _plannedIntents = new AbstractIntent[StoredIntentSlots];
        RefreshPlannedIntents();

        _compositeState = new MoveState(
            CompositeMoveId,
            PerformCompositeMove,
            _plannedIntents)
        {
            MustPerformOnceBeforeTransitioning = true,
        };
        _hiddenState = new MoveState(
            HiddenMoveId,
            static _ => Task.CompletedTask,
            new HiddenIntent());

        var router = new DelegatingMonsterRouterState(
            RouterMoveId,
            (owner, rng) =>
            {
                _ = owner;
                _ = rng;
                return ResolveFollowUpMoveId();
            });
        _compositeState.FollowUpState = router;
        _hiddenState.FollowUpState = router;

        return new MonsterMoveStateMachine(
            [_compositeState, _hiddenState, router],
            !CanPerformMoves
                ? _hiddenState
                : HasStoredIntentPlan
                    ? _compositeState
                    : router);
    }

    private string ResolveFollowUpMoveId()
    {
        if (!CanPerformMoves)
        {
            return HiddenMoveId;
        }

        if (HasStoredIntentPlan)
        {
            RefreshPlannedIntents();
            return CompositeMoveId;
        }

        return HiddenMoveId;
    }

    protected void InstallPlan(
        IReadOnlyList<RnfmabjMove> moves,
        Rng rng,
        int roundNumber)
    {
        int count = Math.Min(
            moves.Count,
            Math.Min(IntentCapacity, StoredIntentSlots));
        var damageValues = Enumerable.Repeat(
            -1,
            StoredIntentSlots * DamageValuesPerSlot).ToArray();

        for (int slot = 0; slot < StoredIntentSlots; slot++)
        {
            RnfmabjMove move = slot < count ? moves[slot] : RnfmabjMove.None;
            SetStoredIntent(slot, (int)move);
            if (move == RnfmabjMove.None)
            {
                continue;
            }

            RnfmabjMoveDefinition definition = RnfmabjMoveDefinitions.Get(move);
            if (!definition.IsAttack)
            {
                continue;
            }

            int rolled = rng.NextInt(
                definition.MinimumDamage,
                definition.MaximumDamage + 1);
            for (int hit = 0; hit < definition.Hits; hit++)
            {
                // Flurry intentionally displays and executes one synchronized
                // roll three times, matching the (5-6)x3 contract.
                damageValues[DamageIndex(slot, hit)] = rolled;
            }
        }

        PlannedDamageValues = damageValues;
        LastPlannedRound = roundNumber;
        PlanSerial++;
        HasCompletedFirstTurn = true;
        RefreshPlannedIntents();
        RevealPlan();
    }

    protected RnfmabjMove GetPlannedMove(int slot) =>
        (RnfmabjMove)GetStoredIntent(slot);

    protected int GetPlannedDamage(int slot, int hit = 0)
    {
        int index = DamageIndex(slot, hit);
        return index >= 0 && index < PlannedDamageValues.Length
            ? Math.Max(0, PlannedDamageValues[index])
            : 0;
    }

    protected void RemovePlannedMoveAt(int removedSlot)
    {
        if (removedSlot < 0 || removedSlot >= StoredIntentSlots)
        {
            throw new ArgumentOutOfRangeException(
                nameof(removedSlot),
                removedSlot,
                null);
        }

        int[] shiftedDamage = (int[])PlannedDamageValues.Clone();
        for (int slot = removedSlot; slot < StoredIntentSlots - 1; slot++)
        {
            SetStoredIntent(slot, GetStoredIntent(slot + 1));
            for (int hit = 0; hit < DamageValuesPerSlot; hit++)
            {
                shiftedDamage[DamageIndex(slot, hit)] =
                    shiftedDamage[DamageIndex(slot + 1, hit)];
            }
        }

        SetStoredIntent(StoredIntentSlots - 1, (int)RnfmabjMove.None);
        for (int hit = 0; hit < DamageValuesPerSlot; hit++)
        {
            shiftedDamage[DamageIndex(StoredIntentSlots - 1, hit)] = -1;
        }

        PlannedDamageValues = shiftedDamage;
        RefreshPlannedIntents();
    }

    protected async Task RefreshPlanDisplay()
    {
        // RevealPlan switches the move state machine (SetMoveImmediate) and stays on the normal
        // exception path; only the intent node refresh is local presentation.
        RefreshPlannedIntents();
        RevealPlan();
        await PresentationGuard.RunAsync(async () =>
        {
            if (NCombatRoom.Instance?.GetCreatureNode(Creature) is { } node)
            {
                await node.RefreshIntents();
            }
        }, "Rnfmabj intent refresh");
    }

    protected void HidePlan()
    {
        if (_hiddenState != null)
        {
            SetMoveImmediate(_hiddenState, forceTransition: true);
        }
    }

    protected void RevealPlan()
    {
        if (_compositeState != null && CanPerformMoves && HasStoredIntentPlan)
        {
            SetMoveImmediate(_compositeState, forceTransition: true);
        }
        else
        {
            HidePlan();
        }
    }

    protected abstract AbstractIntent CreateIntent(RnfmabjMove move, int slot);

    protected abstract Task PerformPlannedMove(int slot, RnfmabjMove move);

    protected async Task<IReadOnlyList<Creature>> AttackAllPlayers(
        int slot,
        RnfmabjMove move)
    {
        RnfmabjMoveDefinition definition = RnfmabjMoveDefinitions.Get(move);
        if (Creature.IsDead || Creature.CombatState == null)
        {
            return [];
        }

        IReadOnlyList<Creature> attackedPlayers = Creature.CombatState.PlayerCreatures
            .Where(static target => target.IsAlive)
            .OrderBy(static target => target.CombatId)
            .ToArray();
        if (!string.IsNullOrWhiteSpace(definition.WindupSfx))
        {
            LocalOggOneShotPlayer.Play(definition.WindupSfx, -2f);
        }
        if (!Creature.IsAlive || !CanPerformMoves)
        {
            return attackedPlayers;
        }

        await CreatureCmd.TriggerAnim(Creature, definition.Animation, 0f);
        await Cmd.Wait(definition.AnimationDelaySeconds);
        if (!Creature.IsAlive || !CanPerformMoves)
        {
            return attackedPlayers;
        }

        if (!definition.IsAttack)
        {
            if (!string.IsNullOrWhiteSpace(definition.ImpactSfx))
            {
                LocalOggOneShotPlayer.Play(definition.ImpactSfx, -2f);
            }

            return attackedPlayers;
        }

        int hitIndex = 0;
        AttackCommand attack = DamageCmd.Attack(GetPlannedDamage(slot))
            .WithHitCount(definition.Hits)
            .FromMonster(this)
            .WithNoAttackerAnim()
            .WithHitFx(definition.DamageType == LibraryDamageType.Blunt
                ? "vfx/vfx_attack_blunt"
                : "vfx/vfx_attack_slash")
            .SpawningHitVfxOnEachCreature()
            .BeforeDamage(() =>
            {
                string? hitSfx = hitIndex % 2 == 1
                    ? definition.AlternateImpactSfx ?? definition.ImpactSfx
                    : definition.ImpactSfx;
                if (!string.IsNullOrWhiteSpace(hitSfx))
                {
                    LocalOggOneShotPlayer.Play(hitSfx, -2f);
                }
                hitIndex++;
                return Task.CompletedTask;
            });

        var command = await attack.Execute(null);
        RecordDirectAttackDamageDealt(
            AttackCommandCompat.Results(command)
                .Where(static result => result.Receiver.IsPlayer));

        return attackedPlayers;
    }

    protected async Task ApplyMoveEffectToPlayers(
        RnfmabjMove move,
        IReadOnlyList<Creature> attackedPlayers)
    {
        RnfmabjMoveDefinition definition = RnfmabjMoveDefinitions.Get(move);
        foreach (Creature player in attackedPlayers
                     .Where(static player => player.IsAlive)
                     .OrderBy(static player => player.CombatId))
        {
            switch (definition.Effect)
            {
                case RnfmabjMoveEffect.ApplyParalysisAndWeak:
                {
                    LibraryOfRuinaParalysisPower? paralysis =
                        await PowerCmdCompat.Apply<LibraryOfRuinaParalysisPower>(
                            player,
                            definition.EffectAmount,
                            Creature,
                            null);
                    paralysis?.SetTurnsRemaining(definition.EffectDurationTurns);
                    await LibraryPowerCmd.Apply<LibraryWeakPower>(
                        player,
                        definition.EffectAmount,
                        definition.EffectDurationTurns,
                        Creature,
                        null);
                    break;
                }
                case RnfmabjMoveEffect.ApplyCorrosion:
                    await PowerCmdCompat.Apply<RnfmabjCorrosionPower>(
                        player,
                        definition.EffectAmount,
                        Creature,
                        null);
                    break;
                case RnfmabjMoveEffect.TransformDrawPileCardToWound:
                    await TransformRandomDrawPileCardToWound(player);
                    break;
            }
        }
    }

    private static async Task TransformRandomDrawPileCardToWound(Creature player)
    {
        if (player.Player == null)
        {
            return;
        }

        IReadOnlyList<CardModel> candidates = PileType.Draw
            .GetPile(player.Player)
            .Cards
            .Select(static (card, index) => (card, index))
            .Where(static candidate =>
                candidate.card.IsTransformable && candidate.card is not Wound)
            .OrderBy(static candidate =>
                candidate.card.Id.Entry,
                StringComparer.Ordinal)
            .ThenBy(static candidate => candidate.index)
            .Select(static candidate => candidate.card)
            .ToArray();
        if (candidates.Count == 0)
        {
            return;
        }

        int index = player.Player.RunState.Rng.CombatCardSelection.NextInt(candidates.Count);
        await CardCmd.TransformTo<Wound>(candidates[index]);
    }

    private async Task PerformCompositeMove(IReadOnlyList<Creature> targets)
    {
        _ = targets;
        if (!CanPerformMoves)
        {
            ClearPlanAndHide();
            return;
        }

        for (int slot = 0;
             slot < StoredIntentSlots && Creature.IsAlive && CanPerformMoves;
             slot++)
        {
            RnfmabjMove move = GetPlannedMove(slot);
            if (move == RnfmabjMove.None)
            {
                break;
            }

            await PerformPlannedMove(slot, move);
        }

        ClearConsumedPlan();
        if (!CanPerformMoves)
        {
            HidePlan();
        }
    }

    private void RefreshPlannedIntents()
    {
        if (_plannedIntents == null)
        {
            return;
        }

        for (int slot = 0; slot < StoredIntentSlots; slot++)
        {
            _plannedIntents[slot] = CreateIntent(GetPlannedMove(slot), slot);
        }
    }

    private void ClearConsumedPlan()
    {
        ClearStoredIntentPlan();
        PlannedDamageValues = Enumerable.Repeat(
            -1,
            StoredIntentSlots * DamageValuesPerSlot).ToArray();
        RefreshPlannedIntents();
    }

    protected void ClearPlanAndHide()
    {
        ClearConsumedPlan();
        HidePlan();
    }

    private static int DamageIndex(int slot, int hit) =>
        slot * DamageValuesPerSlot + Math.Clamp(hit, 0, DamageValuesPerSlot - 1);

    private async Task InstallFormationPowers()
    {
        if (Creature.CombatState == null)
        {
            return;
        }

        if (this is Rnfmabj)
        {
            foreach (Creature player in Creature.CombatState.PlayerCreatures
                         .Where(static player => player.IsAlive)
                         .OrderBy(static player => player.CombatId))
            {
                await PowerCmdCompat.Ensure<SurroundedPower>(
                    player,
                    1,
                    Creature,
                    null,
                    silent: true);
            }
            return;
        }

        await PowerCmdCompat.Ensure<MinionPower>(
            Creature,
            1,
            Creature,
            null,
            silent: true);
        if (this is RnfmabjLeftHand)
        {
            await PowerCmdCompat.Ensure<BackAttackLeftPower>(
                Creature,
                1,
                Creature,
                null,
                silent: true);
        }
        else if (this is RnfmabjRightHand)
        {
            await PowerCmdCompat.Ensure<BackAttackRightPower>(
                Creature,
                1,
                Creature,
                null,
                silent: true);
        }
    }
}

public sealed class Rnfmabj : RnfmabjMonsterBase
{
    internal const int BodyPhaseThreshold = 300;
    internal const int HandDisabledThreshold = 30;
    internal const int BladeCooldownClamp = 1;
    private const int HandRepairThreshold = 50;
    private const int PhaseThreeHandHp = 80;
    private const int DirectiveChecksPerTask = 4;

    private static readonly CardType[][] FirstRoundDirectiveSequences =
    [
        [
            CardType.Attack,
            CardType.Attack,
            CardType.Skill,
            CardType.Skill,
        ],
        [
            CardType.Power,
            CardType.Power,
            CardType.Skill,
            CardType.Skill,
        ],
    ];

    private static readonly CardType[][] SecondRoundDirectiveSequences =
    [
        [
            CardType.Skill,
            CardType.Attack,
            CardType.Power,
            CardType.Power,
        ],
        [
            CardType.Attack,
            CardType.Skill,
            CardType.Power,
            CardType.Power,
        ],
    ];

    private static readonly CardType[] RandomDirectiveCardTypes =
    [
        CardType.Attack,
        CardType.Skill,
        CardType.Power,
    ];

    private static readonly CardType[] RandomDirectiveNonPowerCardTypes =
    [
        CardType.Attack,
        CardType.Skill,
    ];

    public static readonly string[] StaticAssetPaths = RnfmabjCombatAssets.All;

    [SavedProperty]
    public int Phase { get; private set; } = 1;

    [SavedProperty]
    public bool IsUnited { get; private set; }

    [SavedProperty]
    public bool PhaseThreeInitialized { get; private set; }

    [SavedProperty]
    public int BladeCooldown { get; private set; } = 6;

    [SavedProperty]
    public int LastActivatedPlanSerial { get; private set; } = -1;

    [SavedProperty]
    public int PlannedTargetOne { get; private set; } = -1;

    [SavedProperty]
    public int PlannedTargetTwo { get; private set; } = -1;

    [SavedProperty]
    public int PlannedTargetThree { get; private set; } = -1;

    [SavedProperty]
    public int PlannedTargetFour { get; private set; } = -1;

    [SavedProperty]
    public int PlannedTargetFive { get; private set; } = -1;

    [SavedProperty]
    public int DirectivePlanSerial { get; private set; } = -1;

    [SavedProperty]
    public int DirectiveSequenceLength { get; private set; }

    [SavedProperty]
    public int[] DirectiveSequenceCodes { get; private set; } = [];

    [SavedProperty]
    public int CurrentDirectiveTaskIndex { get; private set; }

    [SavedProperty]
    public bool CurrentDirectiveCompleted { get; private set; }

    [SavedProperty]
    public string DirectiveRequiredPlayerNetIds { get; private set; } = string.Empty;

    [SavedProperty]
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

    public override async Task AfterCardPlayed(
        PlayerChoiceContext context,
        CardPlay cardPlay)
    {
        await base.AfterCardPlayed(context, cardPlay);
        if (Creature.IsDead
            || Creature.CombatState?.CurrentSide != CombatSide.Player
            || !TryGetCurrentDirectiveSequence(out CardType[] sequence)
            || CurrentDirectiveCompleted)
        {
            return;
        }

        IReadOnlyList<ulong> requiredPlayers = ParseRequiredPlayerNetIds();
        ulong playerNetId = cardPlay.Player.NetId;
        if (!requiredPlayers.Contains(playerNetId))
        {
            return;
        }

        int progress = GetDirectiveProgress(playerNetId);
        if (progress >= sequence.Length)
        {
            // A player who has completed the current directive stays locked
            // while waiting for every other player.
            return;
        }

        CardType playedType = cardPlay.Card.Type;
        progress = playedType == sequence[progress]
            ? progress + 1
            : 0;
        SetDirectiveProgress(playerNetId, progress);
        if (progress < sequence.Length
            || requiredPlayers.Count == 0
            || requiredPlayers.Any(netId =>
                GetDirectiveProgress(netId) < sequence.Length))
        {
            return;
        }

        bool canceled = await CancelLeftmostDirectiveIntent();
        int taskCount = GetDirectiveTaskCount();
        if (!canceled || CurrentDirectiveTaskIndex + 1 >= taskCount)
        {
            CurrentDirectiveCompleted = true;
            return;
        }

        CurrentDirectiveTaskIndex++;
        CurrentDirectiveCompleted = false;
        DirectiveProgressByPlayerNetId = string.Empty;
    }

    internal RnfmabjDirectiveSnapshot GetDirectiveSnapshot(
        ulong? requestedPlayerNetId)
    {
        if (!TryGetCurrentDirectiveSequence(out CardType[] sequence))
        {
            return RnfmabjDirectiveSnapshot.Hidden;
        }

        IReadOnlyList<ulong> requiredPlayers = ParseRequiredPlayerNetIds();
        ulong? localPlayerNetId = requestedPlayerNetId;
        if (!localPlayerNetId.HasValue && requiredPlayers.Count > 0)
        {
            localPlayerNetId = requiredPlayers[0];
        }

        int progress = localPlayerNetId.HasValue
            ? GetDirectiveProgress(localPlayerNetId.Value)
            : 0;
        int completedPlayers = requiredPlayers.Count(netId =>
            GetDirectiveProgress(netId) >= sequence.Length);
        int taskCount = GetDirectiveTaskCount();
        string fingerprint = string.Join(
            '|',
            DirectivePlanSerial.ToString(CultureInfo.InvariantCulture),
            CurrentDirectiveTaskIndex.ToString(CultureInfo.InvariantCulture),
            CurrentDirectiveCompleted ? "1" : "0",
            DirectiveProgressByPlayerNetId,
            localPlayerNetId?.ToString(CultureInfo.InvariantCulture) ?? "none");
        return new RnfmabjDirectiveSnapshot(
            IsVisible: true,
            Sequence: sequence,
            Progress: Math.Clamp(progress, 0, sequence.Length),
            TaskNumber: CurrentDirectiveTaskIndex + 1,
            TaskCount: taskCount,
            CompletedPlayers: completedPlayers,
            RequiredPlayers: requiredPlayers.Count,
            LocalPlayerCompleted: progress >= sequence.Length,
            CurrentTaskCompleted: CurrentDirectiveCompleted,
            Fingerprint: fingerprint);
    }

    private void EnsureDirectiveTasksForPlan(int round, Rng rng)
    {
        if (DirectivePlanSerial == PlanSerial && HasUsableDirectiveState())
        {
            return;
        }

        int taskCount = Enumerable.Range(0, StoredIntentSlots)
            .Count(slot => IsCancelableDirectiveMove(GetPlannedMove(slot)));
        DirectivePlanSerial = PlanSerial;
        CurrentDirectiveTaskIndex = 0;
        CurrentDirectiveCompleted = false;
        DirectiveProgressByPlayerNetId = string.Empty;
        DirectiveRequiredPlayerNetIds = Creature.CombatState == null
            ? string.Empty
            : string.Join(
                ";",
                Creature.CombatState.Players
                    .Select(static player => player.NetId)
                    .Distinct()
                    .OrderBy(static netId => netId)
                    .Select(static netId =>
                        netId.ToString(CultureInfo.InvariantCulture)));

        if (taskCount <= 0)
        {
            DirectiveSequenceLength = 0;
            DirectiveSequenceCodes = [];
            return;
        }

        CardType[][]? fixedSequences = round switch
        {
            1 => FirstRoundDirectiveSequences,
            2 => SecondRoundDirectiveSequences,
            _ => null,
        };
        DirectiveSequenceLength = DirectiveChecksPerTask;
        DirectiveSequenceCodes = new int[
            taskCount * DirectiveSequenceLength];
        for (int task = 0; task < taskCount; task++)
        {
            CardType[]? fixedSequence = fixedSequences != null
                && task < fixedSequences.Length
                    ? fixedSequences[task]
                    : null;
            bool alreadyRequiresPower = false;
            for (int step = 0; step < DirectiveSequenceLength; step++)
            {
                CardType cardType = fixedSequence?[step]
                    ?? RollRandomDirectiveCardType(
                        rng,
                        alreadyRequiresPower);
                alreadyRequiresPower |= cardType == CardType.Power;
                DirectiveSequenceCodes[
                    task * DirectiveSequenceLength + step] = (int)cardType;
            }
        }
    }

    private static CardType RollRandomDirectiveCardType(
        Rng rng,
        bool alreadyRequiresPower)
    {
        CardType[] cardTypes = alreadyRequiresPower
            ? RandomDirectiveNonPowerCardTypes
            : RandomDirectiveCardTypes;
        return cardTypes[rng.NextInt(cardTypes.Length)];
    }

    private bool HasUsableDirectiveState()
    {
        if (DirectiveSequenceLength == 0)
        {
            return DirectiveSequenceCodes.Length == 0;
        }

        int taskCount = GetDirectiveTaskCount();
        return DirectiveSequenceLength > 0
            && DirectiveSequenceCodes.Length % DirectiveSequenceLength == 0
            && taskCount > 0
            && CurrentDirectiveTaskIndex >= 0
            && CurrentDirectiveTaskIndex < taskCount
            && DirectiveSequenceCodes.All(code =>
                IsDirectiveCardType((CardType)code));
    }

    private bool TryGetCurrentDirectiveSequence(out CardType[] sequence)
    {
        sequence = [];
        if (!HasUsableDirectiveState() || DirectiveSequenceLength <= 0)
        {
            return false;
        }

        int offset = CurrentDirectiveTaskIndex * DirectiveSequenceLength;
        if (offset < 0
            || offset + DirectiveSequenceLength > DirectiveSequenceCodes.Length)
        {
            return false;
        }

        sequence = new CardType[DirectiveSequenceLength];
        for (int step = 0; step < DirectiveSequenceLength; step++)
        {
            sequence[step] = (CardType)DirectiveSequenceCodes[offset + step];
        }

        return true;
    }

    private int GetDirectiveTaskCount() =>
        DirectiveSequenceLength > 0
            ? DirectiveSequenceCodes.Length / DirectiveSequenceLength
            : 0;

    private async Task<bool> CancelLeftmostDirectiveIntent()
    {
        int slot = Enumerable.Range(0, StoredIntentSlots)
            .FirstOrDefault(
                slot => IsCancelableDirectiveMove(GetPlannedMove(slot)),
                -1);
        if (slot < 0)
        {
            return false;
        }

        int[] targets = Enumerable.Range(0, StoredIntentSlots)
            .Select(GetPlannedTarget)
            .ToArray();
        RemovePlannedMoveAt(slot);
        for (int targetSlot = slot; targetSlot < StoredIntentSlots - 1; targetSlot++)
        {
            SetPlannedTarget(targetSlot, targets[targetSlot + 1]);
        }
        SetPlannedTarget(StoredIntentSlots - 1, -1);
        await RefreshPlanDisplay();
        return true;
    }

    private IReadOnlyList<ulong> ParseRequiredPlayerNetIds()
    {
        var result = new List<ulong>();
        foreach (string entry in DirectiveRequiredPlayerNetIds.Split(
                     ';',
                     StringSplitOptions.RemoveEmptyEntries
                     | StringSplitOptions.TrimEntries))
        {
            if (ulong.TryParse(
                    entry,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out ulong netId))
            {
                result.Add(netId);
            }
        }

        result.Sort();
        return result;
    }

    private int GetDirectiveProgress(ulong playerNetId)
    {
        Dictionary<ulong, int> progress = ParseDirectiveProgress();
        return progress.TryGetValue(playerNetId, out int value)
            ? Math.Clamp(value, 0, Math.Max(0, DirectiveSequenceLength))
            : 0;
    }

    private void SetDirectiveProgress(ulong playerNetId, int value)
    {
        Dictionary<ulong, int> progress = ParseDirectiveProgress();
        value = Math.Clamp(value, 0, Math.Max(0, DirectiveSequenceLength));
        if (value == 0)
        {
            progress.Remove(playerNetId);
        }
        else
        {
            progress[playerNetId] = value;
        }

        DirectiveProgressByPlayerNetId = string.Join(
            ";",
            progress.OrderBy(static pair => pair.Key)
                .Select(static pair =>
                    pair.Key.ToString(CultureInfo.InvariantCulture)
                    + ":"
                    + pair.Value.ToString(CultureInfo.InvariantCulture)));
    }

    private Dictionary<ulong, int> ParseDirectiveProgress()
    {
        var result = new Dictionary<ulong, int>();
        foreach (string entry in DirectiveProgressByPlayerNetId.Split(
                     ';',
                     StringSplitOptions.RemoveEmptyEntries
                     | StringSplitOptions.TrimEntries))
        {
            int separator = entry.IndexOf(':');
            if (separator <= 0
                || !ulong.TryParse(
                    entry[..separator],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out ulong netId)
                || !int.TryParse(
                    entry[(separator + 1)..],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int value))
            {
                continue;
            }

            result[netId] = Math.Clamp(
                value,
                0,
                Math.Max(0, DirectiveSequenceLength));
        }

        return result;
    }

    private static bool IsCancelableDirectiveMove(RnfmabjMove move) =>
        move is RnfmabjMove.ExecuteAttack or RnfmabjMove.ExecuteGuard;

    private static bool IsDirectiveCardType(CardType cardType) =>
        cardType is CardType.Attack or CardType.Skill or CardType.Power;

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

internal readonly record struct RnfmabjDirectiveSnapshot(
    bool IsVisible,
    CardType[] Sequence,
    int Progress,
    int TaskNumber,
    int TaskCount,
    int CompletedPlayers,
    int RequiredPlayers,
    bool LocalPlayerCompleted,
    bool CurrentTaskCompleted,
    string Fingerprint)
{
    public static RnfmabjDirectiveSnapshot Hidden { get; } = new(
        IsVisible: false,
        Sequence: [],
        Progress: 0,
        TaskNumber: 0,
        TaskCount: 0,
        CompletedPlayers: 0,
        RequiredPlayers: 0,
        LocalPlayerCompleted: false,
        CurrentTaskCompleted: false,
        Fingerprint: "hidden");
}

public abstract class RnfmabjHandBase : RnfmabjMonsterBase, LibraryOfRuina.helpers.IFinalHpLossClamp
{
    internal const int SurvivalHp = 1;

    private static readonly int[] NoEmotionThresholds = [];

    [SavedProperty]
    public bool IsFakeDead { get; private set; }

    [SavedProperty]
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

public sealed class RnfmabjLeftHand : RnfmabjHandBase
{
    public static readonly string[] StaticAssetPaths = RnfmabjCombatAssets.All;

    protected override bool IsLeftHand => true;

    public override IEnumerable<string> AssetPaths => StaticAssetPaths;

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData =>
        new()
        {
            Blunt = LibraryResistanceLevel.Normal,
            Pierce = LibraryResistanceLevel.Endure,
            Slash = LibraryResistanceLevel.Normal,
        };
}

public sealed class RnfmabjRightHand : RnfmabjHandBase
{
    public static readonly string[] StaticAssetPaths = RnfmabjCombatAssets.All;

    protected override bool IsLeftHand => false;

    public override IEnumerable<string> AssetPaths => StaticAssetPaths;

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData =>
        new()
        {
            Blunt = LibraryResistanceLevel.Normal,
            Pierce = LibraryResistanceLevel.Normal,
            Slash = LibraryResistanceLevel.Endure,
        };
}

internal static class RnfmabjCombatAssets
{
    private const string AudioRoot =
        "res://audio/special_guests/rnfmabj/combat/";

    public static readonly string[] All =
    [
        "res://images/powers/rnfmabj_corrosion_power.png",
        "res://images/powers/rnfmabj_counter_evade_power.png",
        "res://images/powers/rnfmabj_hand_mechanics_power.png",
        "res://images/powers/rnfmabj_mechanics_power.png",
        "res://images/powers/rnfmabj_twisted_blade_passive_power.png",
        AudioRoot + "Yan_GreatSword_Finish.ogg",
        AudioRoot + "Yan_GreatSword_Start.ogg",
        AudioRoot + "Yan_Guard.ogg",
        AudioRoot + "Yan_Lib_Hori.ogg",
        AudioRoot + "Yan_Lib_Vert.ogg",
        AudioRoot + "Yan_Stab.ogg",
        AudioRoot + "Yan_Stigma_Atk.ogg",
        AudioRoot + "Yan_Stigma_Start.ogg",
        AudioRoot + "Yan_Typing_Atk.ogg",
        AudioRoot + "Yan_Typing_Start.ogg",
        AudioRoot + "Yan_Vert.ogg",
    ];
}
