using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.cards;
using LibraryOfRuina.content.abnormalities.CosmicFragment;
using LibraryOfRuina.content.liberation.History;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.cards;
using LibraryOfRuina.framework.encounters;
using LibraryOfRuina.scene_transitions;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.content.liberation.Art;

public sealed class ArtFloorLiberationEncounter :
    LiberationEncounterBase,
    IEncounterBgmSource,
    ILiberationPhaseBgmSource,
    IFloorLiberationEncounter
{
    EncounterBgmConfig IEncounterBgmSource.Bgm => EncounterBgmConfig.PhaseBased(
        "ArtFloorLiberationBGM",
        HistoryFloorLiberationEncounter.AngelaLiberationBgmTracks,
        volumeScale: 0.85f);

    internal const string DaCapoSlot = "da_capo";
    internal const string FirstPerformerSlot = "first_performer";
    internal const string GalaxyFriendLeftSlot = "galaxy_friend_left";
    internal const string LittleGalaxySlot = "little_galaxy";
    internal const string GalaxyFriendRightSlot = "galaxy_friend_right";
    internal const string PleasureSlot = "pleasure";
    internal const string DustbornLeftSlot = "dustborn_left";
    internal const string NostalgicScentSlot = "nostalgic_scent";
    internal const string DustbornRightSlot = "dustborn_right";
    internal const string FinalPerformerOneSlot = "final_performer_1";
    internal const string FinalPerformerTwoSlot = "final_performer_2";
    internal const string FinalDaCapoSlot = "final_da_capo";
    internal const string FinalPerformerThreeSlot = "final_performer_3";
    internal const string FinalPerformerFourSlot = "final_performer_4";
    internal const int MaxPhase = 6;
    private const decimal PhaseTransitionHealAmount = 6m;

    private const string CurrentPhaseKey = "CurrentPhase";
    private const string KilledBossCountKey = "KilledBossCount";
    private const string TransitionPendingKey = "TransitionPending";
    private const string SettlementTriggeredKey = "SettlementTriggered";
    private const string EndedByPlaceholderKey = "EndedByPlaceholder";
    private const string EndedByLethalDamageKey = "EndedByLethalDamage";

    private int _currentPhase = 1;
    private int _killedBossCount;
    private bool _transitionPending;
    private bool _settlementTriggered;
    private bool _endedByPlaceholder;
    private bool _endedByLethalDamage;

    public override RoomType RoomType => RoomType.Boss;

    public override bool ShouldGiveRewards => false;

    public override bool HasScene => true;

    public override IReadOnlyList<string> Slots =>
    [
        FirstPerformerSlot,
        DaCapoSlot,
        GalaxyFriendLeftSlot,
        LittleGalaxySlot,
        GalaxyFriendRightSlot,
        PleasureSlot,
        DustbornLeftSlot,
        NostalgicScentSlot,
        DustbornRightSlot,
        FinalPerformerOneSlot,
        FinalPerformerTwoSlot,
        FinalDaCapoSlot,
        FinalPerformerThreeSlot,
        FinalPerformerFourSlot
    ];

    protected override bool HasCustomBackground => true;

    public override float GetCameraScaling() => 0.82f;

    public override Vector2 GetCameraOffset() => Vector2.Down * 50f + Vector2.Left * 100f;

    public override MegaSkeletonDataResource? BossNodeSpineResource => null;

    public override string BossNodePath => "res://images/map/placeholder/art_floor_liberation_encounter_icon";

    public int CurrentPhase => _currentPhase;

    public int KilledBossCount => _killedBossCount;

    public bool SettlementTriggered => _settlementTriggered;

    public bool EndedByPlaceholder => _endedByPlaceholder;

    public bool EndedByLethalDamage => _endedByLethalDamage;

    public string LiberationFloorId =>
        LiberationFloorIds.Art;

    public bool IsFullyLiberated => _killedBossCount >= MaxPhase;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<ArtFloorDaCapoBoss>(),
        ModelDb.Monster<ArtFloorFirstPerformer>(),
        ModelDb.Monster<ArtFloorBeyondFragmentBoss>(),
        ModelDb.Monster<ArtFloorLittleGalaxyBoss>(),
        ModelDb.Monster<ArtFloorGalaxyFriend>(),
        ModelDb.Monster<ArtFloorPleasureBoss>(),
        ModelDb.Monster<ArtFloorNostalgicScentBoss>(),
        ModelDb.Monster<ArtFloorDustbornPerson>(),
        ModelDb.Monster<ArtFloorFinalDaCapoBoss>(),
        ModelDb.Monster<ArtFloorDaCapoPerformer>()
    ];

    public override IEnumerable<string> ExtraAssetPaths =>
        ModelDb.Monster<ArtFloorDaCapoBoss>().AssetPaths
            .Concat(ModelDb.Monster<ArtFloorFirstPerformer>().AssetPaths)
            .Concat(ModelDb.Monster<ArtFloorBeyondFragmentBoss>().AssetPaths)
            .Concat(ModelDb.Monster<ArtFloorLittleGalaxyBoss>().AssetPaths)
            .Concat(ModelDb.Monster<ArtFloorGalaxyFriend>().AssetPaths)
            .Concat(ModelDb.Monster<ArtFloorPleasureBoss>().AssetPaths)
            .Concat(ModelDb.Monster<ArtFloorNostalgicScentBoss>().AssetPaths)
            .Concat(ModelDb.Monster<ArtFloorDustbornPerson>().AssetPaths)
            .Concat(ModelDb.Monster<ArtFloorFinalDaCapoBoss>().AssetPaths)
            .Concat(ModelDb.Monster<ArtFloorDaCapoPerformer>().AssetPaths)
            .Concat(LorexSceneTransitionAssetPaths.All)
            .Concat(new[]
            {
                ImageHelper.GetImagePath("powers/art_floor_green_passive_power.png"),
                ImageHelper.GetImagePath("powers/fanatic_worship_power.png"),
                ImageHelper.GetImagePath("powers/art_floor_erosion_power.png"),
                ImageHelper.GetImagePath("powers/spider_bud_untargetable_power.png"),
                ImageHelper.GetImagePath("powers/art_floor_little_galaxy_eternal_farewell_power.png"),
                ImageHelper.GetImagePath("powers/art_floor_little_galaxy_pebble_power.png"),
                ImageHelper.GetImagePath("powers/art_floor_galaxy_do_not_leave_me_power.png"),
                ImageHelper.GetImagePath("powers/art_floor_pleasure_joy_thorns_power.png"),
                ImageHelper.GetImagePath("powers/art_floor_pleasure_soft_body_power.png"),
                ImageHelper.GetImagePath("powers/art_floor_pleasure_unbearable_pleasure_power.png"),
                ImageHelper.GetImagePath("powers/art_floor_pleasure_exploding_head_power.png"),
                ImageHelper.GetImagePath("powers/art_floor_atonement_crown_power.png"),
                ImageHelper.GetImagePath("powers/art_floor_petal_power.png"),
                ImageHelper.GetImagePath("powers/art_floor_fragrance_power.png"),
                ImageHelper.GetImagePath("powers/art_floor_collapse_power.png"),
                ImageHelper.GetImagePath("powers/art_floor_final_da_capo_cycle_power.png"),
                ImageHelper.GetImagePath("powers/art_floor_final_da_capo_aria_power.png"),
                ImageHelper.GetImagePath("powers/art_floor_final_da_capo_performer_passive_power.png"),
                ImageHelper.GetImagePath("powers/art_floor_adagio_cantabile_power.png"),
                ImageHelper.GetImagePath("powers/art_floor_imbalanced_power.png"),
                ImageHelper.GetImagePath("powers/art_floor_da_capo_soul_binding_power.png"),
                ImageHelper.GetImagePath("powers/beyond_fragment_tentacle_power.png"),
                ImageHelper.GetImagePath("powers/beyond_fragment_incomprehensible_power.png"),
                ImageHelper.GetImagePath("powers/boundary_thorn_power.png"),
                ArtFloorLiberationBackgroundController.PhaseOneTexturePath,
                ArtFloorLiberationBackgroundController.PhaseTwoTexturePath,
                ArtFloorLiberationBackgroundController.PhaseThreeTexturePath,
                ArtFloorLiberationBackgroundController.PhaseFourTexturePath,
                ArtFloorLiberationBackgroundController.PhaseFiveTexturePath,
                ArtFloorLiberationBackgroundController.GalaxyFilterNormalTexturePath,
                ArtFloorLiberationBackgroundController.GalaxyFilterFakeDeathTexturePath,
                ArtFloorLiberationBackgroundController.BackgroundScenePath,
                BossNodePath + ".png",
                BossNodePath + "_outline.png",
                ImageHelper.GetImagePath("ui/run_history/art_floor_liberation_encounter.png"),
                ImageHelper.GetImagePath("ui/run_history/art_floor_liberation_encounter_outline.png"),
                ImageHelper.GetImagePath(EgoCardBase.GetPortraitResourcePath<BeyondFragmentEgoCard>()),
                ImageHelper.GetImagePath(OurLittleGalaxyEgoPreviewCard.GetPortraitResourcePath()),
                ImageHelper.GetImagePath(PleasureCard.GetPortraitResourcePath()),
                ImageHelper.GetImagePath(PleasureEgoCard.GetPortraitResourcePath()),
                ImageHelper.GetImagePath(NostalgicScentEgoCard.GetPortraitResourcePath())
            })
            .Concat(HistoryFloorLiberationEncounter.AngelaLiberationBgmTracks)
            .Distinct();

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        if (_currentPhase >= 6)
        {
            return CreatePhaseSixMonsters();
        }

        if (_currentPhase >= 5)
        {
            return CreatePhaseFiveMonsters();
        }

        if (_currentPhase >= 4)
        {
            return [(ModelDb.Monster<ArtFloorPleasureBoss>().ToMutable(), PleasureSlot)];
        }

        if (_currentPhase >= 3)
        {
            return CreatePhaseThreeMonsters();
        }

        if (_currentPhase >= 2)
        {
            return [(ModelDb.Monster<ArtFloorBeyondFragmentBoss>().ToMutable(), DaCapoSlot)];
        }

        return
        [
            (ModelDb.Monster<ArtFloorFirstPerformer>().ToMutable(), FirstPerformerSlot),
            (ModelDb.Monster<ArtFloorDaCapoBoss>().ToMutable(), DaCapoSlot)
        ];
    }

    public override Dictionary<string, string> SaveCustomState()
    {
        return new Dictionary<string, string>
        {
            [CurrentPhaseKey] = _currentPhase.ToString(),
            [KilledBossCountKey] = _killedBossCount.ToString(),
            [TransitionPendingKey] = _transitionPending.ToString(),
            [SettlementTriggeredKey] = _settlementTriggered.ToString(),
            [EndedByPlaceholderKey] = _endedByPlaceholder.ToString(),
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
        _endedByPlaceholder = bag.ReadBool(EndedByPlaceholderKey);
        _endedByLethalDamage = bag.ReadBool(EndedByLethalDamageKey);
        ArtFloorLiberationBackgroundController.SetPhaseBackground(_currentPhase);
    }

    public async Task EnsureControllerPowers(CombatStateLike? combatState)
    {
        if (combatState == null)
        {
            return;
        }

        foreach (Creature player in combatState.PlayerCreatures)
        {
            await PowerCmdCompat.Ensure<ArtFloorLiberationControllerPower>(
                player);
        }
    }

    public void MarkBossPhaseStarted(int phase)
    {
        if (phase == _currentPhase)
        {
            _killedBossCount = Math.Min(_killedBossCount, phase - 1);
            ArtFloorLiberationBackgroundController.SetPhaseBackground(phase);
            ArtFloorLiberationSettlementStore.Record(this);
        }
    }

    internal async Task OnPhaseBossDeath(
        ILiberationPrimaryPhaseBoss boss,
        bool wasRemovalPrevented,
        float deathAnimLength,
        bool endedByPlaceholder = false)
    {
        if (wasRemovalPrevented
            || _settlementTriggered
            || boss.LiberationPhase != _currentPhase)
        {
            return;
        }

        _killedBossCount = Math.Max(_killedBossCount, boss.LiberationPhase);

        if (_currentPhase >= MaxPhase)
        {
            _settlementTriggered = true;
            _transitionPending = false;
            _endedByPlaceholder = endedByPlaceholder;
            _endedByLethalDamage = false;
            if (boss.Creature.CombatState is { } finalState)
            {
                await ClearPhaseTwoPlayerState(finalState);
            }
            ArtFloorLiberationBackgroundController.SetGalaxyCryingMode(false);
            ArtFloorLiberationSettlementStore.Record(this);
            return;
        }

        _currentPhase++;
        RefreshLiberationPhaseBgm();
        _transitionPending = true;
        _endedByPlaceholder = endedByPlaceholder;
        _endedByLethalDamage = false;
        ArtFloorLiberationSettlementStore.Record(this);

        if (boss.Creature.CombatState is { } combatState)
        {
            if (boss is ArtFloorPleasureBoss)
            {
                await ClearPleasureCards(combatState);
            }

            await RemoveOtherPhaseCreatures(combatState, boss.Creature);
            await ClearPhaseTwoPlayerState(combatState);
            await LiberationPhasePlayerRecovery.RestorePlayers(
                combatState,
                PhaseTransitionHealAmount);
        }

        await LiberationPhaseTransition.ShowAsync(
            boss,
            triggerAnimation: true);
        await Cmd.CustomScaledWait(Math.Max(0.15f, deathAnimLength), Math.Max(0.3f, deathAnimLength));
    }

    internal async Task TriggerPlaceholderPhaseEnd(CombatStateLike? combatState, bool fromTurnLimit)
    {
        if (_settlementTriggered)
        {
            return;
        }

        _endedByPlaceholder = fromTurnLimit;

        ILiberationPrimaryPhaseBoss? phaseBoss = combatState?.Enemies
            .Select(static enemy => enemy.Monster)
            .OfType<ILiberationPrimaryPhaseBoss>()
            .FirstOrDefault(boss => boss.LiberationPhase == _currentPhase);
        if (phaseBoss == null)
        {
            return;
        }

        await OnPhaseBossDeath(
            phaseBoss,
            wasRemovalPrevented: false,
            deathAnimLength: 0.3f,
            endedByPlaceholder: fromTurnLimit);
    }

    public bool ShouldPreventPlayerDeath(Creature creature)
    {
        // 结算触发后仍持续拦截最后的存活着死亡，直到本场战斗真正结束（同 TechnologyFloor）：
        // 避免挂起的敌方行动在战斗结束后补刀，让玩家以真实死亡管线重入
        // KillWithoutCheckingWinCondition 触发 "killed outside of combat in multiplayer"。
        return creature.IsPlayer
            && IsLastAlivePlayer(creature)
            && (_settlementTriggered || _killedBossCount >= 1);
    }

    public bool ShouldPreventTransitionBossDeath(Creature creature)
    {
        return _transitionPending
            && !_settlementTriggered
            && creature.Monster is ILiberationPrimaryPhaseBoss boss
            && boss.LiberationPhase + 1 == _currentPhase;
    }

    public async Task OnPreventingDeath(Creature creature)
    {
        if (ShouldPreventPlayerDeath(creature))
        {
            await CreatureCmd.SetCurrentHp(creature, 1m);
            if (_settlementTriggered)
            {
                // 结算已在进行：只恢复血量并返回，不重复触发胜利流程。
                return;
            }

            _settlementTriggered = true;
            _transitionPending = false;
            _endedByLethalDamage = true;
            _endedByPlaceholder = false;
            ArtFloorLiberationSettlementStore.Record(this);

            await EndCombatAsLiberationVictory(creature.CombatState);
            return;
        }

        if (ShouldPreventTransitionBossDeath(creature))
        {
            await CreatureCmd.SetCurrentHp(creature, 1m);
        }
    }

    public async Task OnBeforeSideTurnStart(CombatSide side, CombatStateLike combatState)
    {
        await EnsureControllerPowers(combatState);
        await ApplyNostalgicScentWinterStasisIfNeeded(combatState);

        if (side == CombatSide.Enemy && _transitionPending && !_settlementTriggered
            && !combatState.Enemies.Any(static e =>
                e.Monster is ILiberationPhaseBoss))
        {
            await SpawnNextPhaseFromTransition(combatState);
        }
    }

    public async Task ApplyNostalgicScentWinterStasis(CombatStateLike combatState)
    {
        bool applied = false;
        foreach (Creature dustborn in combatState.Enemies.Where(static enemy =>
                     enemy.Monster is ArtFloorDustbornPerson && enemy.IsAlive))
        {
            await PowerCmdCompat.Apply<ArtFloorDustbornWinterStasisPower>(
                dustborn,
                1m,
                dustborn,
                null,
                silent: true);
            if (dustborn.Monster is ArtFloorDustbornPerson person)
            {
                await person.QueueWinterStasis();
            }

            applied = true;
        }

        if (applied)
        {
            combatState.SortEnemiesBySlotName();
        }
    }

    public async Task ApplyNostalgicScentWinterStasisIfNeeded(CombatStateLike combatState)
    {
        if (_currentPhase != 5 || _settlementTriggered)
        {
            return;
        }

        ArtFloorNostalgicScentBoss? boss = combatState.Enemies
            .Select(static enemy => enemy.Monster)
            .OfType<ArtFloorNostalgicScentBoss>()
            .FirstOrDefault();
        if (boss is not { IsWinterBeginningQueued: true })
        {
            return;
        }

        await ApplyNostalgicScentWinterStasis(combatState);
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
        await LiberationPhaseCleanup.RemovePhaseCreatures(combatState);
        await SpawnNextPhase(combatState);
    }

    private async Task SpawnNextPhase(CombatStateLike combatState)
    {
        TextureRect? backgroundImage = ArtFloorLiberationBackgroundController.GetCurrentBackgroundImage();
        await ClearPhaseTwoPlayerState(combatState);

        if (combatState.Enemies.Any(static enemy => enemy.IsAlive))
        {
            return;
        }

        await LorexSceneTransitionController.PlayPhaseBackgroundRevealAsync(
            backgroundImage,
            ArtFloorLiberationBackgroundController.GetPhaseBackgroundTexturePath(_currentPhase),
            SpawnCurrentPhaseCreatures,
            () => ArtFloorLiberationBackgroundController.SetPhaseBackground(_currentPhase));
        _transitionPending = false;

        async Task SpawnCurrentPhaseCreatures()
        {
        if (_currentPhase >= 6)
        {
            foreach ((MonsterModel monster, string slot) in CreatePhaseSixMonstersForSpawn())
            {
                Creature spawnedCreature = await CreatureCmd.Add(monster, combatState, CombatSide.Enemy, slot);
                spawnedCreature.PrepareForNextTurn(combatState.PlayerCreatures);
            }

            await EnsureFinalDaCapoPerformerPassivePowers(combatState);
            combatState.SortEnemiesBySlotName();
            ArtFloorLiberationSettlementStore.Record(this);
            return;
        }

        if (_currentPhase >= 5)
        {
            foreach ((MonsterModel monster, string slot) in CreatePhaseFiveMonstersForSpawn())
            {
                Creature spawnedCreature = await CreatureCmd.Add(monster, combatState, CombatSide.Enemy, slot);
                spawnedCreature.PrepareForNextTurn(combatState.PlayerCreatures);
            }

            combatState.SortEnemiesBySlotName();
            await ApplyNostalgicScentWinterStasisIfNeeded(combatState);
            ArtFloorLiberationSettlementStore.Record(this);
            return;
        }

        if (_currentPhase >= 4)
        {
            Creature boss = await CreatureCmd.Add(
                ModelDb.Monster<ArtFloorPleasureBoss>().ToMutable(),
                combatState,
                CombatSide.Enemy,
                PleasureSlot);
            boss.PrepareForNextTurn(combatState.PlayerCreatures);
            combatState.SortEnemiesBySlotName();
            ArtFloorLiberationSettlementStore.Record(this);
            return;
        }

        if (_currentPhase >= 3)
        {
            Creature boss = await CreatureCmd.Add(
                ModelDb.Monster<ArtFloorLittleGalaxyBoss>().ToMutable(),
                combatState,
                CombatSide.Enemy,
                LittleGalaxySlot);
            boss.PrepareForNextTurn(combatState.PlayerCreatures);

            IReadOnlyList<(MonsterModel Monster, string Slot)> friends =
            [
                (CreateArtFloorGalaxyFriend(ArtFloorGalaxyFriendInitialMove.Wait), GalaxyFriendLeftSlot),
                (CreateArtFloorGalaxyFriend(ArtFloorGalaxyFriendInitialMove.StarlightFall), GalaxyFriendRightSlot)
            ];

            foreach ((MonsterModel monster, string slot) in friends)
            {
                Creature spawnedCreature = await CreatureCmd.Add(monster, combatState, CombatSide.Enemy, slot);
                spawnedCreature.PrepareForNextTurn(combatState.PlayerCreatures);
            }

            combatState.SortEnemiesBySlotName();
            ArtFloorLiberationSettlementStore.Record(this);
            return;
        }

        Creature spawned = await CreatureCmd.Add(
            ModelDb.Monster<ArtFloorBeyondFragmentBoss>().ToMutable(),
            combatState,
            CombatSide.Enemy,
            DaCapoSlot);
        spawned.PrepareForNextTurn(combatState.PlayerCreatures);
        combatState.SortEnemiesBySlotName();
        ArtFloorLiberationSettlementStore.Record(this);
        }
    }

    private static IReadOnlyList<(MonsterModel, string?)> CreatePhaseThreeMonsters()
    {
        return
        [
            (CreateArtFloorGalaxyFriend(ArtFloorGalaxyFriendInitialMove.Wait), GalaxyFriendLeftSlot),
            (ModelDb.Monster<ArtFloorLittleGalaxyBoss>().ToMutable(), LittleGalaxySlot),
            (CreateArtFloorGalaxyFriend(ArtFloorGalaxyFriendInitialMove.StarlightFall), GalaxyFriendRightSlot)
        ];
    }

    private static IReadOnlyList<(MonsterModel, string?)> CreatePhaseFiveMonsters()
    {
        return CreatePhaseFiveMonstersForSpawn()
            .Select(static entry => ((MonsterModel)entry.Monster, (string?)entry.Slot))
            .ToArray();
    }

    private static IReadOnlyList<(MonsterModel, string?)> CreatePhaseSixMonsters()
    {
        return CreatePhaseSixMonstersForSpawn()
            .Select(static entry => ((MonsterModel)entry.Monster, (string?)entry.Slot))
            .ToArray();
    }

    private static IReadOnlyList<(MonsterModel Monster, string Slot)> CreatePhaseFiveMonstersForSpawn()
    {
        return
        [
            (CreateDustbornPerson(ArtFloorDustbornSide.Left), DustbornLeftSlot),
            (ModelDb.Monster<ArtFloorNostalgicScentBoss>().ToMutable(), NostalgicScentSlot),
            (CreateDustbornPerson(ArtFloorDustbornSide.Right), DustbornRightSlot)
        ];
    }

    private static IReadOnlyList<(MonsterModel Monster, string Slot)> CreatePhaseSixMonstersForSpawn()
    {
        return
        [
            (ModelDb.Monster<ArtFloorFinalDaCapoBoss>().ToMutable(), FinalDaCapoSlot),
            (CreateFinalPerformer(ArtFloorDaCapoPerformerVariant.First), FinalPerformerOneSlot),
            (CreateFinalPerformer(ArtFloorDaCapoPerformerVariant.Second), FinalPerformerTwoSlot),
            (CreateFinalPerformer(ArtFloorDaCapoPerformerVariant.Third), FinalPerformerThreeSlot),
            (CreateFinalPerformer(ArtFloorDaCapoPerformerVariant.Fourth), FinalPerformerFourSlot)
        ];
    }

    private static async Task EnsureFinalDaCapoPerformerPassivePowers(CombatStateLike combatState)
    {
        foreach (Creature performer in combatState.Enemies
            .Where(static enemy => enemy.Monster is ArtFloorDaCapoPerformer)
            .OrderBy(static enemy => enemy.SlotName))
        {
            await ArtFloorDaCapoPerformer.EnsurePerformerPassivePower(performer);
        }
    }

    private static ArtFloorDustbornPerson CreateDustbornPerson(ArtFloorDustbornSide side)
    {
        var person = (ArtFloorDustbornPerson)ModelDb.Monster<ArtFloorDustbornPerson>().ToMutable();
        person.ConfigureSide(side);
        return person;
    }

    private static ArtFloorGalaxyFriend CreateArtFloorGalaxyFriend(ArtFloorGalaxyFriendInitialMove initialMove)
    {
        var friend = (ArtFloorGalaxyFriend)ModelDb.Monster<ArtFloorGalaxyFriend>().ToMutable();
        friend.ConfigureInitialMove(initialMove);
        return friend;
    }

    private static ArtFloorDaCapoPerformer CreateFinalPerformer(ArtFloorDaCapoPerformerVariant variant)
    {
        var performer = (ArtFloorDaCapoPerformer)ModelDb.Monster<ArtFloorDaCapoPerformer>().ToMutable();
        performer.ConfigureVariant(variant);
        return performer;
    }

    internal ArtFloorFinalDaCapoBoss? GetCurrentArtFloorDaCapo()
    {
        return CombatManager.Instance.DebugOnlyGetState()?.Enemies
            .Select(static enemy => enemy.Monster)
            .OfType<ArtFloorFinalDaCapoBoss>()
            .FirstOrDefault();
    }

    private static async Task RemoveOtherPhaseCreatures(CombatStateLike combatState, Creature phaseBoss)
    {
        await LiberationPhaseCleanup.RemovePhaseCreatures(
            combatState,
            except: phaseBoss,
            includeDeadStateCreatures: false);
    }

    private static async Task ClearPhaseTwoPlayerState(CombatStateLike combatState)
    {
        foreach (Creature player in combatState.PlayerCreatures)
        {
            BoundaryThornPower? thorn = player.GetPower<BoundaryThornPower>();
            if (thorn != null)
            {
                await PowerCmd.Remove(thorn);
            }

            FanaticWorshipPower? worship = player.GetPower<FanaticWorshipPower>();
            if (worship != null)
            {
                await PowerCmd.Remove(worship);
            }

            ArtFloorAtonementCrownPower? crown = player.GetPower<ArtFloorAtonementCrownPower>();
            if (crown != null)
            {
                await PowerCmd.Remove(crown);
            }

            ArtFloorFragrancePower? fragrance = player.GetPower<ArtFloorFragrancePower>();
            if (fragrance != null)
            {
                await PowerCmd.Remove(fragrance);
            }

            ArtFloorCollapsePower? collapse = player.GetPower<ArtFloorCollapsePower>();
            if (collapse != null)
            {
                await PowerCmd.Remove(collapse);
            }

            ArtFloorNextTurnCollapsePower? nextTurnCollapse = player.GetPower<ArtFloorNextTurnCollapsePower>();
            if (nextTurnCollapse != null)
            {
                await PowerCmd.Remove(nextTurnCollapse);
            }

            if (player.Player?.PlayerCombatState == null)
            {
                continue;
            }

            await ClearEpiphanyCards(player);
            await ClearPleasureCards(player);
        }
    }

    private static async Task ClearEpiphanyCards(Creature player)
    {
        if (player.Player?.PlayerCombatState == null)
        {
            return;
        }

        IReadOnlyList<CardModel> visibleEpiphanies = player.Player.PlayerCombatState.AllPiles
            .Where(static pile => pile.Type is PileType.Hand or PileType.Play)
            .SelectMany(static pile => pile.Cards)
            .Where(static card => card is CosmicFragmentEpiphanyCard)
            .ToArray();
        if (visibleEpiphanies.Count > 0)
        {
            await CardPileCmd.RemoveFromCombat(visibleEpiphanies);
        }

        IReadOnlyList<CardModel> hiddenEpiphanies = player.Player.PlayerCombatState.AllCards
            .Where(static card => card is CosmicFragmentEpiphanyCard)
            .ToArray();
        if (hiddenEpiphanies.Count > 0)
        {
            await CardPileCmd.RemoveFromCombat(hiddenEpiphanies, skipVisuals: true);
        }
    }

    private static async Task ClearPleasureCards(CombatStateLike combatState)
    {
        foreach (Creature player in combatState.PlayerCreatures)
        {
            await ClearPleasureCards(player);
        }
    }

    private static async Task ClearPleasureCards(Creature player)
    {
        if (player.Player?.PlayerCombatState == null)
        {
            return;
        }

        IReadOnlyList<CardModel> visiblePleasures = player.Player.PlayerCombatState.AllPiles
            .Where(static pile => pile.Type is PileType.Hand or PileType.Play)
            .SelectMany(static pile => pile.Cards)
            .Where(static card => card is PleasureCard)
            .ToArray();
        if (visiblePleasures.Count > 0)
        {
            await CardPileCmd.RemoveFromCombat(visiblePleasures);
        }

        IReadOnlyList<CardModel> hiddenPleasures = player.Player.PlayerCombatState.AllCards
            .Where(static card => card is PleasureCard)
            .ToArray();
        if (hiddenPleasures.Count > 0)
        {
            await CardPileCmd.RemoveFromCombat(hiddenPleasures, skipVisuals: true);
        }
    }
}
