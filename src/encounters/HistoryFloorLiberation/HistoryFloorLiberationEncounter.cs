using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.audio;
using LibraryOfRuina.backgrounds.HistoryFloorLiberation;
using LibraryOfRuina.cards;
using LibraryOfRuina.cards.TechnologyFloorLiberation;
using LibraryOfRuina.compat;
using LibraryOfRuina.events.HistoryFloorLiberation;
using LibraryOfRuina.features.moontext;
using LibraryOfRuina.features.secondascension;
using LibraryOfRuina.monsters.HistoryFloorLiberation;
using LibraryOfRuina.powers.HistoryFloorLiberation;
using LibraryOfRuina.scene_transitions;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using LibraryOfRuina.interop;

namespace LibraryOfRuina.encounters.HistoryFloorLiberation;

public sealed class HistoryFloorLiberationEncounter :
    LiberationEncounterBase,
    IEncounterBgmSource,
    ILiberationPhaseBgmSource,
    IFloorLiberationEncounter,
    ISporeWorkerSpawner
{
    EncounterBgmConfig IEncounterBgmSource.Bgm => EncounterBgmConfig.PhaseBased(
        "AngelaLiberationBGM",
        HistoryFloorLiberationEncounter.AngelaLiberationBgmTracks,
        volumeScale: 0.85f);

    internal const string CenterSlot = "malkuth";
    internal const string MatchOneSlot = "match_1";
    internal const string MatchTwoSlot = "match_2";
    internal const string MatchThreeSlot = "match_3";
    internal const string MatchFourSlot = "match_4";
    internal const string EndLightSlot = "end_light";
    internal const string FlutteringBossSlot = "fluttering_boss";
    internal const string WaspBossSlot = "wasp_boss";
    internal const string WorkerBeeSlotThree = "worker_bee_3";
    internal const string WorkerBeeSlotFour = "worker_bee_4";
    internal const string EmeraldBoughSlot = "emerald_bough";
    internal const string VineBarrierSlotOne = "vine_barrier_1";
    internal const string VineBarrierSlotTwo = "vine_barrier_2";
    internal const string FlutteringEncounterScenePath = "res://scenes/encounters/history_floor_liberation_fluttering_encounter.tscn";
    internal const string WaspEncounterScenePath = "res://scenes/encounters/history_floor_liberation_wasp_encounter.tscn";
    internal const string EmeraldBoughEncounterScenePath = "res://scenes/encounters/history_floor_liberation_emerald_bough_encounter.tscn";
    private const float PhaseOneCameraScaling = 0.82f;
    private const string CurrentPhaseKey = "CurrentPhase";
    private const string KilledBossCountKey = "KilledBossCount";
    private const string TransitionPendingKey = "TransitionPending";
    private const string SettlementTriggeredKey = "SettlementTriggered";
    private const string EndedByLethalDamageKey = "EndedByLethalDamage";
    private const float PhaseTransitionAfterTurnWaitSeconds = 0.75f;
    internal const int MaxPhase = 5;
    internal static readonly string[] AngelaLiberationBgmTracks =
    [
        "res://audio/bgm/angela_liberation/angela_liberation_phase_1.ogg",
        "res://audio/bgm/angela_liberation/angela_liberation_phase_2.ogg",
        "res://audio/bgm/angela_liberation/angela_liberation_phase_3.ogg"
    ];
    private static readonly Vector2 PhaseOneCameraOffset = Vector2.Down * 50f + Vector2.Left * 100f;

    public override float GetCameraScaling() =>
        _currentPhase <= 1 ? PhaseOneCameraScaling : 1f;

    public override Vector2 GetCameraOffset() =>
        _currentPhase <= 1 ? PhaseOneCameraOffset : Vector2.Zero;

    private int _currentPhase = 1;
    private int _killedBossCount;
    private bool _transitionPending;
    private bool _settlementTriggered;
    private bool _endedByLethalDamage;

    public override RoomType RoomType => RoomType.Boss;

    public override bool ShouldGiveRewards => false;

    public override bool HasScene => true;

    public override IReadOnlyList<string> Slots => _currentPhase switch
    {
        <= 1 => [MatchOneSlot, MatchTwoSlot, MatchThreeSlot, MatchFourSlot, EndLightSlot, CenterSlot],
        3 => [MatchThreeSlot, MatchFourSlot, FlutteringBossSlot, CenterSlot],
        4 => [WorkerBeeSlotThree, WorkerBeeSlotFour, WaspBossSlot, CenterSlot],
        5 => [VineBarrierSlotOne, VineBarrierSlotTwo, EmeraldBoughSlot, CenterSlot],
        _ => [CenterSlot]
    };

    protected override bool HasCustomBackground => true;

    public override MegaSkeletonDataResource? BossNodeSpineResource => null;

    public override string BossNodePath => "res://images/map/placeholder/history_floor_liberation_encounter_icon";

    public int CurrentPhase => _currentPhase;

    public bool CanSpawnSporeWorkers =>
        _currentPhase == 4 && !_settlementTriggered;

    public int KilledBossCount => _killedBossCount;

    public bool SettlementTriggered => _settlementTriggered;

    public bool EndedByLethalDamage => _endedByLethalDamage;

    public string LiberationFloorId =>
        LiberationFloorIds.History;

    public bool IsFullyLiberated => _killedBossCount >= MaxPhase;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<HistoryFloorPhaseBoss>(),
        ModelDb.Monster<HistoryFloorEndLightBoss>(),
        ModelDb.Monster<HistoryFloorForgottenBoss>(),
        ModelDb.Monster<HistoryFloorFlutteringBoss>(),
        ModelDb.Monster<HistoryFloorFlutteringMass>(),
        ModelDb.Monster<HistoryFloorWaspBoss>(),
        ModelDb.Monster<HistoryFloorWorkerBee>(),
        ModelDb.Monster<HistoryFloorLastMatch>(),
        ModelDb.Monster<HistoryFloorEmeraldBoughBoss>(),
        ModelDb.Monster<HistoryFloorVineBarrier>()
    ];

    public override IEnumerable<string> ExtraAssetPaths =>
        ModelDb.Monster<HistoryFloorPhaseBoss>().AssetPaths
            .Concat(ModelDb.Monster<HistoryFloorEndLightBoss>().AssetPaths)
            .Concat(LorexSceneTransitionAssetPaths.All)
            .Concat(ModelDb.Monster<HistoryFloorForgottenBoss>().AssetPaths)
            .Concat(ModelDb.Monster<HistoryFloorFlutteringBoss>().AssetPaths)
            .Concat(ModelDb.Monster<HistoryFloorFlutteringMass>().AssetPaths)
            .Concat(ModelDb.Monster<HistoryFloorWaspBoss>().AssetPaths)
            .Concat(ModelDb.Monster<HistoryFloorWorkerBee>().AssetPaths)
            .Concat(ModelDb.Monster<HistoryFloorLastMatch>().AssetPaths)
            .Concat(ModelDb.Monster<HistoryFloorEmeraldBoughBoss>().AssetPaths)
            .Concat(ModelDb.Monster<HistoryFloorVineBarrier>().AssetPaths)
            .Concat(new[]
            {
                ImageHelper.GetImagePath("powers/history_floor_corrosion_power.png"),
                ImageHelper.GetImagePath("powers/history_floor_rekindled_spark_power.png"),
                ImageHelper.GetImagePath("powers/forgotten_affection_power.png"),
                ImageHelper.GetImagePath("powers/forgotten_affection_attack_power.png"),
                ImageHelper.GetImagePath("powers/forgotten_longing_embrace_power.png"),
                ImageHelper.GetImagePath("powers/fluttering_momentary_satiety_power.png"),
                ImageHelper.GetImagePath("powers/fluttering_hunger_power.png"),
                ImageHelper.GetImagePath("powers/fluttering_hunger_frenzy_power.png"),
                ImageHelper.GetImagePath("powers/fluttering_fresh_meat_passive_power.png"),
                ImageHelper.GetImagePath("powers/fluttering_fresh_meat_power.png"),
                ImageHelper.GetImagePath("powers/fluttering_mass_care_power.png"),
                ImageHelper.GetImagePath("powers/history_floor_wasp_spore_power.png"),
                ImageHelper.GetImagePath("powers/history_floor_wasp_paralysis_power.png"),
                ImageHelper.GetImagePath("powers/history_floor_wasp_expansion_power.png"),
                ImageHelper.GetImagePath("powers/history_floor_wasp_pheromone_power.png"),
                ImageHelper.GetImagePath("powers/emerald_bough_vine_barrier_power.png"),
                ImageHelper.GetImagePath("powers/emerald_bough_forest_apple_power.png"),
                ImageHelper.GetImagePath("powers/emerald_bough_where_are_you_power.png"),
                ImageHelper.GetImagePath("powers/emerald_bough_strangling_vine_power.png"),
                HistoryFloorLiberationBackgroundController.PhaseOneTexturePath,
                HistoryFloorLiberationBackgroundController.ForgottenTexturePath,
                HistoryFloorLiberationBackgroundController.FlutteringTexturePath,
                HistoryFloorLiberationBackgroundController.FlutteringStarvedTexturePath,
                HistoryFloorLiberationBackgroundController.FlutteringPredationOverlayTexturePath,
                HistoryFloorLiberationBackgroundController.WaspTexturePath,
                HistoryFloorLiberationBackgroundController.EmeraldBoughTexturePath,
                FlutteringEncounterScenePath,
                WaspEncounterScenePath,
                EmeraldBoughEncounterScenePath,
                ImageHelper.GetImagePath(EgoCardBase.GetPortraitResourcePath<PunishmentStrikeEgoCard>()),
                ImageHelper.GetImagePath(EgoCardBase.GetPortraitResourcePath<ShatteredLifeEgoCard>()),
                ImageHelper.GetImagePath("ui/run_history/history_floor_liberation_encounter.png"),
                ImageHelper.GetImagePath("ui/run_history/history_floor_liberation_encounter_outline.png"),
                BossNodePath + ".png",
                BossNodePath + "_outline.png"
            })
            .Concat(AngelaLiberationBgmTracks)
            .Distinct();

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        if (_currentPhase == 1)
        {
            return
            [
                (CreateLastMatch(0), MatchOneSlot),
                (CreateLastMatch(1), MatchTwoSlot),
                (CreateLastMatch(2), MatchThreeSlot),
                (CreateLastMatch(3), MatchFourSlot),
                (CreateEndLightBoss(), EndLightSlot)
            ];
        }

        if (_currentPhase == 3)
        {
            return
            [
                (CreateFlutteringMass(HistoryFloorFlutteringMassPattern.GluttonyFirst), MatchThreeSlot),
                (CreateFlutteringMass(HistoryFloorFlutteringMassPattern.WingbeatFirst), MatchFourSlot),
                (CreateFlutteringBoss(), FlutteringBossSlot)
            ];
        }

        if (_currentPhase == 4)
        {
            return
            [
                (CreateWorkerBee(2), WorkerBeeSlotThree),
                (CreateWorkerBee(3), WorkerBeeSlotFour),
                (CreateWaspBoss(), WaspBossSlot)
            ];
        }

        if (_currentPhase == 5)
        {
            return [(CreateEmeraldBoughBoss(), EmeraldBoughSlot)];
        }

        return [(CreatePhaseBoss(_currentPhase), CenterSlot)];
    }

    public override Dictionary<string, string> SaveCustomState()
    {
        return new Dictionary<string, string>
        {
            [CurrentPhaseKey] = _currentPhase.ToString(),
            [KilledBossCountKey] = _killedBossCount.ToString(),
            [TransitionPendingKey] = _transitionPending.ToString(),
            [SettlementTriggeredKey] = _settlementTriggered.ToString(),
            [EndedByLethalDamageKey] = _endedByLethalDamage.ToString()
        };
    }

    public override void LoadCustomState(Dictionary<string, string> state)
    {
        var bag = new EncounterStateBag(state);
        _currentPhase = bag.ReadClampedInt(CurrentPhaseKey, 1, 1, MaxPhase);
        _killedBossCount = bag.ReadClampedInt(KilledBossCountKey, Math.Max(0, _currentPhase - 1), 0, MaxPhase);
        _transitionPending = bag.ReadBool(TransitionPendingKey);
        _settlementTriggered = bag.ReadBool(SettlementTriggeredKey);
        _endedByLethalDamage = bag.ReadBool(EndedByLethalDamageKey);
    }

    public async Task EnsureControllerPowers(CombatStateLike? combatState)
    {
        if (combatState == null)
        {
            return;
        }

        foreach (Creature player in combatState.PlayerCreatures)
        {
            await PowerCmdCompat.Ensure<
                HistoryFloorLiberationControllerPower>(player);
        }
    }

    public void MarkBossPhaseStarted(int phase)
    {
        if (phase == _currentPhase)
        {
            HistoryFloorLiberationSettlementStore.Record(this);
        }

    }

    internal async Task OnPhaseBossDeath(
        ILiberationPrimaryPhaseBoss boss,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (wasRemovalPrevented
            || _settlementTriggered
            || boss.LiberationPhase != _currentPhase)
        {
            return;
        }

        _killedBossCount = Math.Max(_killedBossCount, boss.LiberationPhase);
        StopPhaseBackgroundMoonTextLoop(boss.Creature);
        if (boss.Creature.CombatState is { } phaseCombatState)
        {
            await ClearEndedPhaseState(boss.LiberationPhase, phaseCombatState);
        }

        if (_currentPhase >= MaxPhase)
        {
            _settlementTriggered = true;
            _endedByLethalDamage = false;
            _transitionPending = false;
            HistoryFloorLiberationSettlementStore.Record(this);
            return;
        }

        _currentPhase++;
        RefreshLiberationPhaseBgm();
        _transitionPending = true;
        HistoryFloorLiberationSettlementStore.Record(this);
        if (boss.Creature.CombatState is { } combatState)
        {
            await RemoveOtherPhaseCreatures(combatState, boss.Creature);
            if (!LibrarySecondAscensionState.SkipsLiberationPhaseRecovery(combatState.RunState))
            {
                await RestorePlayersForPhaseTransition(combatState);
            }
        }

        await LiberationPhaseTransition.ShowAsync(
            boss,
            triggerAnimation: true);
        await Cmd.CustomScaledWait(Math.Max(0.15f, deathAnimLength), Math.Max(0.3f, deathAnimLength));
    }

    public bool ShouldPreventPlayerDeath(Creature creature)
    {
        return creature.IsPlayer
            && !_settlementTriggered
            && _killedBossCount >= 2
            && IsLastAlivePlayer(creature);
    }

    public bool ShouldPreventTransitionBossDeath(Creature creature)
    {
        return _transitionPending
            && !_settlementTriggered
            && creature.Monster is ILiberationPrimaryPhaseBoss boss
            && boss.LiberationPhase + 1 == _currentPhase;
    }

    public async Task OnPreventingPlayerDeath(Creature creature)
    {
        if (!ShouldPreventPlayerDeath(creature))
        {
            return;
        }

        _settlementTriggered = true;
        _endedByLethalDamage = true;
        _transitionPending = false;
        StopHistoryFloorBackgroundMoonTextLoops(creature.CombatState);
        HistoryFloorLiberationSettlementStore.Record(this);

        await CreatureCmd.SetCurrentHp(creature, 1m);
        // 历史层一直没有“战斗已结束或正在结束则直接返回”的守卫（艺术、技术、语言、文学层有），这里保持原行为。
        await EndCombatAsLiberationVictory(
            creature.CombatState,
            requireCombatInProgress: false);
    }

    public async Task OnBeforeSideTurnStart(CombatSide side, CombatStateLike combatState)
    {
        await EnsureControllerPowers(combatState);
        await ClearStalePhasePlayerPowers(combatState);

        if (side == CombatSide.Enemy && _transitionPending && !_settlementTriggered
            && !combatState.Enemies.Any(static e =>
                e.Monster is ILiberationPhaseBoss))
        {
            await SpawnNextPhaseFromTransition(combatState);
        }
    }

    public bool ShouldKeepCombatOpen(CombatStateLike combatState) =>
        LiberationCombatEndGuard.ShouldKeepCombatOpen(
            combatState,
            _currentPhase,
            _transitionPending,
            _settlementTriggered);

    public bool ShouldKeepPhaseBossAfterDeath(Creature creature)
    {
        if (_settlementTriggered)
            return false;
        if (creature.Monster is not ILiberationPrimaryPhaseBoss boss)
            return false;
        if (boss.LiberationPhase >= MaxPhase)
            return false;
        return boss.LiberationPhase == _currentPhase
            || (_transitionPending && boss.LiberationPhase + 1 == _currentPhase);
    }

    public bool ShouldSuppressTransitionBossInteraction(Creature creature)
    {
        return _transitionPending
            && !_settlementTriggered
            && creature.IsDead
            && creature.Monster is ILiberationPhaseBoss boss
            && boss.LiberationPhase + 1 == _currentPhase;
    }

    public static MonsterModel CreatePhaseBoss(int phase)
    {
        if (phase == 2)
        {
            return ModelDb.Monster<HistoryFloorForgottenBoss>().ToMutable();
        }

        if (phase == 3)
        {
            return ModelDb.Monster<HistoryFloorFlutteringBoss>().ToMutable();
        }

        if (phase == 4)
        {
            return ModelDb.Monster<HistoryFloorWaspBoss>().ToMutable();
        }

        if (phase == 5)
        {
            return ModelDb.Monster<HistoryFloorEmeraldBoughBoss>().ToMutable();
        }

        var boss = (HistoryFloorPhaseBoss)ModelDb.Monster<HistoryFloorPhaseBoss>().ToMutable();
        boss.ConfigurePhase(phase);
        return boss;
    }

    private static MonsterModel CreateEndLightBoss() =>
        ModelDb.Monster<HistoryFloorEndLightBoss>().ToMutable();

    private static MonsterModel CreateLastMatch(int patternIndex)
    {
        var match = (HistoryFloorLastMatch)ModelDb.Monster<HistoryFloorLastMatch>().ToMutable();
        match.ConfigurePattern(patternIndex);
        return match;
    }

    private static MonsterModel CreateFlutteringBoss() =>
        ModelDb.Monster<HistoryFloorFlutteringBoss>().ToMutable();

    private static MonsterModel CreateWaspBoss() =>
        ModelDb.Monster<HistoryFloorWaspBoss>().ToMutable();

    private static MonsterModel CreateEmeraldBoughBoss() =>
        ModelDb.Monster<HistoryFloorEmeraldBoughBoss>().ToMutable();

    private static MonsterModel CreateVineBarrier() =>
        ModelDb.Monster<HistoryFloorVineBarrier>().ToMutable();

    internal static Control InstantiateFlutteringEncounterScene() =>
        PreloadManager.Cache.GetScene(FlutteringEncounterScenePath)
            .Instantiate<Control>();

    internal static Control InstantiateWaspEncounterScene() =>
        PreloadManager.Cache.GetScene(WaspEncounterScenePath)
            .Instantiate<Control>();

    internal static Control InstantiateEmeraldBoughEncounterScene() =>
        PreloadManager.Cache.GetScene(EmeraldBoughEncounterScenePath)
            .Instantiate<Control>();

    private static MonsterModel CreateFlutteringMass(HistoryFloorFlutteringMassPattern pattern)
    {
        var mass = (HistoryFloorFlutteringMass)ModelDb.Monster<HistoryFloorFlutteringMass>().ToMutable();
        mass.ConfigurePattern(pattern);
        return mass;
    }

    private static MonsterModel CreateWorkerBee(int startIndex)
    {
        var bee = (HistoryFloorWorkerBee)ModelDb.Monster<HistoryFloorWorkerBee>().ToMutable();
        bee.ConfigurePattern(startIndex);
        return bee;
    }

    private async Task SpawnNextPhase(CombatStateLike combatState)
    {
        TextureRect? backgroundImage = HistoryFloorLiberationBackgroundController.GetCurrentBackgroundImage();
        StopHistoryFloorBackgroundMoonTextLoops(combatState);
        _transitionPending = false;

        if (!CombatManager.Instance.IsInProgress)
        {
            return;
        }

        if (combatState.Enemies.Any(static enemy => enemy.IsAlive))
        {
            return;
        }
        if (_currentPhase >= 2 && NCombatRoom.Instance is {} room)
        {
            if (!room.SceneContainer.Scale.IsEqualApprox(Vector2.One))
            {
                room.SceneContainer.Scale = Vector2.One;
                room.SceneContainer.Position -= PhaseOneCameraOffset;
            }

            if (_currentPhase == 3)
            {
                ReplaceEncounterSceneForFlutteringPhase(room);
            }
            else if (_currentPhase == 4)
            {
                ReplaceEncounterSceneForWaspPhase(room);
            }
            else if (_currentPhase == 5)
            {
                ReplaceEncounterSceneForEmeraldBoughPhase(room);
            }
        }

        await LorexSceneTransitionController.PlayPhaseBackgroundRevealAsync(
            backgroundImage,
            HistoryFloorLiberationBackgroundController.GetPhaseBackgroundTexturePath(_currentPhase),
            SpawnCurrentPhaseCreatures,
            () => HistoryFloorLiberationBackgroundController.SetPhaseBackground(_currentPhase));
        return;

        async Task SpawnCurrentPhaseCreatures()
        {
        if (_currentPhase == 3)
        {
            // Boss必须先生成，小怪（带MinionPower）依赖boss存在
            Creature flutteringBoss = await CreatureCmd.Add(CreateFlutteringBoss(), combatState, CombatSide.Enemy, FlutteringBossSlot);
            flutteringBoss.PrepareForNextTurn(combatState.PlayerCreatures);

            IReadOnlyList<(MonsterModel Monster, string Slot)> masses =
            [
                (CreateFlutteringMass(HistoryFloorFlutteringMassPattern.GluttonyFirst), MatchThreeSlot),
                (CreateFlutteringMass(HistoryFloorFlutteringMassPattern.WingbeatFirst), MatchFourSlot)
            ];

            foreach ((MonsterModel monster, string slot) in masses)
            {
                Creature spawnedCreature = await CreatureCmd.Add(monster, combatState, CombatSide.Enemy, slot);
                spawnedCreature.PrepareForNextTurn(combatState.PlayerCreatures);
            }
        }
        else if (_currentPhase == 4)
        {
            // Boss必须先生成，小怪（带MinionPower）依赖boss存在
            Creature waspBoss = await CreatureCmd.Add(CreateWaspBoss(), combatState, CombatSide.Enemy, WaspBossSlot);
            waspBoss.PrepareForNextTurn(combatState.PlayerCreatures);

            IReadOnlyList<(MonsterModel Monster, string Slot)> bees =
            [
                (CreateWorkerBee(2), WorkerBeeSlotThree),
                (CreateWorkerBee(3), WorkerBeeSlotFour)
            ];

            foreach ((MonsterModel monster, string slot) in bees)
            {
                Creature spawnedCreature = await CreatureCmd.Add(monster, combatState, CombatSide.Enemy, slot);
                spawnedCreature.PrepareForNextTurn(combatState.PlayerCreatures);
            }
        }
        else if (_currentPhase == 5)
        {
            Creature emeraldBoughCreature = await CreatureCmd.Add(CreateEmeraldBoughBoss(), combatState, CombatSide.Enemy, EmeraldBoughSlot);
            emeraldBoughCreature.PrepareForNextTurn(combatState.PlayerCreatures);
        }
        else
        {
            Creature phaseCreature = await CreatureCmd.Add(CreatePhaseBoss(_currentPhase), combatState, CombatSide.Enemy, CenterSlot);
            phaseCreature.PrepareForNextTurn(combatState.PlayerCreatures);
        }

        combatState.SortEnemiesBySlotName();
        HistoryFloorLiberationSettlementStore.Record(this);
        }
    }

    private static void ReplaceEncounterSceneForFlutteringPhase(NCombatRoom room)
    {
        if (!VanillaPrivate.CombatRoomEncounterSlots.IsAvailable)
        {
            throw new InvalidOperationException("NCombatRoom.EncounterSlots could not be found.");
        }

        Control? existingSlots = VanillaPrivate.CombatRoomEncounterSlots.Get(room) as Control;
        if (existingSlots?.GetNodeOrNull<Marker2D>(FlutteringBossSlot) != null
            && existingSlots.GetNodeOrNull<Marker2D>(EndLightSlot) == null)
        {
            return;
        }

        Control parent = existingSlots?.GetParent() as Control ?? room.GetNode<Control>("%EnemyContainer");
        if (existingSlots != null && GodotObject.IsInstanceValid(existingSlots))
        {
            parent.RemoveChildSafely(existingSlots);
            existingSlots.QueueFreeSafely();
        }

        Control flutteringSlots = InstantiateFlutteringEncounterScene();
        flutteringSlots.Position -= new Vector2(1920f, 1080f) * 0.5f;
        parent.AddChildSafely(flutteringSlots);
        VanillaPrivate.CombatRoomEncounterSlots.Set(room, flutteringSlots);
    }

    private static void ReplaceEncounterSceneForWaspPhase(NCombatRoom room)
    {
        if (!VanillaPrivate.CombatRoomEncounterSlots.IsAvailable)
        {
            throw new InvalidOperationException("NCombatRoom.EncounterSlots could not be found.");
        }

        Control? existingSlots = VanillaPrivate.CombatRoomEncounterSlots.Get(room) as Control;
        if (existingSlots?.GetNodeOrNull<Marker2D>(WorkerBeeSlotThree) != null
            && existingSlots.GetNodeOrNull<Marker2D>(FlutteringBossSlot) == null)
        {
            return;
        }

        Control parent = existingSlots?.GetParent() as Control ?? room.GetNode<Control>("%EnemyContainer");
        if (existingSlots != null && GodotObject.IsInstanceValid(existingSlots))
        {
            parent.RemoveChildSafely(existingSlots);
            existingSlots.QueueFreeSafely();
        }

        Control waspSlots = InstantiateWaspEncounterScene();
        waspSlots.Position -= new Vector2(1920f, 1080f) * 0.5f;
        parent.AddChildSafely(waspSlots);
        VanillaPrivate.CombatRoomEncounterSlots.Set(room, waspSlots);
    }

    internal static void ReplaceEncounterSceneForEmeraldBoughPhase(NCombatRoom room)
    {
        if (!VanillaPrivate.CombatRoomEncounterSlots.IsAvailable)
        {
            throw new InvalidOperationException("NCombatRoom.EncounterSlots could not be found.");
        }

        Control? existingSlots = VanillaPrivate.CombatRoomEncounterSlots.Get(room) as Control;
        if (existingSlots?.GetNodeOrNull<Marker2D>(EmeraldBoughSlot) != null
            && existingSlots.GetNodeOrNull<Marker2D>(WaspBossSlot) == null)
        {
            return;
        }

        Control parent = existingSlots?.GetParent() as Control ?? room.GetNode<Control>("%EnemyContainer");
        if (existingSlots != null && GodotObject.IsInstanceValid(existingSlots))
        {
            parent.RemoveChildSafely(existingSlots);
            existingSlots.QueueFreeSafely();
        }

        Control emeraldBoughSlots = InstantiateEmeraldBoughEncounterScene();
        emeraldBoughSlots.Position -= new Vector2(1920f, 1080f) * 0.5f;
        parent.AddChildSafely(emeraldBoughSlots);
        VanillaPrivate.CombatRoomEncounterSlots.Set(room, emeraldBoughSlots);
    }

    internal async Task CompletePhaseTransition(ILiberationPhaseBoss boss)
    {
        if (!_transitionPending
            || _settlementTriggered
            || boss.LiberationPhase >= MaxPhase
            || boss.LiberationPhase + 1 != _currentPhase)
        {
            return;
        }

        CombatStateLike? combatState = boss.Creature.CombatState;
        if (combatState == null)
        {
            return;
        }

        await LiberationPhaseCleanup.RemovePhaseCreatures(
            combatState,
            beforeRemove: StopPhaseBackgroundMoonTextLoop);
        await SpawnNextPhase(combatState);
    }

    private async Task SpawnNextPhaseFromTransition(CombatStateLike combatState)
    {
        await Cmd.CustomScaledWait(PhaseTransitionAfterTurnWaitSeconds, PhaseTransitionAfterTurnWaitSeconds);

        await LiberationPhaseCleanup.RemovePhaseCreatures(
            combatState,
            beforeRemove: StopPhaseBackgroundMoonTextLoop);
        await SpawnNextPhase(combatState);
    }

    private static async Task RemoveOtherPhaseCreatures(CombatStateLike combatState, Creature phaseBoss)
    {
        await LiberationPhaseCleanup.RemovePhaseCreatures(
            combatState,
            except: phaseBoss,
            includeDeadStateCreatures: false,
            beforeRemove: StopPhaseBackgroundMoonTextLoop);
    }

    private static async Task RestorePlayersForPhaseTransition(CombatStateLike combatState)
    {
        foreach (Creature playerCreature in combatState.PlayerCreatures)
        {
            bool wasDead = playerCreature.IsDead;
            await CreatureCmd.Heal(playerCreature, 10m);

            if (wasDead && playerCreature.IsAlive)
            {
                await RebuildRevivedPlayerDrawPile(combatState, playerCreature);
            }
        }
    }

    private static async Task RebuildRevivedPlayerDrawPile(CombatStateLike combatState, Creature playerCreature)
    {
        if (playerCreature.Player?.PlayerCombatState is not { } playerCombatState
            || playerCombatState.AllCards.Any())
        {
            return;
        }

        List<CardModel> combatCards = [];
        foreach (CardModel deckCard in playerCreature.Player.Deck.Cards.ToList())
        {
            CardModel combatCard = combatState.CloneCard(deckCard);
            combatCard.DeckVersion = deckCard;
            combatCards.Add(combatCard);
        }

        combatCards.StableShuffle(playerCreature.Player.RunState.Rng.Shuffle);

        foreach (CardModel combatCard in combatCards)
        {
            CardPileAddResult result = await CardPileCmd.Add(combatCard, PileType.Draw, CardPilePosition.Bottom, null, true);
            if (result.success)
            {
                combatCard.Pile?.InvokeCardAddFinished();
            }
        }
    }

    private static async Task ClearEndedPhaseState(int phase, CombatStateLike combatState)
    {
        switch (phase)
        {
            case 2:
                await ClearPlayerPower<ForgottenAffectionPower>(combatState);
                break;
            case 3:
                await ClearPlayerPower<FlutteringFreshMeatPower>(combatState);
                break;
            case 4:
                await ClearWaspPhaseState(combatState);
                break;
        }
    }

    private async Task ClearStalePhasePlayerPowers(CombatStateLike combatState)
    {
        if (_currentPhase != 2)
        {
            await ClearPlayerPower<ForgottenAffectionPower>(combatState);
        }

        if (_currentPhase != 3)
        {
            await ClearPlayerPower<FlutteringFreshMeatPower>(combatState);
        }

        if (_currentPhase != 4)
        {
            await ClearPlayerPower<HistoryFloorWaspSporePower>(combatState);
        }
    }

    private static void StopHistoryFloorBackgroundMoonTextLoops(CombatStateLike? combatState)
    {
        if (combatState == null)
        {
            return;
        }

        foreach (Creature enemy in combatState.Enemies)
        {
            StopPhaseBackgroundMoonTextLoop(enemy);
        }
    }

    private static void StopPhaseBackgroundMoonTextLoop(Creature creature)
    {
        if (creature.Monster is HistoryFloorEndLightBoss endLight)
        {
            endLight.StopBackgroundMoonTextLoop();
            return;
        }

        if (creature.Monster is HistoryFloorForgottenBoss forgotten)
        {
            forgotten.StopBackgroundMoonTextLoop();
            return;
        }

        if (creature.Monster is HistoryFloorFlutteringBoss fluttering)
        {
            fluttering.StopBackgroundMoonTextLoop();
            return;
        }

        if (creature.Monster is HistoryFloorWaspBoss wasp)
        {
            wasp.StopBackgroundMoonTextLoop();
            return;
        }

        if (creature.Monster is HistoryFloorEmeraldBoughBoss emeraldBough)
        {
            emeraldBough.StopBackgroundMoonTextLoop();
            return;
        }

        MoonTextService.StopRandomLoop(creature, HistoryFloorEndLightBoss.BackgroundTextScope);
        MoonTextService.StopRandomLoop(creature, HistoryFloorForgottenBoss.BackgroundTextScope);
        MoonTextService.StopRandomLoop(creature, HistoryFloorFlutteringBoss.BackgroundTextScope);
        MoonTextService.StopRandomLoop(creature, HistoryFloorWaspBoss.BackgroundTextScope);
        MoonTextService.StopRandomLoop(creature, HistoryFloorEmeraldBoughBoss.BackgroundTextScope);
    }

    internal async Task TrySpawnEmeraldBoughVineBarriers(CombatStateLike combatState)
    {
        if (_currentPhase != 5 || _settlementTriggered)
        {
            return;
        }

        string[] vineSlots = [VineBarrierSlotOne, VineBarrierSlotTwo];

        foreach (string slot in vineSlots)
        {
            if (combatState.Enemies.Any(enemy => enemy.SlotName == slot && enemy.IsAlive))
            {
                continue;
            }

            HistoryFloorVineBarrier barrier = (HistoryFloorVineBarrier)ModelDb.Monster<HistoryFloorVineBarrier>().ToMutable();
            Creature spawned = await CreatureCmd.Add(barrier, combatState, CombatSide.Enemy, slot);
            spawned.PrepareForNextTurn(combatState.PlayerCreatures);
        }

        combatState.SortEnemiesBySlotName();
    }

    internal async Task TrySpawnWorkerBeeFromSpore(CombatStateLike combatState)
    {
        if (_currentPhase != 4 || _settlementTriggered)
        {
            await ClearWaspPhaseState(combatState);
            return;
        }

        int workerCount = combatState.Enemies.Count(static enemy => enemy.IsAlive && enemy.Monster is HistoryFloorWorkerBee);
        if (workerCount >= 2)
        {
            return;
        }

        string[] workerSlots =
        [
            WorkerBeeSlotThree,
            WorkerBeeSlotFour
        ];

        string? slot = workerSlots.FirstOrDefault(slotName => combatState.Enemies.All(enemy => enemy.SlotName != slotName || !enemy.IsAlive));
        if (string.IsNullOrWhiteSpace(slot))
        {
            slot = GetNextSlot(combatState);
        }

        if (string.IsNullOrWhiteSpace(slot))
        {
            return;
        }

        var bee = (HistoryFloorWorkerBee)ModelDb.Monster<HistoryFloorWorkerBee>().ToMutable();
        bee.ConfigurePattern(workerCount);
        Creature spawned = await CreatureCmd.Add(bee, combatState, CombatSide.Enemy, slot);
        spawned.PrepareForNextTurn(combatState.PlayerCreatures);
        combatState.SortEnemiesBySlotName();
        LocalOggOneShotPlayer.Play(HistoryFloorWorkerBee.SporeSfxPath, -2f);
    }

    public Task TrySpawnSporeWorker(CombatStateLike combatState)
    {
        return TrySpawnWorkerBeeFromSpore(combatState);
    }

    private static async Task ClearWaspPhaseState(CombatStateLike combatState)
    {
        await ClearPlayerPower<HistoryFloorWaspSporePower>(combatState);

        foreach (Creature enemy in combatState.Enemies.ToArray())
        {
            if (enemy.Monster is HistoryFloorWorkerBee)
            {
                await LiberationPhaseCleanup.RemoveTransitionCreature(
                    enemy,
                    combatState,
                    StopPhaseBackgroundMoonTextLoop);
            }
        }
    }

    private static async Task ClearPlayerPower<TPower>(CombatStateLike combatState)
        where TPower : PowerModel
    {
        foreach (Creature player in combatState.PlayerCreatures)
        {
            TPower? power = player.GetPower<TPower>();
            if (power != null)
            {
                await PowerCmd.Remove(power);
            }
        }
    }
}
