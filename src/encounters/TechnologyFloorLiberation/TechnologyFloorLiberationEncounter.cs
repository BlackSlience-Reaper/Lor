using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.afflictions.FuneralOfTheDeadButterflies;
using LibraryOfRuina.afflictions.TechnologyFloorLiberation;
using LibraryOfRuina.backgrounds.TechnologyFloorLiberation;
using LibraryOfRuina.cards;
using LibraryOfRuina.cards.TechnologyFloorLiberation;
using LibraryOfRuina.compat;
using LibraryOfRuina.encounters.HistoryFloorLiberation;
using LibraryOfRuina.events.TechnologyFloorLiberation;
using LibraryOfRuina.features.moontext;
using LibraryOfRuina.features.secondascension;
using LibraryOfRuina.monsters.TechnologyFloorLiberation;
using LibraryOfRuina.powers.TechnologyFloorLiberation;
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

namespace LibraryOfRuina.encounters.TechnologyFloorLiberation;

public sealed class TechnologyFloorLiberationEncounter :
    EncounterModel,
    ILiberationPhaseBgmSource,
    IFloorLiberationEncounter
{
    internal const string CenterSlot = "yesod";
    internal const string HelperLeftSlot = "helper_left";
    internal const string HelperCenterLeftSlot = "helper_center_left";
    internal const string HelperCenterRightSlot = "helper_center_right";
    internal const string Mk4BossSlot = "mk4_boss";
    internal const string Mk4EncounterScenePath = "res://scenes/encounters/technology_floor_liberation_mk4_encounter.tscn";
    internal const string ChordBossSlot = "chord_boss";
    internal const string ChordStaffSlotOne = "chord_staff_1";
    internal const string ChordStaffSlotTwo = "chord_staff_2";
    internal const string ChordStaffSlotThree = "chord_staff_3";
    internal const string ChordEncounterScenePath = "res://scenes/encounters/technology_floor_liberation_chord_encounter.tscn";
    internal const string SolemnMourningBossSlot = "solemn_mourning_boss";
    internal const string SolemnButterflySlotOne = "solemn_butterfly_1";
    internal const string SolemnButterflySlotTwo = "solemn_butterfly_2";
    internal const string SolemnButterflySlotThree = "solemn_butterfly_3";
    internal const string SolemnButterflySlotFour = "solemn_butterfly_4";
    internal const string SolemnMourningEncounterScenePath = "res://scenes/encounters/technology_floor_liberation_solemn_mourning_encounter.tscn";
    internal const string MagicBulletBossSlot = "magic_bullet_boss";
    internal const string MagicBulletEncounterScenePath = "res://scenes/encounters/technology_floor_liberation_magic_bullet_encounter.tscn";
    internal const int MaxPhase = 5;


    private const string CurrentPhaseKey = "CurrentPhase";
    private const string KilledBossCountKey = "KilledBossCount";
    private const string TransitionPendingKey = "TransitionPending";
    private const string SettlementTriggeredKey = "SettlementTriggered";
    private const string EndedByLethalDamageKey = "EndedByLethalDamage";

    private int _currentPhase = 1;
    private int _killedBossCount;
    private bool _transitionPending;
    private bool _settlementTriggered;
    private bool _endedByLethalDamage;

    public override RoomType RoomType => RoomType.Boss;

    public override bool ShouldGiveRewards => false;

    public override bool HasScene => true;

    public override IReadOnlyList<string> Slots =>
    [
        CenterSlot,
        HelperLeftSlot,
        HelperCenterLeftSlot,
        HelperCenterRightSlot,
        Mk4BossSlot,
        ChordStaffSlotOne,
        ChordStaffSlotTwo,
        ChordStaffSlotThree,
        ChordBossSlot,
        SolemnButterflySlotOne,
        SolemnButterflySlotTwo,
        SolemnButterflySlotThree,
        SolemnButterflySlotFour,
        SolemnMourningBossSlot,
        MagicBulletBossSlot
    ];

    protected override bool HasCustomBackground => true;

    public override MegaSkeletonDataResource? BossNodeSpineResource => null;

    public override string BossNodePath => "res://images/map/placeholder/technology_floor_liberation_encounter_icon";

    public int CurrentPhase => _currentPhase;

    public void RefreshLiberationPhaseBgm()
    {
        EncounterBgmController.RefreshCurrentEncounterTrack();
    }

    public int KilledBossCount => _killedBossCount;

    public bool SettlementTriggered => _settlementTriggered;

    public bool EndedByLethalDamage => _endedByLethalDamage;

    public string LiberationFloorId =>
        LiberationFloorIds.Technology;

    public bool IsFullyLiberated => _killedBossCount >= MaxPhase;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<TechnologyFloorRegretBoss>(),
        ModelDb.Monster<TechnologyFloorGrinderMk4Boss>(),
        ModelDb.Monster<TechnologyFloorMk4Helper>(),
        ModelDb.Monster<TechnologyFloorChordBoss>(),
        ModelDb.Monster<TechnologyFloorChordStaff>(),
        ModelDb.Monster<TechnologyFloorSolemnMourningBoss>(),
        ModelDb.Monster<monsters.DeadButterfly.DeadButterfly>(),
        ModelDb.Monster<TechnologyFloorMagicBulletBoss>()
    ];

    public override IEnumerable<string> ExtraAssetPaths =>
        ModelDb.Monster<TechnologyFloorRegretBoss>().AssetPaths
            .Concat(ModelDb.Monster<TechnologyFloorGrinderMk4Boss>().AssetPaths)
            .Concat(ModelDb.Monster<TechnologyFloorMk4Helper>().AssetPaths)
            .Concat(ModelDb.Monster<TechnologyFloorChordBoss>().AssetPaths)
            .Concat(ModelDb.Monster<TechnologyFloorChordStaff>().AssetPaths)
            .Concat(ModelDb.Monster<TechnologyFloorSolemnMourningBoss>().AssetPaths)
            .Concat(ModelDb.Monster<monsters.DeadButterfly.DeadButterfly>().AssetPaths)
            .Concat(ModelDb.Monster<TechnologyFloorMagicBulletBoss>().AssetPaths)
            .Concat(LorexSceneTransitionAssetPaths.All)
            .Concat(new[]
            {
                ImageHelper.GetImagePath("powers/library_of_ruina_paralysis_power.png"),
                ImageHelper.GetImagePath("powers/library_of_ruina_strength_down_power.png"),
                ImageHelper.GetImagePath("powers/library_of_ruina_endure_power.png"),
                ImageHelper.GetImagePath("powers/library_of_ruina_all_around_helper_recognition_mode_power.png"),
                ImageHelper.GetImagePath("powers/library_of_ruina_guard_power.png"),
                ImageHelper.GetImagePath("powers/library_of_ruina_bleed_power.png"),
                ImageHelper.GetImagePath("powers/technology_floor_erosion_power.png"),
                ImageHelper.GetImagePath("powers/technology_floor_mk4_max_charge_power.png"),
                ImageHelper.GetImagePath("powers/technology_floor_mk4_identification_mk2_power.png"),
                ImageHelper.GetImagePath("powers/technology_floor_mk4_limiter_released_power.png"),
                ImageHelper.GetImagePath("powers/regret_iron_echo_power.png"),
                ImageHelper.GetImagePath("powers/regret_extreme_violence_power.png"),
                ImageHelper.GetImagePath("powers/regret_fear_power.png"),
                ImageHelper.GetImagePath("powers/regret_end_begin_end_power.png"),
                ImageHelper.GetImagePath("powers/chord_inspiring_power.png"),
                ImageHelper.GetImagePath("powers/chord_ensemble_power.png"),
                ImageHelper.GetImagePath("powers/chord_staff_melody_craving_power.png"),
                ImageHelper.GetImagePath("powers/solemn_mourning_seal_power.png"),
                ImageHelper.GetImagePath("powers/solemn_mourning_redemption_hand_power.png"),
                ImageHelper.GetImagePath("powers/solemn_mourning_serenity_power.png"),
                ImageHelper.GetImagePath("powers/solemn_mourning_seal_on_enemy_power.png"),
                ModelDb.Affliction<FuneralSealAffliction>().OverlayPath,
                ModelDb.Affliction<SolemnMourningPersistentSealAffliction>().OverlayPath,
                TechnologyFloorLiberationBackgroundController.PhaseOneTexturePath,
                TechnologyFloorLiberationBackgroundController.PhaseTwoTexturePath,
                TechnologyFloorLiberationBackgroundController.PhaseThreeTexturePath,
                TechnologyFloorLiberationBackgroundController.PhaseFourTexturePath,
                TechnologyFloorLiberationBackgroundController.PhaseFiveTexturePath,
                Mk4EncounterScenePath,
                ChordEncounterScenePath,
                SolemnMourningEncounterScenePath,
                MagicBulletEncounterScenePath,
                ImageHelper.GetImagePath(EgoCardBase.GetPortraitResourcePath<RegretEgoCard>()),
                ImageHelper.GetImagePath(EgoCardBase.GetPortraitResourcePath<LimiterReleaseEgoCard>()),
                ImageHelper.GetImagePath(EgoCardBase.GetPortraitResourcePath<ChordEgoCard>()),
                ImageHelper.GetImagePath(EgoCardBase.GetPortraitResourcePath<SolemnMourningEgoCard>()),
                ImageHelper.GetImagePath(EgoCardBase.GetPortraitResourcePath<MagicBulletBaseEgoCard>()),
                ImageHelper.GetImagePath(EgoCardBase.GetPortraitResourcePath<MagicBulletDespairEgoCard>()),
                ImageHelper.GetImagePath("powers/technology_floor_magic_bullet_power.png"),
                ImageHelper.GetImagePath("ui/run_history/technology_floor_liberation_encounter.png"),
                ImageHelper.GetImagePath("ui/run_history/technology_floor_liberation_encounter_outline.png"),
                BossNodePath + ".png",
                BossNodePath + "_outline.png"
            })
            .Concat(HistoryFloorLiberationEncounter.AngelaLiberationBgmTracks)
            .Distinct();

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        if (_currentPhase == 5)
        {
            return [(CreateMagicBulletBoss(), MagicBulletBossSlot)];
        }

        if (_currentPhase == 4)
        {
            return [(CreateSolemnMourningBoss(), SolemnMourningBossSlot)];
        }

        if (_currentPhase == 3)
        {
            return
            [
                (CreateChordStaff(0), ChordStaffSlotOne),
                (CreateChordStaff(1), ChordStaffSlotTwo),
                (CreateChordStaff(2), ChordStaffSlotThree),
                (CreateChordBoss(), ChordBossSlot)
            ];
        }

        if (_currentPhase == 2)
        {
            return
            [
                (CreateHelper(TechnologyFloorMk4HelperInitialMove.Charge), HelperLeftSlot),
                (CreateHelper(TechnologyFloorMk4HelperInitialMove.Clean), HelperCenterLeftSlot),
                (CreateHelper(TechnologyFloorMk4HelperInitialMove.Rest), HelperCenterRightSlot),
                (CreateGrinderMk4(), Mk4BossSlot)
            ];
        }

        return [(CreateRegretBoss(), CenterSlot)];
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
        _currentPhase = ReadPhase(state, CurrentPhaseKey, 1);
        _killedBossCount = Math.Clamp(ReadInt(state, KilledBossCountKey, Math.Max(0, _currentPhase - 1)), 0, MaxPhase);
        _transitionPending = ReadBool(state, TransitionPendingKey);
        _settlementTriggered = ReadBool(state, SettlementTriggeredKey);
        _endedByLethalDamage = ReadBool(state, EndedByLethalDamageKey);
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
                TechnologyFloorLiberationControllerPower>(player);
        }
    }

    public void MarkBossPhaseStarted(int phase)
    {
        if (phase == _currentPhase)
        {
            TechnologyFloorLiberationSettlementStore.Record(this);
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

        if (_currentPhase >= MaxPhase)
        {
            _settlementTriggered = true;
            _endedByLethalDamage = false;
            _transitionPending = false;
            TechnologyFloorLiberationSettlementStore.Record(this);
            return;
        }

        _currentPhase++;
        RefreshLiberationPhaseBgm();
        _transitionPending = true;
        TechnologyFloorLiberationSettlementStore.Record(this);

        if (boss is TechnologyFloorSolemnMourningBoss && boss.Creature.CombatState is { } sealCombatState)
        {
            SolemnMourningPersistentSealAffliction.ClearAllPersistentSeals(sealCombatState);
        }

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
        // 结算触发后仍然持续拦截最后的存活着死亡，直到本场战斗真正结束：
        // 否则结算胜利流程（EndCombatAsLiberationVictory）结束战斗后，若挂起的敌方行动
        // 在战斗结束后补刀，玩家会以真实死亡管线再次进入 KillWithoutCheckingWinCondition，
        // 触发原版 "Player killed outside of combat in multiplayer" 且单边回血，造成 MP 状态分叉。
        return creature.IsPlayer
            && IsLastAlivePlayer(creature)
            && (_settlementTriggered
                || _killedBossCount >= TechnologyFloorLiberationSettlementStore.MinimumKilledBossCount);
    }

    public bool ShouldPreventTransitionBossDeath(Creature creature)
    {
        return _transitionPending
            && !_settlementTriggered
            && creature.Monster is ILiberationPrimaryPhaseBoss boss
            && boss.LiberationPhase + 1 == _currentPhase;
    }

    private static bool IsLastAlivePlayer(Creature creature)
    {
        if (creature.CombatState is not { } combatState)
        {
            return false;
        }

        return !combatState.PlayerCreatures.Any(p => p != creature && p.IsAlive);
    }

    public async Task OnPreventingDeath(Creature creature)
    {
        if (ShouldPreventPlayerDeath(creature))
        {
            await OnPreventingPlayerDeath(creature);
            return;
        }

        if (ShouldPreventTransitionBossDeath(creature))
        {
            await CreatureCmd.SetCurrentHp(creature, 1m);
        }
    }

    private async Task OnPreventingPlayerDeath(Creature creature)
    {
        if (!ShouldPreventPlayerDeath(creature))
        {
            return;
        }

        await CreatureCmd.SetCurrentHp(creature, 1m);
        if (_settlementTriggered)
        {
            // 结算已在进行：只恢复血量并返回，不重复触发胜利流程。
            return;
        }

        _settlementTriggered = true;
        _endedByLethalDamage = true;
        _transitionPending = false;
        StopTechnologyFloorBackgroundMoonTextLoops(creature.CombatState);
        TechnologyFloorLiberationSettlementStore.Record(this);

        await EndCombatAsLiberationVictory(creature.CombatState);
    }

    public async Task OnBeforeSideTurnStart(CombatSide side, CombatStateLike combatState)
    {
        await EnsureControllerPowers(combatState);

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

        await SpawnNextPhaseFromTransition(combatState);
    }

    private async Task SpawnNextPhaseFromTransition(CombatStateLike combatState)
    {
        await LiberationPhaseCleanup.RemovePhaseCreatures(
            combatState,
            beforeRemove: StopPhaseBackgroundMoonTextLoop);
        await SpawnNextPhase(combatState);
    }

    private async Task SpawnNextPhase(CombatStateLike combatState)
    {
        TextureRect? backgroundImage = TechnologyFloorLiberationBackgroundController.GetCurrentBackgroundImage();
        StopTechnologyFloorBackgroundMoonTextLoops(combatState);
        _transitionPending = false;

        if (combatState.Enemies.Any(static enemy => enemy.IsAlive))
        {
            return;
        }

        if (_currentPhase == 2 && NCombatRoom.Instance is { } mkRoom)
        {
            ReplaceEncounterSceneForMk4Phase(mkRoom);
        }
        else if (_currentPhase == 3 && NCombatRoom.Instance is { } chordRoom)
        {
            ReplaceEncounterSceneForChordPhase(chordRoom);
        }
        else if (_currentPhase == 4 && NCombatRoom.Instance is { } solemnRoom)
        {
            ReplaceEncounterSceneForSolemnMourningPhase(solemnRoom);
        }
        else if (_currentPhase == 5 && NCombatRoom.Instance is { } magicBulletRoom)
        {
            ReplaceEncounterSceneForMagicBulletPhase(magicBulletRoom);
        }

        await LorexSceneTransitionController.PlayPhaseBackgroundRevealAsync(
            backgroundImage,
            TechnologyFloorLiberationBackgroundController.GetPhaseBackgroundTexturePath(_currentPhase),
            SpawnCurrentPhaseCreatures,
            () => TechnologyFloorLiberationBackgroundController.SetPhaseBackground(_currentPhase));
        return;

        async Task SpawnCurrentPhaseCreatures()
        {
        if (_currentPhase == 2)
        {
            // Boss必须先生成，小怪（带MinionPower）依赖boss存在
            Creature bossMk4 = await CreatureCmd.Add(CreateGrinderMk4(), combatState, CombatSide.Enemy, Mk4BossSlot);
            bossMk4.PrepareForNextTurn(combatState.PlayerCreatures);

            IReadOnlyList<(MonsterModel Monster, string Slot)> helpers =
            [
                (CreateHelper(TechnologyFloorMk4HelperInitialMove.Charge), HelperLeftSlot),
                (CreateHelper(TechnologyFloorMk4HelperInitialMove.Clean), HelperCenterLeftSlot),
                (CreateHelper(TechnologyFloorMk4HelperInitialMove.Rest), HelperCenterRightSlot)
            ];

            foreach ((MonsterModel monster, string slot) in helpers)
            {
                Creature spawned = await CreatureCmd.Add(monster, combatState, CombatSide.Enemy, slot);
                spawned.PrepareForNextTurn(combatState.PlayerCreatures);
            }

            combatState.SortEnemiesBySlotName();
            TechnologyFloorLiberationSettlementStore.Record(this);
        }
        else if (_currentPhase == 3)
        {
            // Boss必须先生成，小怪（带MinionPower）依赖boss存在
            Creature bossChord = await CreatureCmd.Add(CreateChordBoss(), combatState, CombatSide.Enemy, ChordBossSlot);
            bossChord.PrepareForNextTurn(combatState.PlayerCreatures);

            IReadOnlyList<(MonsterModel Monster, string Slot)> staffs =
            [
                (CreateChordStaff(0), ChordStaffSlotOne),
                (CreateChordStaff(1), ChordStaffSlotTwo),
                (CreateChordStaff(2), ChordStaffSlotThree)
            ];

            foreach ((MonsterModel monster, string slot) in staffs)
            {
                Creature spawned = await CreatureCmd.Add(monster, combatState, CombatSide.Enemy, slot);
                spawned.PrepareForNextTurn(combatState.PlayerCreatures);
            }

            combatState.SortEnemiesBySlotName();
            TechnologyFloorLiberationSettlementStore.Record(this);
        }
        else if (_currentPhase == 4)
        {
            Creature spawned = await CreatureCmd.Add(
                CreateSolemnMourningBoss(), combatState, CombatSide.Enemy, SolemnMourningBossSlot);
            spawned.PrepareForNextTurn(combatState.PlayerCreatures);

            combatState.SortEnemiesBySlotName();
            TechnologyFloorLiberationSettlementStore.Record(this);
        }
        else if (_currentPhase == 5)
        {
            Creature spawned = await CreatureCmd.Add(
                CreateMagicBulletBoss(), combatState, CombatSide.Enemy, MagicBulletBossSlot);
            spawned.PrepareForNextTurn(combatState.PlayerCreatures);

            combatState.SortEnemiesBySlotName();
            TechnologyFloorLiberationSettlementStore.Record(this);
        }
        }
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

    private static void ReplaceEncounterSceneForMk4Phase(NCombatRoom room)
    {
        if (!VanillaPrivate.CombatRoomEncounterSlots.IsAvailable)
        {
            throw new InvalidOperationException("NCombatRoom.EncounterSlots could not be found.");
        }

        Control? existingSlots = VanillaPrivate.CombatRoomEncounterSlots.Get(room) as Control;
        if (existingSlots?.GetNodeOrNull<Marker2D>(Mk4BossSlot) != null
            && existingSlots.GetNodeOrNull<Marker2D>(CenterSlot) == null)
        {
            return;
        }

        Control parent = existingSlots?.GetParent() as Control ?? room.GetNode<Control>("%EnemyContainer");
        if (existingSlots != null && GodotObject.IsInstanceValid(existingSlots))
        {
            parent.RemoveChildSafely(existingSlots);
            existingSlots.QueueFreeSafely();
        }

        Control mk4Slots = InstantiateMk4EncounterScene();
        mk4Slots.Position -= new Vector2(1920f, 1080f) * 0.5f;
        parent.AddChildSafely(mk4Slots);
        VanillaPrivate.CombatRoomEncounterSlots.Set(room, mk4Slots);
    }

    internal static Control InstantiateMk4EncounterScene() =>
        PreloadManager.Cache.GetScene(Mk4EncounterScenePath)
            .Instantiate<Control>();

    private static void ReplaceEncounterSceneForChordPhase(NCombatRoom room)
    {
        if (!VanillaPrivate.CombatRoomEncounterSlots.IsAvailable)
        {
            throw new InvalidOperationException("NCombatRoom.EncounterSlots could not be found.");
        }

        Control? existingSlots = VanillaPrivate.CombatRoomEncounterSlots.Get(room) as Control;
        if (existingSlots?.GetNodeOrNull<Marker2D>(ChordBossSlot) != null)
        {
            return;
        }

        Control parent = existingSlots?.GetParent() as Control ?? room.GetNode<Control>("%EnemyContainer");
        if (existingSlots != null && GodotObject.IsInstanceValid(existingSlots))
        {
            parent.RemoveChildSafely(existingSlots);
            existingSlots.QueueFreeSafely();
        }

        Control chordSlots = InstantiateChordEncounterScene();
        chordSlots.Position -= new Vector2(1920f, 1080f) * 0.5f;
        parent.AddChildSafely(chordSlots);
        VanillaPrivate.CombatRoomEncounterSlots.Set(room, chordSlots);
    }

    internal static Control InstantiateChordEncounterScene() =>
        PreloadManager.Cache.GetScene(ChordEncounterScenePath)
            .Instantiate<Control>();

    private static void ReplaceEncounterSceneForSolemnMourningPhase(NCombatRoom room)
    {
        if (!VanillaPrivate.CombatRoomEncounterSlots.IsAvailable)
        {
            throw new InvalidOperationException("NCombatRoom.EncounterSlots could not be found.");
        }

        Control? existingSlots = VanillaPrivate.CombatRoomEncounterSlots.Get(room) as Control;
        if (existingSlots?.GetNodeOrNull<Marker2D>(SolemnMourningBossSlot) != null)
        {
            return;
        }

        Control parent = existingSlots?.GetParent() as Control ?? room.GetNode<Control>("%EnemyContainer");
        if (existingSlots != null && GodotObject.IsInstanceValid(existingSlots))
        {
            parent.RemoveChildSafely(existingSlots);
            existingSlots.QueueFreeSafely();
        }

        Control solemnSlots = InstantiateSolemnMourningEncounterScene();
        solemnSlots.Position -= new Vector2(1920f, 1080f) * 0.5f;
        parent.AddChildSafely(solemnSlots);
        VanillaPrivate.CombatRoomEncounterSlots.Set(room, solemnSlots);
    }

    internal static Control InstantiateSolemnMourningEncounterScene() =>
        PreloadManager.Cache.GetScene(SolemnMourningEncounterScenePath)
            .Instantiate<Control>();

    private static void ReplaceEncounterSceneForMagicBulletPhase(NCombatRoom room)
    {
        if (!VanillaPrivate.CombatRoomEncounterSlots.IsAvailable)
        {
            throw new InvalidOperationException("NCombatRoom.EncounterSlots could not be found.");
        }

        Control? existingSlots = VanillaPrivate.CombatRoomEncounterSlots.Get(room) as Control;
        if (existingSlots?.GetNodeOrNull<Marker2D>(MagicBulletBossSlot) != null)
        {
            return;
        }

        Control parent = existingSlots?.GetParent() as Control ?? room.GetNode<Control>("%EnemyContainer");
        if (existingSlots != null && GodotObject.IsInstanceValid(existingSlots))
        {
            parent.RemoveChildSafely(existingSlots);
            existingSlots.QueueFreeSafely();
        }

        Control magicBulletSlots = InstantiateMagicBulletEncounterScene();
        magicBulletSlots.Position -= new Vector2(1920f, 1080f) * 0.5f;
        parent.AddChildSafely(magicBulletSlots);
        VanillaPrivate.CombatRoomEncounterSlots.Set(room, magicBulletSlots);
    }

    internal static Control InstantiateMagicBulletEncounterScene() =>
        PreloadManager.Cache.GetScene(MagicBulletEncounterScenePath)
            .Instantiate<Control>();

    private static MonsterModel CreateRegretBoss() =>
        ModelDb.Monster<TechnologyFloorRegretBoss>().ToMutable();

    private static MonsterModel CreateGrinderMk4() =>
        ModelDb.Monster<TechnologyFloorGrinderMk4Boss>().ToMutable();

    private static MonsterModel CreateHelper(TechnologyFloorMk4HelperInitialMove initialMove)
    {
        var helper = (TechnologyFloorMk4Helper)ModelDb.Monster<TechnologyFloorMk4Helper>().ToMutable();
        helper.ConfigureInitialMove(initialMove);
        return helper;
    }

    private static MonsterModel CreateChordBoss() =>
        ModelDb.Monster<TechnologyFloorChordBoss>().ToMutable();

    private static MonsterModel CreateSolemnMourningBoss() =>
        ModelDb.Monster<TechnologyFloorSolemnMourningBoss>().ToMutable();

    private static MonsterModel CreateMagicBulletBoss() =>
        ModelDb.Monster<TechnologyFloorMagicBulletBoss>().ToMutable();

    private static MonsterModel CreateChordStaff(int patternIndex)
    {
        var staff = (TechnologyFloorChordStaff)ModelDb.Monster<TechnologyFloorChordStaff>().ToMutable();
        staff.ConfigurePattern(patternIndex);
        return staff;
    }

    private static void StopTechnologyFloorBackgroundMoonTextLoops(CombatStateLike? combatState)
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
        if (creature.Monster is TechnologyFloorRegretBoss regret)
        {
            regret.StopBackgroundMoonTextLoop();
            return;
        }

        if (creature.Monster is TechnologyFloorGrinderMk4Boss mk4)
        {
            mk4.StopBackgroundMoonTextLoop();
            return;
        }

        if (creature.Monster is TechnologyFloorChordBoss chord)
        {
            chord.StopBackgroundMoonTextLoop();
            return;
        }

        if (creature.Monster is TechnologyFloorSolemnMourningBoss solemn)
        {
            solemn.StopBackgroundMoonTextLoop();
            return;
        }

        if (creature.Monster is TechnologyFloorMagicBulletBoss magicBullet)
        {
            magicBullet.StopBackgroundMoonTextLoop();
            return;
        }

        MoonTextService.StopRandomLoop(creature, TechnologyFloorRegretBoss.BackgroundTextScope);
        MoonTextService.StopRandomLoop(creature, TechnologyFloorGrinderMk4Boss.BackgroundTextScope);
        MoonTextService.StopRandomLoop(creature, TechnologyFloorChordBoss.BackgroundTextScope);
        MoonTextService.StopRandomLoop(creature, TechnologyFloorSolemnMourningBoss.BackgroundTextScope);
        MoonTextService.StopRandomLoop(creature, TechnologyFloorMagicBulletBoss.BackgroundTextScope);
    }

    private static async Task EndCombatAsLiberationVictory(CombatStateLike? combatState)
    {
        if (combatState == null || !CombatManager.Instance.IsInProgress)
        {
            // 战斗已经结束/正在结束：不再进入击杀与胜负复核管线，避免在 EndCombatInternal
            // 之后重入 KillWithoutCheckingWinCondition（多人下会触发 "killed outside of combat"）。
            return;
        }

        foreach (Creature enemy in combatState.Enemies.ToArray())
        {
            if (enemy.IsAlive)
            {
                await CreatureCmd.Kill(enemy, force: true);
            }
        }

        if (CombatManager.Instance.IsInProgress)
        {
            await CombatManager.Instance.CheckWinCondition();
        }
    }

    private static int ReadInt(Dictionary<string, string> state, string key, int fallback)
    {
        return state.TryGetValue(key, out string? value) && int.TryParse(value, out int parsed)
            ? parsed
            : fallback;
    }

    private static int ReadPhase(Dictionary<string, string> state, string key, int fallback) =>
        Math.Clamp(ReadInt(state, key, fallback), 1, MaxPhase);

    private static bool ReadBool(Dictionary<string, string> state, string key)
    {
        return state.TryGetValue(key, out string? value) && bool.TryParse(value, out bool parsed) && parsed;
    }
}
