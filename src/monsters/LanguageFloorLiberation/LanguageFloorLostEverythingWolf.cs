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

    // 规划只写前三个槽位。PlannedMoveFour 不经过控制器：规划从不写它，只有进场重置、DebugSetPlan 和
    // HasPlannedTurn 直接读写。低血量模式下 HasPlannedTurn 要求它非负，所以这时读档总会清空状态重新规划。
    private const int PlanSlotCount = 3;

    private MoveState? _lowCompositeState;
    private PlannedMoveController<LanguageFloorMoveKind>? _plan;

    // 高、低血量两个复合行动共用槽位，分别展示前 2、3 个。读槽位把非法值当作残暴獠牙，执行不会停在空槽位。
    // 实例随怪物克隆丢弃、用到时重建（见 PlannedMoveController）。
    private PlannedMoveController<LanguageFloorMoveKind> Plan => _plan ??= new(
        this,
        PlanSlotCount,
        GetPlannedMove,
        SetPlannedMove,
        static (_, move) => CreateIntent(move),
        (LanguageFloorMoveKind)(-1));

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

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        // 计划控制器的委托捕获的是被克隆的实例，克隆体必须用自己的。
        _plan = null;
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState highCompositeState = Plan.CreateCompositeState(
            HighCompositeMoveId,
            PerformCompositeMove,
            intentCount: LanguageFloorWolfHowlingNightmarePassivePower.InitialIntentCount);
        _lowCompositeState = Plan.CreateCompositeState(
            LowCompositeMoveId,
            PerformCompositeMove,
            intentCount: LanguageFloorWolfHowlingNightmarePassivePower.IntentCount);
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
        highCompositeState.FollowUpState = router;
        _lowCompositeState.FollowUpState = router;
        reviveAndEmpower.FollowUpState = router;
        MonsterState initialState = HasPlannedTurn
            ? LowHealthMode ? _lowCompositeState : highCompositeState
            : router;
        return new MonsterMoveStateMachine(
            [
                reviveAndEmpower,
                highCompositeState,
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
        Plan.Reveal(_lowCompositeState);
        if (NCombatRoom.Instance?.GetCreatureNode(Creature) is { } node)
        {
            await node.RefreshIntents();
        }
    }

    private Task PerformCompositeMove(IReadOnlyList<Creature> targets) =>
        Plan.PerformPlan(
            () => Creature.IsAlive,
            (_, move) => PerformMove(move),
            slotLimit: CurrentIntentCapacity,
            stopAfterMove: () =>
                Creature.CombatState?.Encounter is LanguageFloorLiberationEncounter { PhaseComplete: true });

    private int CurrentIntentCapacity =>
        LowHealthMode
            ? LanguageFloorWolfHowlingNightmarePassivePower.IntentCount
            : LanguageFloorWolfHowlingNightmarePassivePower.InitialIntentCount;

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

    private void SetPlannedMove(int slot, LanguageFloorMoveKind move)
    {
        switch (slot)
        {
            case 0:
                PlannedMoveOne = (int)move;
                break;
            case 1:
                PlannedMoveTwo = (int)move;
                break;
            default:
                PlannedMoveThree = (int)move;
                break;
        }
    }

    private void PlanNextTurn(Rng rng)
    {
        PlanTurn(rng, incrementTurn: true);
    }

    // 按槽位顺序掷骰；嚎叫回合第一个槽位固定为嚎叫，不消耗随机数。高血量模式第三个槽位写空。
    private void PlanTurn(Rng rng, bool incrementTurn)
    {
        if (incrementTurn)
        {
            WolfTurnCount++;
        }

        bool roar = ShouldUseHowl(WolfTurnCount, LowHealthMode);
        Plan.WriteSlots(
            Math.Min(CurrentIntentCapacity, PlanSlotCount),
            slot => slot == 0 && roar
                ? LanguageFloorMoveKind.Howl
                : NextBaseMove(rng));
        Plan.RefreshIntents();
    }

    internal static bool ShouldUseHowl(int wolfTurnCount, bool lowHealthMode)
    {
        int interval = lowHealthMode
            ? LanguageFloorWolfHowlingNightmarePassivePower.HowlInterval
            : LanguageFloorWolfHowlPassivePower.HowlInterval;
        return wolfTurnCount > 0 && wolfTurnCount % interval == 0;
    }

    internal IReadOnlyList<LanguageFloorMoveKind> PlannedMoves =>
        Enumerable.Range(0, CurrentIntentCapacity)
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
        Plan.RefreshIntents();
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
