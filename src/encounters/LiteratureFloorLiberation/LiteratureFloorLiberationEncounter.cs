using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.backgrounds.LiteratureFloorLiberation;
using LibraryOfRuina.content.abnormalities.Leticia;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.encounters.HistoryFloorLiberation;
using LibraryOfRuina.events.LiteratureFloorLiberation;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.encounters;
using LibraryOfRuina.interop;
using LibraryOfRuina.monsters.LiteratureFloorLiberation;
using LibraryOfRuina.powers.LiteratureFloorLiberation;
using LibraryOfRuina.scene_transitions;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.encounters.LiteratureFloorLiberation;

public sealed class LiteratureFloorLiberationEncounter :
    LiberationEncounterBase,
    IEncounterBgmSource,
    ILiberationPhaseBgmSource,
    IFloorLiberationEncounter
{
    EncounterBgmConfig IEncounterBgmSource.Bgm => EncounterBgmConfig.PhaseBased(
        "LiteratureFloorLiberationBGM",
        HistoryFloorLiberationEncounter.AngelaLiberationBgmTracks,
        volumeScale: 0.85f);

    public const int PlannedMaxPhase = 5;
    public const int ImplementedMaxPhase = 5;
    public const decimal PhaseTransitionHealAmount = 10m;
    public const string GiftLeftSlot = "gift_left";
    public const string GiftRightSlot = "gift_right";
    public const string LaetitiaSlot = "laetitia";
    public const string EnhancedSpiderLeftSlot = "enhanced_spider_left";
    public const string EnhancedSpiderRightSlot = "enhanced_spider_right";
    public const string RedEyesSlot = "red_eyes";
    public const string EnhancedLeftShoeSlot = "enhanced_left_shoe";
    public const string BloodlustSlot = "bloodlust";
    public const string TodaysExpressionSlot = "todays_expression";
    public const string BlackSwanBrotherSlotOne = "black_swan_brother_1";
    public const string BlackSwanBrotherSlotTwo = "black_swan_brother_2";
    public const string BlackSwanBrotherSlotThree = "black_swan_brother_3";
    public const string BlackSwanBrotherSlotFour = "black_swan_brother_4";
    public const string BlackSwanSlot = "black_swan";

    internal const string EncounterScenePath =
        "res://scenes/encounters/literature_floor_liberation_encounter.tscn";
    internal const string BackgroundScenePath =
        "res://scenes/backgrounds/literature_floor_liberation_encounter/literature_floor_liberation_encounter_background.tscn";
    internal const string BackgroundLayerScenePath =
        "res://scenes/backgrounds/literature_floor_liberation_encounter/layers/literature_floor_liberation_encounter_bg_00_a.tscn";
    internal const string BackgroundTexturePath =
        "res://images/backgrounds/literature_floor_liberation_encounter/creature_map_latitia_composite.png";
    internal const string RedEyesEncounterScenePath =
        "res://scenes/encounters/literature_floor_liberation_red_eyes_encounter.tscn";
    internal const string BloodlustEncounterScenePath =
        "res://scenes/encounters/literature_floor_liberation_bloodlust_encounter.tscn";
    internal const string TodaysExpressionEncounterScenePath =
        "res://scenes/encounters/literature_floor_liberation_todays_expression_encounter.tscn";
    internal const string BlackSwanEncounterScenePath =
        "res://scenes/encounters/literature_floor_liberation_black_swan_encounter.tscn";
    internal const string BossNodeResourcePath =
        "res://images/map/placeholder/literature_floor_liberation_encounter_icon";

    private const string CurrentPhaseKey = "CurrentPhase";
    private const string KilledBossCountKey = "KilledBossCount";
    private const string PhaseCompleteKey = "PhaseComplete";
    private const string TransitionPendingKey = "TransitionPending";
    private const string SettlementTriggeredKey = "SettlementTriggered";
    private const string EndedByLethalDamageKey = "EndedByLethalDamage";
    private const string SuperGiftPendingKey = "SuperGiftPending";
    private const string NormalMovesUntilSuperGiftKey =
        "NormalMovesUntilSuperGift";
    private const string LeftFriendSpawnRoundKey = "LeftFriendSpawnRound";
    private const string LeftFriendWeakenedKey = "LeftFriendWeakened";
    private const string RightFriendSpawnRoundKey = "RightFriendSpawnRound";
    private const string RightFriendWeakenedKey = "RightFriendWeakened";
    private const string BlackSwanRoundStartsKey = "BlackSwanRoundStarts";
    private const string NextBlackSwanBrotherKey = "NextBlackSwanBrother";
    private const float PhaseFiveCameraScaling = 0.82f;
    private static readonly Vector2 PhaseFiveCameraOffset =
        Vector2.Down * 50f + Vector2.Left * 100f;

    private bool _superGiftPending;
    private int _normalMovesUntilSuperGift;
    private int _leftFriendSpawnRound = -1;
    private bool _leftFriendWeakened;
    private int _rightFriendSpawnRound = -1;
    private bool _rightFriendWeakened;
    private int _blackSwanRoundStarts;
    private int _nextBlackSwanBrother = 3;

    public override RoomType RoomType => RoomType.Boss;

    public override bool ShouldGiveRewards => false;

    public override bool HasScene => true;

    public override float GetCameraScaling() =>
        CurrentPhase == 5 ? PhaseFiveCameraScaling : 0.9f;

    public override Vector2 GetCameraOffset() =>
        CurrentPhase == 5
            ? PhaseFiveCameraOffset
            : Vector2.Zero;

    public override IReadOnlyList<string> Slots => CurrentPhase switch
    {
        1 => [GiftLeftSlot, GiftRightSlot, LaetitiaSlot],
        2 => [EnhancedSpiderLeftSlot, EnhancedSpiderRightSlot, RedEyesSlot],
        3 => [EnhancedLeftShoeSlot, BloodlustSlot],
        4 => [TodaysExpressionSlot],
        5 =>
        [
            BlackSwanBrotherSlotOne,
            BlackSwanBrotherSlotTwo,
            BlackSwanBrotherSlotThree,
            BlackSwanBrotherSlotFour,
            BlackSwanSlot
        ],
        _ => []
    };

    protected override bool HasCustomBackground => true;

    public override string BossNodePath => BossNodeResourcePath;

    public int CurrentPhase { get; private set; } = 1;

    public int KilledBossCount { get; private set; }

    public bool PhaseComplete { get; private set; }

    public bool TransitionPending { get; private set; }

    public bool SettlementTriggered { get; private set; }

    public bool EndedByLethalDamage { get; private set; }

    public string LiberationFloorId => LiberationFloorIds.Literature;

    public bool IsFullyLiberated =>
        KilledBossCount >= PlannedMaxPhase;

    internal int BlackSwanRoundStarts => _blackSwanRoundStarts;

    internal int NextBlackSwanBrother => _nextBlackSwanBrother;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<LiteratureFloorSurpriseGiftBox>(),
        ModelDb.Monster<LiteratureFloorLaetitiaBoss>(),
        ModelDb.Monster<LiteratureFloorLittleWitchFriend>(),
        ModelDb.Monster<LiteratureFloorRedEyesBoss>(),
        ModelDb.Monster<LiteratureFloorEnhancedSmallSpider>(),
        ModelDb.Monster<LiteratureFloorEnhancedLeftShoe>(),
        ModelDb.Monster<LiteratureFloorBloodlustBoss>(),
        ModelDb.Monster<LiteratureFloorTodaysExpressionBoss>(),
        ModelDb.Monster<LiteratureFloorBlackSwanBoss>(),
        ModelDb.Monster<LiteratureFloorBlackSwanFirstBrother>(),
        ModelDb.Monster<LiteratureFloorBlackSwanSecondBrother>(),
        ModelDb.Monster<LiteratureFloorBlackSwanThirdBrother>(),
        ModelDb.Monster<LiteratureFloorBlackSwanFourthBrother>(),
        ModelDb.Monster<LiteratureFloorBlackSwanFifthBrother>(),
        ModelDb.Monster<LiteratureFloorBlackSwanSixthBrother>()
    ];

    public override IEnumerable<string> ExtraAssetPaths =>
        ModelDb.Monster<LiteratureFloorSurpriseGiftBox>().AssetPaths
            .Concat(ModelDb.Monster<LiteratureFloorLaetitiaBoss>().AssetPaths)
            .Concat(ModelDb.Monster<LiteratureFloorLittleWitchFriend>().AssetPaths)
            .Concat(ModelDb.Monster<LiteratureFloorRedEyesBoss>().AssetPaths)
            .Concat(ModelDb.Monster<LiteratureFloorEnhancedSmallSpider>().AssetPaths)
            .Concat(ModelDb.Monster<LiteratureFloorEnhancedLeftShoe>().AssetPaths)
            .Concat(ModelDb.Monster<LiteratureFloorBloodlustBoss>().AssetPaths)
            .Concat(ModelDb.Monster<LiteratureFloorTodaysExpressionBoss>().AssetPaths)
            .Concat(ModelDb.Monster<LiteratureFloorBlackSwanBoss>().AssetPaths)
            .Concat(ModelDb.Monster<LiteratureFloorBlackSwanFirstBrother>().AssetPaths)
            .Concat(ModelDb.Monster<LiteratureFloorBlackSwanSecondBrother>().AssetPaths)
            .Concat(ModelDb.Monster<LiteratureFloorBlackSwanThirdBrother>().AssetPaths)
            .Concat(ModelDb.Monster<LiteratureFloorBlackSwanFourthBrother>().AssetPaths)
            .Concat(ModelDb.Monster<LiteratureFloorBlackSwanFifthBrother>().AssetPaths)
            .Concat(ModelDb.Monster<LiteratureFloorBlackSwanSixthBrother>().AssetPaths)
            .Concat(LorexSceneTransitionAssetPaths.All)
            .Concat(
            [
                EncounterScenePath,
                RedEyesEncounterScenePath,
                BloodlustEncounterScenePath,
                TodaysExpressionEncounterScenePath,
                BlackSwanEncounterScenePath,
                BackgroundScenePath,
                BackgroundLayerScenePath,
                BackgroundTexturePath,
                LiteratureFloorLiberationBackgroundController
                    .PhaseThreeTexturePath,
                LiteratureFloorLiberationBackgroundController
                    .PhaseFourTexturePath,
                LiteratureFloorLiberationBackgroundController
                    .PhaseFiveTexturePath,
                "res://images/ui/run_history/literature_floor_liberation_encounter.png",
                "res://images/ui/run_history/literature_floor_liberation_encounter_outline.png",
                BossNodeResourcePath + ".png",
                BossNodeResourcePath + "_outline.png",
                "res://images/powers/library_passive_green.png"
            ])
            .Concat(
                HistoryFloorLiberationEncounter
                    .AngelaLiberationBgmTracks)
            .Distinct();

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        if (PhaseComplete)
        {
            return [];
        }

        return CurrentPhase switch
        {
            1 => CreatePhaseOneMonsters(),
            2 => CreatePhaseTwoMonsters(),
            3 => CreatePhaseThreeMonsters(),
            4 => CreatePhaseFourMonsters(),
            5 => CreatePhaseFiveMonsters(),
            _ => []
        };
    }

    private static IReadOnlyList<(MonsterModel, string?)>
        CreatePhaseOneMonsters() =>
        [
            (
                ModelDb.Monster<LiteratureFloorSurpriseGiftBox>()
                    .ToMutable(),
                GiftLeftSlot),
            (
                ModelDb.Monster<LiteratureFloorSurpriseGiftBox>()
                    .ToMutable(),
                GiftRightSlot),
            (
                ModelDb.Monster<LiteratureFloorLaetitiaBoss>()
                    .ToMutable(),
                LaetitiaSlot)
        ];

    private static IReadOnlyList<(MonsterModel, string?)>
        CreatePhaseTwoMonsters()
    {
        var leftSpider = (LiteratureFloorEnhancedSmallSpider)ModelDb
            .Monster<LiteratureFloorEnhancedSmallSpider>()
            .ToMutable();
        leftSpider.ConfigureOpeningMove(startsWithSharpFangs: true);

        var rightSpider = (LiteratureFloorEnhancedSmallSpider)ModelDb
            .Monster<LiteratureFloorEnhancedSmallSpider>()
            .ToMutable();
        rightSpider.ConfigureOpeningMove(startsWithSharpFangs: false);

        return
        [
            (leftSpider, EnhancedSpiderLeftSlot),
            (rightSpider, EnhancedSpiderRightSlot),
            (
                ModelDb.Monster<LiteratureFloorRedEyesBoss>().ToMutable(),
                RedEyesSlot)
        ];
    }

    private static IReadOnlyList<(MonsterModel, string?)>
        CreatePhaseThreeMonsters() =>
        [
            (
                ModelDb.Monster<LiteratureFloorEnhancedLeftShoe>()
                    .ToMutable(),
                EnhancedLeftShoeSlot),
            (
                ModelDb.Monster<LiteratureFloorBloodlustBoss>()
                    .ToMutable(),
                BloodlustSlot)
        ];

    private static IReadOnlyList<(MonsterModel, string?)>
        CreatePhaseFourMonsters() =>
        [
            (
                ModelDb.Monster<LiteratureFloorTodaysExpressionBoss>()
                    .ToMutable(),
                TodaysExpressionSlot)
        ];

    private static IReadOnlyList<(MonsterModel, string?)>
        CreatePhaseFiveMonsters() =>
        [
            (
                ModelDb.Monster<LiteratureFloorBlackSwanFirstBrother>()
                    .ToMutable(),
                BlackSwanBrotherSlotOne),
            (
                ModelDb.Monster<LiteratureFloorBlackSwanSecondBrother>()
                    .ToMutable(),
                BlackSwanBrotherSlotTwo),
            (
                ModelDb.Monster<LiteratureFloorBlackSwanBoss>()
                    .ToMutable(),
                BlackSwanSlot)
        ];

    public override Dictionary<string, string> SaveCustomState() =>
        new()
        {
            [CurrentPhaseKey] = CurrentPhase.ToString(),
            [KilledBossCountKey] = KilledBossCount.ToString(),
            [PhaseCompleteKey] = PhaseComplete.ToString(),
            [TransitionPendingKey] = TransitionPending.ToString(),
            [SettlementTriggeredKey] = SettlementTriggered.ToString(),
            [EndedByLethalDamageKey] = EndedByLethalDamage.ToString(),
            [SuperGiftPendingKey] = _superGiftPending.ToString(),
            [NormalMovesUntilSuperGiftKey] =
                _normalMovesUntilSuperGift.ToString(),
            [LeftFriendSpawnRoundKey] = _leftFriendSpawnRound.ToString(),
            [LeftFriendWeakenedKey] = _leftFriendWeakened.ToString(),
            [RightFriendSpawnRoundKey] = _rightFriendSpawnRound.ToString(),
            [RightFriendWeakenedKey] = _rightFriendWeakened.ToString(),
            [BlackSwanRoundStartsKey] = _blackSwanRoundStarts.ToString(),
            [NextBlackSwanBrotherKey] = _nextBlackSwanBrother.ToString()
        };

    public override void LoadCustomState(Dictionary<string, string> state)
    {
        var bag = new EncounterStateBag(state);
        CurrentPhase = bag.ReadClampedInt(
            CurrentPhaseKey,
            1,
            1,
            PlannedMaxPhase);
        KilledBossCount = bag.ReadClampedInt(
            KilledBossCountKey,
            0,
            0,
            PlannedMaxPhase);
        PhaseComplete = bag.ReadBool(PhaseCompleteKey);
        TransitionPending = !PhaseComplete
            && bag.ReadBool(TransitionPendingKey);
        SettlementTriggered = bag.ReadBool(SettlementTriggeredKey);
        EndedByLethalDamage = bag.ReadBool(EndedByLethalDamageKey);
        _superGiftPending = bag.ReadBool(SuperGiftPendingKey);
        _normalMovesUntilSuperGift = Math.Max(
            0,
            bag.ReadInt(NormalMovesUntilSuperGiftKey, 0));
        _leftFriendSpawnRound = bag.ReadInt(LeftFriendSpawnRoundKey, -1);
        _leftFriendWeakened = bag.ReadBool(LeftFriendWeakenedKey);
        _rightFriendSpawnRound = bag.ReadInt(RightFriendSpawnRoundKey, -1);
        _rightFriendWeakened = bag.ReadBool(RightFriendWeakenedKey);
        _blackSwanRoundStarts = Math.Max(
            0,
            bag.ReadInt(BlackSwanRoundStartsKey, 0));
        _nextBlackSwanBrother = bag.ReadClampedInt(
            NextBlackSwanBrotherKey,
            3,
            3,
            7);
    }

    internal bool HasLivingGiftBoxes(CombatStateLike? combatState) =>
        combatState?.Enemies.Any(static enemy =>
            enemy.IsAlive
            && enemy.Monster is LiteratureFloorSurpriseGiftBox) == true;

    internal bool IsSuperGiftDue(CombatStateLike? combatState) =>
        !HasLivingGiftBoxes(combatState)
        && (_superGiftPending || _normalMovesUntilSuperGift <= 0);

    internal void CompleteSuperGift()
    {
        _superGiftPending = false;
        _normalMovesUntilSuperGift = 2;
    }

    internal void CompleteNormalGiftMove()
    {
        if (!_superGiftPending && _normalMovesUntilSuperGift > 0)
        {
            _normalMovesUntilSuperGift--;
        }
    }

    internal async Task OnGiftBoxDeath(
        Creature giftBox,
        bool weakenedFriend)
    {
        if (PhaseComplete
            || CurrentPhase != 1
            || giftBox.CombatState is not { } combatState
            || giftBox.SlotName is not (GiftLeftSlot or GiftRightSlot))
        {
            return;
        }

        int spawnRound = combatState.RoundNumber + 1;
        if (giftBox.SlotName == GiftLeftSlot)
        {
            _leftFriendSpawnRound = spawnRound;
            _leftFriendWeakened = weakenedFriend;
        }
        else
        {
            _rightFriendSpawnRound = spawnRound;
            _rightFriendWeakened = weakenedFriend;
        }

        if (HasLivingGiftBoxes(combatState))
        {
            await NotifyLaetitiaRosterChanged(combatState);
            return;
        }

        _superGiftPending = true;
        LiteratureFloorLaetitiaBoss? laetitia = combatState.Enemies
            .Where(static enemy => enemy.IsAlive)
            .Select(static enemy => enemy.Monster)
            .OfType<LiteratureFloorLaetitiaBoss>()
            .FirstOrDefault();
        if (laetitia != null)
        {
            await laetitia.OnAllGiftBoxesDefeated(
                combatState.CurrentSide == CombatSide.Player);
        }
    }

    internal async Task SpawnDueFriends(
        CombatSide side,
        CombatStateLike combatState)
    {
        if (side != CombatSide.Player
            || PhaseComplete
            || CurrentPhase != 1
            || !combatState.Enemies.Any(static enemy =>
                enemy.IsAlive
                && enemy.Monster is LiteratureFloorLaetitiaBoss))
        {
            return;
        }

        await SpawnDueFriend(
            combatState,
            GiftLeftSlot,
            _leftFriendSpawnRound,
            _leftFriendWeakened);
        await SpawnDueFriend(
            combatState,
            GiftRightSlot,
            _rightFriendSpawnRound,
            _rightFriendWeakened);
        await NotifyLaetitiaRosterChanged(combatState);
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
                LiteratureFloorLiberationControllerPower>(player);
        }
    }

    internal async Task OnPhaseBossDeath(
        ILiberationPrimaryPhaseBoss boss,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (wasRemovalPrevented
            || PhaseComplete
            || SettlementTriggered
            || boss.LiberationPhase != CurrentPhase
            || boss.Creature.CombatState is not { } combatState)
        {
            return;
        }

        KilledBossCount = Math.Max(KilledBossCount, boss.LiberationPhase);
        if (boss.LiberationPhase >= ImplementedMaxPhase)
        {
            await CompleteImplementedFinalPhase(
                combatState,
                boss.Creature,
                boss.LiberationPhase);
            return;
        }

        CurrentPhase = boss.LiberationPhase + 1;
        TransitionPending = true;
        PhaseComplete = false;
        _superGiftPending = false;
        _normalMovesUntilSuperGift = 0;
        ClearPendingFriends();
        if (CurrentPhase == LiteratureFloorBlackSwanBoss.Phase)
        {
            ResetBlackSwanPhaseState();
        }

        if (boss.LiberationPhase == 1)
        {
            await ClearAllGifts(combatState);
        }
        else if (boss.LiberationPhase == 2)
        {
            await ClearPhaseTwoPlayerState(combatState);
        }
        else if (boss.LiberationPhase == 3)
        {
            await ClearPhaseThreePlayerState(combatState);
        }

        await LiberationPhaseCleanup.RemovePhaseCreatures(
            combatState,
            except: boss.Creature,
            includeDeadStateCreatures: false);
        await LiberationPhasePlayerRecovery.RestorePlayers(
            combatState,
            PhaseTransitionHealAmount);
        RefreshLiberationPhaseBgm();

        await LiberationPhaseTransition.ShowAsync(
            boss,
            triggerAnimation: true);
        await Cmd.CustomScaledWait(
            Math.Max(0.15f, deathAnimLength),
            Math.Max(0.3f, deathAnimLength));
    }

    public async Task OnBeforeSideTurnStart(
        CombatSide side,
        CombatStateLike combatState)
    {
        await EnsureControllerPowers(combatState);
        if (side == CombatSide.Enemy
            && TransitionPending
            && !PhaseComplete
            && !combatState.Enemies.Any(static enemy =>
                enemy.Monster is ILiberationPhaseBoss))
        {
            await SpawnCurrentPhaseFromTransition(combatState);
        }
    }

    public bool ShouldKeepCombatOpen(CombatStateLike combatState) =>
        LiberationCombatEndGuard.ShouldKeepCombatOpen(
            combatState,
            CurrentPhase,
            TransitionPending,
            PhaseComplete);

    public bool ShouldKeepPhaseBossAfterDeath(Creature creature)
    {
        if (PhaseComplete
            || creature.Monster is not ILiberationPrimaryPhaseBoss boss
            || boss.LiberationPhase >= ImplementedMaxPhase)
        {
            return false;
        }

        return boss.LiberationPhase == CurrentPhase
            || (TransitionPending
                && boss.LiberationPhase + 1 == CurrentPhase);
    }

    public bool ShouldSuppressTransitionBossInteraction(Creature creature) =>
        TransitionPending
        && !PhaseComplete
        && creature.IsDead
        && creature.Monster is ILiberationPhaseBoss boss
        && boss.LiberationPhase + 1 == CurrentPhase;

    public bool ShouldPreventTransitionBossDeath(Creature creature) =>
        TransitionPending
        && !PhaseComplete
        && !SettlementTriggered
        && creature.Monster is ILiberationPrimaryPhaseBoss boss
        && boss.LiberationPhase + 1 == CurrentPhase;

    public bool ShouldPreventPlayerDeath(Creature creature) =>
        creature.IsPlayer
        && IsLastAlivePlayer(creature)
        && (SettlementTriggered
            || KilledBossCount
            >= LiteratureFloorLiberationSettlementStore
                .MinimumKilledBossCount);

    public async Task OnPreventingDeath(Creature creature)
    {
        if (ShouldPreventPlayerDeath(creature))
        {
            await CreatureCmd.SetCurrentHp(creature, 1m);
            if (SettlementTriggered)
            {
                return;
            }

            PhaseComplete = true;
            TransitionPending = false;
            SettlementTriggered = true;
            EndedByLethalDamage = true;
            LiteratureFloorLiberationSettlementStore.Record(this);
            await EndCombatAsLiberationVictory(creature.CombatState);
            return;
        }

        if (ShouldPreventTransitionBossDeath(creature))
        {
            await CreatureCmd.SetCurrentHp(creature, 1m);
        }
    }

    internal async Task CompletePhaseTransition(ILiberationPhaseBoss boss)
    {
        if (!TransitionPending
            || PhaseComplete
            || boss.LiberationPhase + 1 != CurrentPhase
            || boss.Creature.CombatState is not { } combatState)
        {
            return;
        }

        await SpawnCurrentPhaseFromTransition(combatState);
    }

    private async Task SpawnCurrentPhaseFromTransition(
        CombatStateLike combatState)
    {
        await LiberationPhaseCleanup.RemovePhaseCreatures(combatState);

        TextureRect? backgroundImage =
            LiteratureFloorLiberationBackgroundController
                .GetCurrentBackgroundImage();
        if (NCombatRoom.Instance is { } room)
        {
            ApplyCameraForCurrentPhase(room);
        }

        await LorexSceneTransitionController.PlayPhaseBackgroundRevealAsync(
            backgroundImage,
            LiteratureFloorLiberationBackgroundController
                .GetPhaseBackgroundTexturePath(CurrentPhase),
            SpawnCurrentPhaseCreatures,
            () => LiteratureFloorLiberationBackgroundController
                .SetPhaseBackground(CurrentPhase));
        TransitionPending = false;

        async Task SpawnCurrentPhaseCreatures()
        {
            if (NCombatRoom.Instance is { } room)
            {
                ReplaceEncounterSceneForCurrentPhase(room);
            }

            foreach ((MonsterModel monster, string? slot) in
                     CreateCurrentPhaseMonsters())
            {
                Creature spawned = await CreatureCmd.Add(
                    monster,
                    combatState,
                    CombatSide.Enemy,
                    slot);
                spawned.PrepareForNextTurn(combatState.PlayerCreatures);
            }
        }
    }

    private void ApplyCameraForCurrentPhase(NCombatRoom room)
    {
        if (CurrentPhase != 5)
        {
            return;
        }

        Vector2 targetScale = Vector2.One * PhaseFiveCameraScaling;
        if (room.SceneContainer.Scale.IsEqualApprox(targetScale))
        {
            return;
        }

        room.SceneContainer.Scale = targetScale;
        room.SceneContainer.Position += PhaseFiveCameraOffset;
    }

    private IReadOnlyList<(MonsterModel, string?)>
        CreateCurrentPhaseMonsters() => CurrentPhase switch
        {
            2 => CreatePhaseTwoMonsters(),
            3 => CreatePhaseThreeMonsters(),
            4 => CreatePhaseFourMonsters(),
            5 => CreatePhaseFiveMonsters(),
            _ => []
        };

    private async Task CompleteImplementedFinalPhase(
        CombatStateLike combatState,
        Creature finalBossCreature,
        int completedPhase)
    {
        await LiberationPhaseCleanup.RemovePhaseCreatures(
            combatState,
            except: finalBossCreature,
            includeDeadStateCreatures: false);

        CurrentPhase = completedPhase + 1;
        KilledBossCount = Math.Max(KilledBossCount, completedPhase);
        TransitionPending = false;
        PhaseComplete = true;
        SettlementTriggered = true;
        EndedByLethalDamage = false;
        LiteratureFloorLiberationSettlementStore.Record(this);
        ScheduleDeferredWinConditionCheck();
    }

    private static async Task ClearPhaseTwoPlayerState(
        CombatStateLike combatState)
    {
        foreach (Creature player in combatState.PlayerCreatures)
        {
            LiteratureFloorCocoonBindPower? cocoon =
                player.GetPower<LiteratureFloorCocoonBindPower>();
            if (cocoon != null)
            {
                await PowerCmd.Remove(cocoon);
            }
        }
    }

    private static async Task ClearPhaseThreePlayerState(
        CombatStateLike combatState)
    {
        foreach (Creature player in combatState.PlayerCreatures)
        {
            if (player.GetPower<LiteratureFloorDeepWoundPower>()
                is { } deepWound)
            {
                await PowerCmd.Remove(deepWound);
            }

        }
    }

    internal static Control InstantiatePhaseTwoEncounterScene() =>
        PreloadManager.Cache
            .GetScene(RedEyesEncounterScenePath)
            .Instantiate<Control>();

    internal static Control InstantiatePhaseThreeEncounterScene() =>
        PreloadManager.Cache
            .GetScene(BloodlustEncounterScenePath)
            .Instantiate<Control>();

    internal static Control InstantiatePhaseFourEncounterScene() =>
        PreloadManager.Cache
            .GetScene(TodaysExpressionEncounterScenePath)
            .Instantiate<Control>();

    internal static Control InstantiatePhaseFiveEncounterScene() =>
        PreloadManager.Cache
            .GetScene(BlackSwanEncounterScenePath)
            .Instantiate<Control>();

    private void ReplaceEncounterSceneForCurrentPhase(NCombatRoom room)
    {
        if (!VanillaPrivate.CombatRoomEncounterSlots.IsAvailable)
        {
            throw new InvalidOperationException(
                "NCombatRoom.EncounterSlots could not be found.");
        }

        Control? existingSlots =
            VanillaPrivate.CombatRoomEncounterSlots.Get(room) as Control;
        string requiredMarker = CurrentPhase switch
        {
            5 => BlackSwanSlot,
            4 => TodaysExpressionSlot,
            3 => BloodlustSlot,
            _ => RedEyesSlot
        };
        if (existingSlots?.GetNodeOrNull<Marker2D>(requiredMarker) != null)
        {
            return;
        }

        Control parent = existingSlots?.GetParent() as Control
            ?? room.GetNode<Control>("%EnemyContainer");
        if (existingSlots != null
            && GodotObject.IsInstanceValid(existingSlots))
        {
            parent.RemoveChildSafely(existingSlots);
            existingSlots.QueueFreeSafely();
        }

        Control phaseSlots = CurrentPhase switch
        {
            5 => InstantiatePhaseFiveEncounterScene(),
            4 => InstantiatePhaseFourEncounterScene(),
            3 => InstantiatePhaseThreeEncounterScene(),
            2 => InstantiatePhaseTwoEncounterScene(),
            _ => throw new InvalidOperationException(
                "Unsupported Literature floor phase scene: "
                + CurrentPhase)
        };
        phaseSlots.Position -= new Vector2(1920f, 1080f) * 0.5f;
        parent.AddChildSafely(phaseSlots);
        VanillaPrivate.CombatRoomEncounterSlots.Set(room, phaseSlots);
    }

    internal async Task AdvanceBlackSwanRoundAndTrySummon(
        CombatStateLike combatState)
    {
        if (CurrentPhase != LiteratureFloorBlackSwanBoss.Phase
            || PhaseComplete
            || TransitionPending)
        {
            return;
        }

        _blackSwanRoundStarts++;
        if (_blackSwanRoundStarts < 2
            || _nextBlackSwanBrother > 6)
        {
            return;
        }

        int livingBrothers = combatState.Enemies.Count(static enemy =>
            enemy.IsAlive
            && enemy.Monster
                is LiteratureFloorBlackSwanBrotherBase);
        if (livingBrothers >= LiteratureFloorBlackSwanBoss
                .MaxLivingBrothers)
        {
            return;
        }

        string[] brotherSlots =
        [
            BlackSwanBrotherSlotOne,
            BlackSwanBrotherSlotTwo,
            BlackSwanBrotherSlotThree,
            BlackSwanBrotherSlotFour
        ];
        string? freeSlot = brotherSlots.FirstOrDefault(slot =>
            combatState.Enemies.All(enemy =>
                !enemy.IsAlive || enemy.SlotName != slot));
        if (freeSlot == null)
        {
            return;
        }

        MonsterModel brother = CreateBlackSwanBrother(
            _nextBlackSwanBrother);
        _nextBlackSwanBrother++;
        Creature spawned = await CreatureCmd.Add(
            brother,
            combatState,
            CombatSide.Enemy,
            freeSlot);
        spawned.PrepareForNextTurn(combatState.PlayerCreatures);
    }

    private static MonsterModel CreateBlackSwanBrother(
        int brotherNumber) => brotherNumber switch
        {
            3 => ModelDb
                .Monster<LiteratureFloorBlackSwanThirdBrother>()
                .ToMutable(),
            4 => ModelDb
                .Monster<LiteratureFloorBlackSwanFourthBrother>()
                .ToMutable(),
            5 => ModelDb
                .Monster<LiteratureFloorBlackSwanFifthBrother>()
                .ToMutable(),
            6 => ModelDb
                .Monster<LiteratureFloorBlackSwanSixthBrother>()
                .ToMutable(),
            _ => throw new ArgumentOutOfRangeException(
                nameof(brotherNumber),
                brotherNumber,
                "Only Black Swan brothers three through six are summoned.")
        };

    private void ResetBlackSwanPhaseState()
    {
        _blackSwanRoundStarts = 0;
        _nextBlackSwanBrother = 3;
    }

    private async Task SpawnDueFriend(
        CombatStateLike combatState,
        string slot,
        int spawnRound,
        bool weakened)
    {
        if (spawnRound < 0 || combatState.RoundNumber < spawnRound)
        {
            return;
        }

        if (combatState.Enemies.Any(enemy =>
                enemy.IsAlive && enemy.SlotName == slot))
        {
            return;
        }

        ClearPendingFriend(slot);
        var friend = (LiteratureFloorLittleWitchFriend)ModelDb
            .Monster<LiteratureFloorLittleWitchFriend>()
            .ToMutable();
        friend.ConfigureInitialMoveForSlot(slot);
        Creature friendCreature = await CreatureCmd.Add(
            friend,
            combatState,
            CombatSide.Enemy,
            slot);
        if (weakened)
        {
            await CreatureCmd.SetCurrentHp(
                friendCreature,
                CalculateFriendCurrentHp(
                    friendCreature.MaxHp,
                    weakened: true));
        }
    }

    internal static decimal CalculateFriendCurrentHp(
        decimal maxHp,
        bool weakened) =>
        weakened
            ? Math.Max(1m, Math.Ceiling(maxHp * 0.5m))
            : maxHp;

    private static async Task NotifyLaetitiaRosterChanged(
        CombatStateLike combatState)
    {
        LiteratureFloorLaetitiaBoss? laetitia = combatState.Enemies
            .Where(static enemy => enemy.IsAlive)
            .Select(static enemy => enemy.Monster)
            .OfType<LiteratureFloorLaetitiaBoss>()
            .FirstOrDefault();
        if (laetitia != null)
        {
            await laetitia.RefreshLonelyResistance();
        }
    }

    private static async Task ClearAllGifts(CombatStateLike combatState)
    {
        foreach (Creature player in combatState.PlayerCreatures)
        {
            if (player.Player?.PlayerCombatState == null)
            {
                continue;
            }

            IReadOnlyList<CardModel> visibleGifts = player.Player
                .PlayerCombatState
                .AllPiles
                .Where(static pile =>
                    pile.Type is PileType.Hand or PileType.Play)
                .SelectMany(static pile => pile.Cards)
                .Where(static card => card is LeticiaGift)
                .ToArray();
            if (visibleGifts.Count > 0)
            {
                await CardPileCmd.RemoveFromCombat(visibleGifts);
            }

            IReadOnlyList<CardModel> hiddenGifts = player.Player
                .PlayerCombatState
                .AllCards
                .Where(static card => card is LeticiaGift)
                .ToArray();
            if (hiddenGifts.Count > 0)
            {
                await CardPileCmd.RemoveFromCombat(
                    hiddenGifts,
                    skipVisuals: true);
            }
        }
    }

    private void ClearPendingFriends()
    {
        _leftFriendSpawnRound = -1;
        _leftFriendWeakened = false;
        _rightFriendSpawnRound = -1;
        _rightFriendWeakened = false;
    }

    private void ClearPendingFriend(string slot)
    {
        if (slot == GiftLeftSlot)
        {
            _leftFriendSpawnRound = -1;
            _leftFriendWeakened = false;
        }
        else
        {
            _rightFriendSpawnRound = -1;
            _rightFriendWeakened = false;
        }
    }
}
