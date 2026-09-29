using LibraryLib.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.audio;
using LibraryOfRuina.compat;
using LibraryOfRuina.encounters;
using LibraryOfRuina.encounters.LanguageFloorLiberation;
using LibraryOfRuina.helpers;
using LibraryOfRuina.intents;
using LibraryOfRuina.powers.LanguageFloorLiberation;
using LibraryOfRuina.powers.LittleRedMercenary;
using LibraryOfRuina.visuals.LanguageFloorLiberation;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.monsters.LanguageFloorLiberation;

public sealed class LanguageFloorLostEverythingWolf :
    LiberationPhaseBossMonster,
    ITargetedMonsterAttackProvider
{
    private const string HighCompositeMoveId = "LANGUAGE_FLOOR_WOLF_COMPOSITE_HIGH";
    private const string LowCompositeMoveId = "LANGUAGE_FLOOR_WOLF_COMPOSITE_LOW";
    private const string RouterMoveId = "LANGUAGE_FLOOR_WOLF_ROUTER";
    private const string AttackSfxPath = "res://audio/sfx/little_red_mercenary/wolf_bite.ogg";
    private const string HowlSfxPath = "res://audio/sfx/little_red_mercenary/wolf_howl.ogg";
    private const string SlashAnimationTrigger = "WolfSlash";
    private const string SpecialAnimationTrigger = "WolfS2";
    private const string HowlAnimationTrigger = "WolfHowl";
    internal const int HowlWeak = 2;
    private const int HowlWeakTurns = 1;

    [SavedProperty]
    public int PlannedMoveOne { get; private set; } = -1;

    [SavedProperty]
    public int PlannedMoveTwo { get; private set; } = -1;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int PlannedMoveThree { get; private set; } = -1;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int PlannedMoveFour { get; private set; } = -1;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int WolfTurnCount { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool LowHealthMode { get; private set; }

    private MoveState? _highCompositeState;
    private MoveState? _lowCompositeState;
    private AbstractIntent[]? _highCompositeIntents;
    private AbstractIntent[]? _lowCompositeIntents;

    public override int LiberationPhase => 1;

    protected override bool ReviveTriggerPlaysHitAnimation => true;

    public override bool ShouldDisappearFromDoom => false;

    public override int MinInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 397, 390);

    public override int MaxInitialHp =>
        AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 400, 394);

    private bool _pendingLowHealthMode;

    public override int DefaultChaoResistance => 130;

    public override LibraryCreatureResistanceData.Resistance? DefaultPhysicalResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Normal,
        Pierce = LibraryResistanceLevel.Normal,
        Blunt = LibraryResistanceLevel.Normal
    };

    public override LibraryCreatureResistanceData.Resistance? DefaultChaoResistanceData => new()
    {
        Slash = LibraryResistanceLevel.Resist,
        Pierce = LibraryResistanceLevel.Resist,
        Blunt = LibraryResistanceLevel.Resist  
    };

    public override IEnumerable<string> AssetPaths
    {
        get
        {
            return LanguageFloorLostEverythingWolfCreatureVisuals.Profile
                .AssetPaths
                .Concat(
                [
                    "res://images/powers/art_floor_green_passive_power.png",
                    "res://images/powers/language_floor_scar_power.png",
                    "res://images/powers/language_floor_rip_open_claw_passive_power.png",
                    "res://images/powers/language_floor_wolf_howl_passive_power.png",
                    "res://images/powers/language_floor_punish_evil_passive_power.png",
                    "res://images/powers/language_floor_destined_big_bad_wolf_passive_power.png",
                    "res://images/powers/language_floor_hide_in_darkness_passive_power.png",
                    "res://images/powers/language_floor_shadow_ambush_passive_power.png",
                    "res://images/powers/language_floor_exhaustion_passive_power.png",
                    "res://images/powers/language_floor_shadow_wolf_power.png",
                    AttackSfxPath,
                    HowlSfxPath
                ])
                .Concat(EnumerateIntentAssets().SelectMany(static intent => intent.AssetPaths))
                .Distinct();
        }
    }

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        if (!HasPlannedTurn)
        {
            WolfTurnCount = 0;
            PlannedMoveOne = -1;
            PlannedMoveTwo = -1;
            PlannedMoveThree = -1;
            PlannedMoveFour = -1;
            LowHealthMode = false;
            _pendingLowHealthMode = false;
        }

        await PowerCmdCompat.Ensure<LanguageFloorRipOpenClawPassivePower>(
            Creature);
        await PowerCmdCompat.Ensure<LanguageFloorWolfHowlPassivePower>(
            Creature);
        await PowerCmdCompat.Ensure<
            LanguageFloorWolfHowlingNightmarePassivePower>(Creature);
        await PowerCmdCompat.Apply<LibraryOfRuinaFocusOfAttentionPower>(Creature, 1m, Creature, null, true);
        if (Creature.CombatState?.Encounter is LanguageFloorLiberationEncounter encounter)
        {
            await encounter.EnsureControllerPowers(Creature.CombatState);
        }
        
        EncounterBgmController.RegisterMonster(Creature);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        _highCompositeIntents = new AbstractIntent[2];
        _lowCompositeIntents = new AbstractIntent[3];
        RefreshPlannedIntents();
        _highCompositeState = new MoveState(
            HighCompositeMoveId,
            PerformCompositeMove,
            _highCompositeIntents);
        _lowCompositeState = new MoveState(
            LowCompositeMoveId,
            PerformCompositeMove,
            _lowCompositeIntents);
        MoveState reviveAndEmpower = CreateReviveAndEmpowerState();

        var router = new DelegatingMonsterRouterState(
            RouterMoveId,
            (_, rng) =>
            {
                PlanNextTurn(rng);
                return LowHealthMode
                    ? LowCompositeMoveId
                    : HighCompositeMoveId;
            });
        _highCompositeState.FollowUpState = router;
        _lowCompositeState.FollowUpState = router;
        reviveAndEmpower.FollowUpState = router;
        MonsterState initialState = HasPlannedTurn
            ? LowHealthMode ? _lowCompositeState : _highCompositeState
            : router;
        return new MonsterMoveStateMachine(
            [
                reviveAndEmpower,
                _highCompositeState,
                _lowCompositeState,
                router
            ],
            initialState);
    }

    public bool UsesTargetedAttackContract(Creature owner) => true;

    public IReadOnlyList<Creature> GetTargetedAttackTargets(Creature owner)
    {
        return LanguageFloorLiberationCombatHelper.GetPartnerThenPlayerTarget(owner) is { } target
            ? [target]
            : [];
    }

    public string GetTargetedAttackTargetName(Creature owner) =>
        GetTargetedAttackTargets(owner).FirstOrDefault()?.Name ?? "未知目标";

    protected override Task CompleteLiberationPhaseTransition() =>
        Creature.CombatState?.Encounter
            is LanguageFloorLiberationEncounter encounter
            ? encounter.CompletePhaseTransition(this)
            : Task.CompletedTask;

    public override async Task AfterCurrentHpChanged(Creature creature, decimal delta)
    {
        await base.AfterCurrentHpChanged(creature, delta);
        if (creature == Creature
            && delta < 0m
            && !LowHealthMode
            && Creature.CurrentHp <= Math.Ceiling(Creature.MaxHp * 0.5m))
        {
            _pendingLowHealthMode = true;
            
        }
    }

    public override async Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side, IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (_pendingLowHealthMode && combatState.CurrentSide == CombatSide.Player)
        {
            await EnterLowHealthMode();
            _pendingLowHealthMode = false;
        }
    }

    private async Task EnterLowHealthMode()
    {
        if (LowHealthMode || !_pendingLowHealthMode)
        {
            return;
        }
        
        LowHealthMode = true;
        if (_lowCompositeState == null)
        {
            return;
        }

        PlanTurn(RunRng.MonsterAi, incrementTurn: false);
        SetMoveImmediate(_lowCompositeState, forceTransition: true);
        if (NCombatRoom.Instance?.GetCreatureNode(Creature) is { } node)
        {
            await node.RefreshIntents();
        }
    }

    private async Task PerformCompositeMove(IReadOnlyList<Creature> targets)
    {
        int capacity = LowHealthMode
            ? LanguageFloorWolfHowlingNightmarePassivePower.IntentCount
            : LanguageFloorWolfHowlingNightmarePassivePower.InitialIntentCount;
        for (int slot = 0; slot < capacity && Creature.IsAlive; slot++)
        {
            await PerformMove(GetPlannedMove(slot));
            if (Creature.CombatState?.Encounter is LanguageFloorLiberationEncounter { PhaseComplete: true })
            {
                return;
            }
        }
    }

    private async Task PerformMove(LanguageFloorMoveKind move)
    {
        switch (move)
        {
            case LanguageFloorMoveKind.BrutalFangs:
                await ExecuteTargetedHits(GetMoveDamage(move), 1, SlashAnimationTrigger);
                await CreatureCmd.GainBlock(Creature, 10m, ValueProp.Move, null);
                await CreatureCmd.Heal(Creature, (int)Math.Ceiling(Creature.MaxHp * 0.05m));
                break;
            case LanguageFloorMoveKind.HorrifyingClaws:
                await ExecuteTargetedHits(GetMoveDamage(move), 2, SlashAnimationTrigger);
                await CreatureCmd.GainBlock(Creature, 18m, ValueProp.Move, null);
                break;
            case LanguageFloorMoveKind.BloodstainedHunt:
                await ExecuteTargetedHits(
                    GetMoveDamage(move),
                    3,
                    SpecialAnimationTrigger);
                Creature? target = LanguageFloorLiberationCombatHelper.GetPartnerThenPlayerTarget(Creature);
                if (target?.IsAlive == true)
                {
                    await PowerCmdCompat.Apply<LibraryBleedingPower>(target, 6m, Creature, null);
                }

                break;
            case LanguageFloorMoveKind.Howl:
                await ExecuteGroupAttack(GetMoveDamage(move), 1);
                foreach (Creature creature in LanguageFloorLiberationCombatHelper.GetLivingPlayersAndPartner(Creature))
                {
                    await LibraryPowerCmd.Apply<LibraryWeakPower>(
                        creature,
                        HowlWeak,
                        HowlWeakTurns,
                        Creature,
                        null);
                }

                break;
        }
    }

    private async Task ExecuteTargetedHits(int damage, int repeats, string animation)
    {
        for (int i = 0; i < repeats && Creature.IsAlive; i++)
        {
            Creature? target = LanguageFloorLiberationCombatHelper.GetPartnerThenPlayerTarget(Creature);
            if (target == null)
            {
                return;
            }

            LocalOggOneShotPlayer.Play(AttackSfxPath);
            using var forcedTargets = TargetedMonsterAttackHelper.ForceTargets(Creature, [target]);
            AttackCommand command = await DamageCmd.Attack(damage)
                .FromMonster(this)
                .WithAttackerAnim(animation, AbnormalityAnimHelper.DefaultAttackSegmentDelaySeconds)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(null);
            await ApplyScarAndAnger(
                AttackCommandCompat.Results(command),
                LanguageFloorAngerGaugePower.WolfAttackAnger);
        }
    }

    private async Task ExecuteGroupAttack(int damage, int repeats)
    {
        IReadOnlyList<Creature> actualTargets =
            LanguageFloorLiberationCombatHelper.GetLivingPlayersAndPartner(Creature);
        if (actualTargets.Count == 0)
        {
            return;
        }

        for (int i = 0; i < repeats && Creature.IsAlive; i++)
        {
            actualTargets = LanguageFloorLiberationCombatHelper.GetLivingPlayersAndPartner(Creature);
            if (actualTargets.Count == 0)
            {
                return;
            }

            using var forcedTargets = TargetedMonsterAttackHelper.ForceTargets(Creature, actualTargets);
            LocalOggOneShotPlayer.Play(HowlSfxPath);
            AttackCommand command = await DamageCmd.Attack(damage)
                .FromMonster(this)
                .WithAttackerAnim(HowlAnimationTrigger, AbnormalityAnimHelper.DefaultAttackSegmentDelaySeconds)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(null);
            await ApplyScarAndAnger(
                AttackCommandCompat.Results(command),
                LanguageFloorAngerGaugePower.WolfRoarAnger);
        }
    }

    internal async Task ApplyScarAndAnger(
        IReadOnlyList<DamageResult> results,
        int angerPerHitOnScarlet)
    {
        foreach (DamageResult result in results.Where(static result => result.UnblockedDamage > 0))
        {
            if (result.Receiver.Monster is LanguageFloorScarletScar)
            {
                await (result.Receiver.GetPower<LanguageFloorAngerGaugePower>()?.ChangeAnger(angerPerHitOnScarlet)
                    ?? Task.CompletedTask);
            }
        }
    }

    private static int GetMoveDamage(LanguageFloorMoveKind move)
    {
        int normal = move switch
        {
            LanguageFloorMoveKind.BrutalFangs => 8,
            LanguageFloorMoveKind.HorrifyingClaws => 11,
            LanguageFloorMoveKind.BloodstainedHunt => 8,
            LanguageFloorMoveKind.Howl => 17,
            _ => 0
        };
        int ascended = move switch
        {
            LanguageFloorMoveKind.BrutalFangs => 10,
            LanguageFloorMoveKind.HorrifyingClaws => 12,
            LanguageFloorMoveKind.BloodstainedHunt => 9,
            LanguageFloorMoveKind.Howl => 19,
            _ => normal
        };

        return AscensionHelper.GetValueIfAscension(
            AscensionLevel.DeadlyEnemies,
            ascended,
            normal);
    }

    private static AbstractIntent CreateIntent(LanguageFloorMoveKind move)
    {
        int damage = GetMoveDamage(move);
        return move switch
        {
            LanguageFloorMoveKind.BrutalFangs => new CombinedTargetedAttackDefendIntent(
                damage,
                1,
                "LANGUAGE_FLOOR_WOLF_BRUTAL_FANGS.description",
                null,
                false,
                10,
                IntentBadge.Heal()),
            LanguageFloorMoveKind.HorrifyingClaws => new CombinedTargetedAttackDefendIntent(
                damage,
                2,
                "LANGUAGE_FLOOR_WOLF_HORRIFYING_CLAWS.description",
                null,
                false,
                blockAmount: 18),
            LanguageFloorMoveKind.BloodstainedHunt => new CombinedTargetedAttackDebuffIntent(
                damage,
                3,
                "LANGUAGE_FLOOR_WOLF_BLOODSTAINED_HUNT.description",
                null,
                false,
                IntentBadge.Bleed(12)),
            LanguageFloorMoveKind.Howl => new IndiscriminateAttackIntent(
                damage,
                1,
                "LANGUAGE_FLOOR_WOLF_HOWL.description",
                LanguageFloorLiberationCombatHelper.GetLivingPlayersAndPartner,
                IntentBadge.Weak(HowlWeak)),
            _ => throw new ArgumentOutOfRangeException(nameof(move), move, null)
        };
    }

    private void RefreshPlannedIntents()
    {
        if (_highCompositeIntents != null)
        {
            for (int slot = 0; slot < _highCompositeIntents.Length; slot++)
            {
                _highCompositeIntents[slot] = CreateIntent(GetPlannedMove(slot));
            }
        }

        if (_lowCompositeIntents != null)
        {
            for (int slot = 0; slot < _lowCompositeIntents.Length; slot++)
            {
                _lowCompositeIntents[slot] = CreateIntent(GetPlannedMove(slot));
            }
        }
    }

    private static IEnumerable<AbstractIntent> EnumerateIntentAssets()
    {
        yield return CreateIntent(LanguageFloorMoveKind.BrutalFangs);
        yield return CreateIntent(LanguageFloorMoveKind.HorrifyingClaws);
        yield return CreateIntent(LanguageFloorMoveKind.BloodstainedHunt);
        yield return CreateIntent(LanguageFloorMoveKind.Howl);
    }

    internal LanguageFloorMoveKind GetPlannedMove(int slot)
    {
        int value = slot switch
        {
            0 => PlannedMoveOne,
            1 => PlannedMoveTwo,
            2 => PlannedMoveThree,
            _ => PlannedMoveFour
        };
        return Enum.IsDefined(typeof(LanguageFloorMoveKind), value)
            ? (LanguageFloorMoveKind)value
            : LanguageFloorMoveKind.BrutalFangs;
    }

    private void PlanNextTurn(Rng rng)
    {
        PlanTurn(rng, incrementTurn: true);
    }

    private void PlanTurn(Rng rng, bool incrementTurn)
    {
        if (incrementTurn)
        {
            WolfTurnCount++;
        }

        int capacity = LowHealthMode
            ? LanguageFloorWolfHowlingNightmarePassivePower.IntentCount
            : LanguageFloorWolfHowlingNightmarePassivePower.InitialIntentCount;
        bool roar = ShouldUseHowl(WolfTurnCount, LowHealthMode);

        PlannedMoveOne = roar
            ? (int)LanguageFloorMoveKind.Howl
            : (int)NextBaseMove(rng);
        PlannedMoveTwo = (int)NextBaseMove(rng);
        PlannedMoveThree = capacity >= 3 ? (int)NextBaseMove(rng) : -1;
        //PlannedMoveFour = capacity >= 4 ? (int)NextBaseMove(rng) : -1;
        RefreshPlannedIntents();
    }

    internal static bool ShouldUseHowl(int wolfTurnCount, bool lowHealthMode)
    {
        int interval = lowHealthMode
            ? LanguageFloorWolfHowlingNightmarePassivePower.HowlInterval
            : LanguageFloorWolfHowlPassivePower.HowlInterval;
        return wolfTurnCount > 0 && wolfTurnCount % interval == 0;
    }

    internal IReadOnlyList<LanguageFloorMoveKind> PlannedMoves =>
        Enumerable.Range(
                0,
                LowHealthMode
                    ? LanguageFloorWolfHowlingNightmarePassivePower.IntentCount
                    : LanguageFloorWolfHowlingNightmarePassivePower.InitialIntentCount)
            .Select(GetPlannedMove)
            .ToArray();

    internal Task DebugEnterLowHealthMode() => EnterLowHealthMode();

    internal void DebugSetPlan(
        int wolfTurnCount,
        bool lowHealthMode,
        params LanguageFloorMoveKind[] moves)
    {
        WolfTurnCount = wolfTurnCount;
        LowHealthMode = lowHealthMode;
        PlannedMoveOne = moves.Length > 0 ? (int)moves[0] : -1;
        PlannedMoveTwo = moves.Length > 1 ? (int)moves[1] : -1;
        PlannedMoveThree = moves.Length > 2 ? (int)moves[2] : -1;
        PlannedMoveFour = moves.Length > 3 ? (int)moves[3] : -1;
        RefreshPlannedIntents();
    }

    private bool HasPlannedTurn =>
        PlannedMoveOne >= 0
        && PlannedMoveTwo >= 0
        && (!LowHealthMode || PlannedMoveThree >= 0 && PlannedMoveFour >= 0);

    private static LanguageFloorMoveKind NextBaseMove(Rng rng)
    {
        LanguageFloorMoveKind[] candidates =
        [
            LanguageFloorMoveKind.BrutalFangs,
            LanguageFloorMoveKind.HorrifyingClaws,
            LanguageFloorMoveKind.BloodstainedHunt
        ];
        return candidates[rng.NextInt(candidates.Length)];
    }

}
