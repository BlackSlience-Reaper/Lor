using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.cards.SocialFloorLiberation;
using LibraryOfRuina.content.abnormalities.ScarecrowSearchingForWisdom;
using LibraryOfRuina.encounters.LanguageFloorLiberation;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.encounters;
using LibraryOfRuina.monsters.SocialFloorLiberation;
using LibraryOfRuina.powers.SocialFloorLiberation;
using LibraryOfRuina.visuals.SocialFloorLiberation;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.encounters.SocialFloorLiberation;

public enum SocialFloorTrial
{
    Initial = 1,
    Woodsman = 2,
    Scarecrow = 3,
    Lion = 4,
    Home = 5,
    Rage = 6
}

public sealed class SocialFloorLiberationEncounter :
    LiberationEncounterBase,
    IEncounterBgmSource,
    ILiberationPhaseBgmSource,
    IFloorLiberationEncounter
{
    EncounterBgmConfig IEncounterBgmSource.Bgm => EncounterBgmConfig.PhaseBased(
        "SocialFloorLiberationBGM",
        LanguageFloorLiberationEncounter.RolandLiberationBgmTracks,
        volumeScale: 0.85f);

    public const string FalseThroneSlot = "false_throne";
    public const string CrystalSlotOne = "crystal_1";
    public const string CrystalSlotTwo = "crystal_2";
    public const string CrystalSlotThree = "crystal_3";
    public const string FaceSlotOne = "face_1";
    public const string FaceSlotTwo = "face_2";
    public const string FaceSlotThree = "face_3";
    public const string FaceSlotFour = "face_4";

    public static readonly IReadOnlyList<string> CrystalSlots =
    [
        CrystalSlotOne,
        CrystalSlotTwo,
        CrystalSlotThree
    ];

    public static readonly IReadOnlyList<string> FaceSlots =
    [
        FaceSlotOne,
        FaceSlotTwo,
        FaceSlotThree,
        FaceSlotFour
    ];

    internal const string EncounterScenePath =
        "res://scenes/encounters/social_floor_liberation_encounter.tscn";
    internal const string BackgroundScenePath =
        "res://scenes/backgrounds/social_floor_liberation_encounter/social_floor_liberation_encounter_background.tscn";
    internal const string BossNodeResourcePath =
        "res://images/map/placeholder/social_floor_liberation_encounter_icon";

    private const string StateVersionKey = "SocialFloorStateVersion";
    private const string TrialKey = "SocialFloorTrial";
    private const string TrialRoundKey = "SocialFloorTrialRound";
    private const string SetupCompleteKey = "SocialFloorSetupComplete";
    private const string PlayersHealedKey = "SocialFloorPlayersHealed";
    private const string DestroyedCrystalMaskKey =
        "SocialFloorDestroyedCrystalMask";
    private const string DestroyedFaceMaskKey =
        "SocialFloorDestroyedFaceMask";
    private const string PendingTrialKey = "SocialFloorPendingTrial";
    private const string HasPendingTrialKey =
        "SocialFloorHasPendingTrial";
    private const string ScaredyCatPlayerNetIdKey =
        "SocialFloorScaredyCatPlayerNetId";
    private const string OzmaPlayerNetIdKey =
        "SocialFloorOzmaPlayerNetId";
    private const string CowardAppliedKey = "SocialFloorCowardApplied";
    private const string OzmaReplacementPendingKey =
        "SocialFloorOzmaReplacementPending";
    private const string PowderCostKey = "SocialFloorPowderCost";
    private const string TransformedKey = "SocialFloorTransformed";
    private const string FinalStrikeTriggeredKey =
        "SocialFloorFinalStrikeTriggered";
    private const string PlannedMoveKey = "SocialFloorPlannedMove";
    private const string ParticipantCountKey = "SocialFloorParticipantCount";
    private const string BossHpKey = "SocialFloorBossHp";
    private const string BossChaoKey = "SocialFloorBossChao";
    private const string SummonVitalsKey = "SocialFloorSummonVitals";
    private const string WisdomStacksKey = "SocialFloorWisdomStacks";
    private const string WisdomCardsKey = "SocialFloorWisdomCards";
    private const string LionCardsSubmittedKey =
        "SocialFloorLionCardsSubmitted";
    private const string CouragePlayedKey = "SocialFloorCouragePlayed";
    private const string CouragePowerPresentKey =
        "SocialFloorCouragePowerPresent";
    private const string CouragePendingActivationsKey =
        "SocialFloorCouragePendingActivations";
    private const string CourageEnergyActiveKey =
        "SocialFloorCourageEnergyActive";
    private const string CourageRemoveAtTurnEndKey =
        "SocialFloorCourageRemoveAtTurnEnd";
    private const int CurrentStateVersion = 2;
    private const int AllCrystalsMask = 0b111;
    private const int AllFacesMask = 0b1111;
    internal const int NormalWisdomCardCount = 5;
    internal const int ToughWisdomCardCount = 4;
    internal const int NormalWisdomRequirement = 3;
    internal const int ToughWisdomRequirement = 2;
    internal const int ScarecrowPenaltyHpLossPercent = 60;
    internal const int InitialPowderCost = 10;

    private const float EncounterCameraScaling = 0.82f;
    private static readonly Vector2 EncounterCameraOffset =
        Vector2.Down * 50f + Vector2.Left * 100f;

    public SocialFloorTrial Trial { get; private set; } =
        SocialFloorTrial.Initial;

    public int TrialRound { get; private set; }

    public bool SetupComplete { get; private set; }

    public bool PlayersHealed { get; private set; }

    public int DestroyedCrystalMask { get; internal set; }

    public int DestroyedFaceMask { get; internal set; }

    public bool HasPendingTrial { get; private set; }

    public SocialFloorTrial PendingTrial { get; private set; } =
        SocialFloorTrial.Initial;

    public ulong? ScaredyCatPlayerNetId { get; internal set; }

    public ulong? OzmaPlayerNetId { get; internal set; }

    public bool CowardApplied { get; private set; }

    public bool OzmaReplacementPending { get; internal set; }

    public int PowderCost { get; internal set; } = InitialPowderCost;

    public bool Transformed { get; private set; }

    public bool FinalStrikeTriggered { get; private set; }

    public FalseThroneMove PlannedMove { get; internal set; } =
        FalseThroneMove.InitialSequence;

    public int ParticipantCount { get; private set; } = 1;

    public int SavedBossHp { get; private set; } = -1;

    public int SavedBossChao { get; private set; } = -1;

    public string SavedSummonVitals { get; private set; } = string.Empty;

    public string SavedWisdomStacks { get; internal set; } = string.Empty;

    public string SavedWisdomCards { get; internal set; } = string.Empty;

    public int LionCardsSubmitted { get; internal set; }

    public bool CouragePlayed { get; internal set; }

    public bool CouragePowerPresent { get; internal set; }

    public int CouragePendingActivations { get; internal set; }

    public bool CourageEnergyActive { get; internal set; }

    public bool CourageRemoveAtTurnEnd { get; internal set; }

    private bool _loadedFromCustomState;
    private bool _runtimeStateRestored;
    private bool _enemyVitalsRestored;
    private bool _enemyStunStateRestored;

    public int CurrentPhase => (int)Trial;

    public string LiberationFloorId =>
        LiberationFloorIds.Social;

    public bool IsFullyLiberated => true;

    public int DestroyedCrystalCount =>
        CountBits(DestroyedCrystalMask & AllCrystalsMask);

    public int DestroyedFaceCount =>
        CountBits(DestroyedFaceMask & AllFacesMask);

    public bool AreAllCrystalsDestroyed =>
        (DestroyedCrystalMask & AllCrystalsMask) == AllCrystalsMask;

    public bool AreAllFacesDestroyed =>
        (DestroyedFaceMask & AllFacesMask) == AllFacesMask;

    internal bool ShouldKeepWoodsmanSummons =>
        Trial == SocialFloorTrial.Woodsman
        && PlannedMove != FalseThroneMove.BigMistake;

    public override RoomType RoomType => RoomType.Boss;

    public override bool ShouldGiveRewards => true;

    public override bool HasScene => true;

    protected override bool HasCustomBackground => true;

    public override MegaSkeletonDataResource? BossNodeSpineResource => null;

    public override string BossNodePath => BossNodeResourcePath;

    public override float GetCameraScaling() => EncounterCameraScaling;

    public override Vector2 GetCameraOffset() => EncounterCameraOffset;

    public override IReadOnlyList<string> Slots =>
    [
        FalseThroneSlot,
        ..CrystalSlots,
        ..FaceSlots
    ];

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<FalseThrone>(),
        ModelDb.Monster<EmeraldCrystal>(),
        ModelDb.Monster<ScowlingFace>()
    ];

    public override IEnumerable<string> ExtraAssetPaths =>
        ModelDb.Monster<FalseThrone>().AssetPaths
            .Concat(ModelDb.Monster<EmeraldCrystal>().AssetPaths)
            .Concat(ModelDb.Monster<ScowlingFace>().AssetPaths)
            .Concat(
            [
                EncounterScenePath,
                BackgroundScenePath,
                "res://scenes/backgrounds/social_floor_liberation_encounter/layers/social_floor_liberation_encounter_bg_00_a.tscn",
                "res://images/ui/run_history/social_floor_liberation_encounter.png",
                "res://images/ui/run_history/social_floor_liberation_encounter_outline.png",
                BossNodeResourcePath + ".png",
                BossNodeResourcePath + "_outline.png"
            ])
            .Concat(SocialFloorLiberationVfx.AssetPaths)
            .Concat(LanguageFloorLiberationEncounter.RolandLiberationBgmTracks)
            .Distinct();

    protected override IReadOnlyList<(MonsterModel, string?)>
        GenerateMonsters()
    {
        List<(MonsterModel, string?)> monsters =
        [
            (ModelDb.Monster<FalseThrone>().ToMutable(), FalseThroneSlot)
        ];
        if (!SetupComplete)
        {
            return monsters;
        }

        CurrentTrial.AddRoomSummons(this, monsters);
        return monsters;
    }

    public override Dictionary<string, string> SaveCustomState()
    {
        CaptureRuntimeState();
        return new Dictionary<string, string>
        {
            [StateVersionKey] = EncounterStateBag.FormatInvariant(CurrentStateVersion),
            [TrialKey] = EncounterStateBag.FormatInvariant((int)Trial),
            [TrialRoundKey] = EncounterStateBag.FormatInvariant(TrialRound),
            [SetupCompleteKey] = SetupComplete.ToString(),
            [PlayersHealedKey] = PlayersHealed.ToString(),
            [DestroyedCrystalMaskKey] = EncounterStateBag.FormatInvariant(DestroyedCrystalMask),
            [DestroyedFaceMaskKey] = EncounterStateBag.FormatInvariant(DestroyedFaceMask),
            [HasPendingTrialKey] = HasPendingTrial.ToString(),
            [PendingTrialKey] = EncounterStateBag.FormatInvariant((int)PendingTrial),
            [ScaredyCatPlayerNetIdKey] = EncounterStateBag.FormatInvariant(
                ScaredyCatPlayerNetId),
            [OzmaPlayerNetIdKey] = EncounterStateBag.FormatInvariant(OzmaPlayerNetId),
            [CowardAppliedKey] = CowardApplied.ToString(),
            [OzmaReplacementPendingKey] =
                OzmaReplacementPending.ToString(),
            [PowderCostKey] = EncounterStateBag.FormatInvariant(PowderCost),
            [TransformedKey] = Transformed.ToString(),
            [FinalStrikeTriggeredKey] = FinalStrikeTriggered.ToString(),
            [PlannedMoveKey] = EncounterStateBag.FormatInvariant((int)PlannedMove),
            [ParticipantCountKey] = EncounterStateBag.FormatInvariant(ParticipantCount),
            [BossHpKey] = EncounterStateBag.FormatInvariant(SavedBossHp),
            [BossChaoKey] = EncounterStateBag.FormatInvariant(SavedBossChao),
            [SummonVitalsKey] = SavedSummonVitals,
            [WisdomStacksKey] = SavedWisdomStacks,
            [WisdomCardsKey] = SavedWisdomCards,
            [LionCardsSubmittedKey] = EncounterStateBag.FormatInvariant(LionCardsSubmitted),
            [CouragePlayedKey] = CouragePlayed.ToString(),
            [CouragePowerPresentKey] = CouragePowerPresent.ToString(),
            [CouragePendingActivationsKey] =
                EncounterStateBag.FormatInvariant(CouragePendingActivations),
            [CourageEnergyActiveKey] = CourageEnergyActive.ToString(),
            [CourageRemoveAtTurnEndKey] =
                CourageRemoveAtTurnEnd.ToString()
        };
    }

    public override void LoadCustomState(Dictionary<string, string> state)
    {
        var bag = new EncounterStateBag(state);
        Trial = bag.ReadInvariantEnum(TrialKey, SocialFloorTrial.Initial);
        TrialRound = Math.Clamp(
            bag.ReadInvariantInt(TrialRoundKey),
            0,
            1_000_000);
        SetupComplete = bag.ReadBool(SetupCompleteKey);
        PlayersHealed = bag.ReadBool(PlayersHealedKey);
        DestroyedCrystalMask =
            bag.ReadInvariantInt(DestroyedCrystalMaskKey) & AllCrystalsMask;
        DestroyedFaceMask =
            bag.ReadInvariantInt(DestroyedFaceMaskKey) & AllFacesMask;
        HasPendingTrial = bag.ReadBool(HasPendingTrialKey);
        PendingTrial = bag.ReadInvariantEnum(PendingTrialKey, Trial);
        ScaredyCatPlayerNetId = bag.ReadInvariantNullableUlong(ScaredyCatPlayerNetIdKey);
        OzmaPlayerNetId = bag.ReadInvariantNullableUlong(OzmaPlayerNetIdKey);
        CowardApplied = bag.ReadBool(CowardAppliedKey);
        OzmaReplacementPending = bag.ReadBool(OzmaReplacementPendingKey);
        PowderCost = Math.Max(
            0,
            bag.ReadInvariantInt(PowderCostKey, InitialPowderCost));
        Transformed = bag.ReadBool(TransformedKey);
        FinalStrikeTriggered = bag.ReadBool(FinalStrikeTriggeredKey);
        PlannedMove = bag.ReadInvariantEnum(
            PlannedMoveKey,
            ResolveDefaultMove(Trial, TrialRound));
        ParticipantCount = Math.Clamp(
            bag.ReadInvariantInt(ParticipantCountKey, 1),
            1,
            4);
        SavedBossHp = bag.ReadInvariantInt(BossHpKey, -1);
        SavedBossChao = bag.ReadInvariantInt(BossChaoKey, -1);
        SavedSummonVitals = bag.ReadString(SummonVitalsKey);
        SavedWisdomStacks = bag.ReadString(WisdomStacksKey);
        SavedWisdomCards = bag.ReadString(WisdomCardsKey);
        LionCardsSubmitted = Math.Clamp(
            bag.ReadInvariantInt(LionCardsSubmittedKey),
            0,
            SocialFloorScaredyCatPower.CardLimit);
        CouragePlayed = bag.ReadBool(CouragePlayedKey);
        CouragePowerPresent = bag.ReadBool(CouragePowerPresentKey);
        CouragePendingActivations = Math.Clamp(
            bag.ReadInvariantInt(CouragePendingActivationsKey),
            0,
            SocialFloorCouragePower.TotalActivations);
        CourageEnergyActive = bag.ReadBool(CourageEnergyActiveKey);
        CourageRemoveAtTurnEnd = bag.ReadBool(CourageRemoveAtTurnEndKey);
        if (HasPendingTrial && PendingTrial <= Trial)
        {
            HasPendingTrial = false;
            PendingTrial = Trial;
        }
        if (Trial != SocialFloorTrial.Initial && !SetupComplete)
        {
            // A room save is reconstructed before combat commands are legal.
            // Treat an interrupted phase setup as complete so GenerateMonsters
            // supplies its summons and the first safe player boundary restores
            // cards and powers without calling CreatureCmd.Add too early.
            SetupComplete = true;
        }
        if (Trial != SocialFloorTrial.Rage)
        {
            OzmaReplacementPending = false;
            Transformed = false;
            FinalStrikeTriggered = false;
        }
        else if (!Transformed)
        {
            FinalStrikeTriggered = false;
        }
        _loadedFromCustomState = true;
        _runtimeStateRestored = false;
        _enemyVitalsRestored = false;
        _enemyStunStateRestored = false;
    }

    internal async Task InitializeCombat(
        PlayerChoiceContext choiceContext,
        FalseThrone boss)
    {
        CombatStateLike? combatState = boss.Creature.CombatState;
        if (combatState == null)
        {
            return;
        }

        ParticipantCount = Math.Max(1, combatState.Players.Count);

        if (!PlayersHealed)
        {
            PlayersHealed = true;
            foreach (Creature player in LivingPlayers(combatState))
            {
                await CreatureCmd.Heal(player, player.MaxHp);
            }
        }

        await EnsureTrialSetup(choiceContext, boss, combatState);
        RestoreEnemyVitalsAfterLoad(boss, combatState);
    }

    internal async Task OnBeforeEnemyTurn(
        PlayerChoiceContext choiceContext,
        FalseThrone boss,
        CombatStateLike combatState)
    {
        bool isDeferredTransition =
            CurrentTrial.DefersPendingTrialToEnemyTurnEnd(this);
        if (HasPendingTrial && !isDeferredTransition)
        {
            await EnterPendingTrial(choiceContext, boss, combatState);
        }

        await EnsureTrialSetup(choiceContext, boss, combatState);
        if (!boss.Creature.IsStunned)
        {
            boss.ForcePlannedMove(PlannedMove);
        }
    }

    internal async Task OnBeforePlayerTurn(
        FalseThrone boss,
        CombatStateLike combatState)
    {
        await RestoreRuntimeStateAfterLoad(boss, combatState);
        await CurrentTrial.BeforePlayerTurnAsync(this, boss, combatState);
    }

    internal async Task OnAfterEnemyTurn(
        PlayerChoiceContext choiceContext,
        FalseThrone boss,
        CombatStateLike combatState)
    {
        TrialRound++;
        await CurrentTrial.AfterEnemyTurnAsync(
            this,
            choiceContext,
            boss,
            combatState);
    }

    internal async Task NotifyCrystalDestroyed(
        EmeraldCrystal crystal)
    {
        if (Trial != SocialFloorTrial.Woodsman
            || crystal.Creature.SlotName is not { } slot)
        {
            return;
        }

        int index = IndexOfSlot(CrystalSlots, slot);
        int bit = index < 0 ? 0 : 1 << index;
        if (bit == 0 || (DestroyedCrystalMask & bit) != 0)
        {
            return;
        }

        DestroyedCrystalMask |= bit;
        if (crystal.Creature.CombatState is not { } combatState)
        {
        }

        // foreach (var player in combatState.Players
        //              .Where(static player => player.Creature.IsAlive)
        //              .OrderBy(static player => player.NetId))
        // {
        //     await PlayerCmd.GainEnergy(1m, player);
        // }
    }

    internal Task NotifyFaceDestroyed(ScowlingFace face)
    {
        if (Trial != SocialFloorTrial.Lion
            || face.Creature.SlotName is not { } slot)
        {
            return Task.CompletedTask;
        }

        int index = IndexOfSlot(FaceSlots, slot);
        int bit = index < 0 ? 0 : 1 << index;
        if (bit == 0 || (DestroyedFaceMask & bit) != 0)
        {
            return Task.CompletedTask;
        }

        DestroyedFaceMask |= bit;
        if (AreAllFacesDestroyed)
        {
            QueueTrial(SocialFloorTrial.Home);
        }

        return Task.CompletedTask;
    }

    internal async Task NotifyCreatureDeath(Creature creature)
    {
        if (!creature.IsPlayer)
        {
            return;
        }

        ulong netId = creature.Player!.NetId;
        await CurrentTrial.OnPlayerDiedAsync(this, creature.Player, netId);
    }

    internal void QueueRageTrial()
    {
        if (Trial == SocialFloorTrial.Home)
        {
            QueueTrial(SocialFloorTrial.Rage);
        }
    }

    internal void MarkPowderCost(int cost) =>
        PowderCost = Math.Max(0, cost);

    internal void MarkCouragePlayed(ulong holderNetId)
    {
        if (ScaredyCatPlayerNetId == holderNetId)
        {
            CouragePlayed = true;
        }
    }

    internal void MarkLionCardsSubmitted(
        ulong holderNetId,
        int cardsSubmitted)
    {
        if (Trial == SocialFloorTrial.Lion
            && ScaredyCatPlayerNetId == holderNetId)
        {
            LionCardsSubmitted = Math.Clamp(
                cardsSubmitted,
                0,
                SocialFloorScaredyCatPower.CardLimit);
        }
    }

    internal void MarkTransformed()
    {
        if (Trial == SocialFloorTrial.Rage)
        {
            Transformed = true;
        }
    }

    internal bool TryMarkFinalStrikeTriggered()
    {
        if (!Transformed || FinalStrikeTriggered)
        {
            return false;
        }

        FinalStrikeTriggered = true;
        return true;
    }

    internal bool IsScaredyCatHolder(Player player) =>
        ScaredyCatPlayerNetId == player.NetId;

    internal bool IsOzmaHolder(Player player) =>
        OzmaPlayerNetId == player.NetId;

    internal static int ResolveFaceHpForPlayerCount(int playerCount) =>
        Math.Clamp(playerCount, 1, 4) switch
        {
            1 => 50,
            2 => 55,
            3 => 65,
            _ => 75
        };

    /// <summary>
    /// 当前试炼的规则。每个调用点都按当时的 <see cref="Trial"/> 重新取，
    /// 与原来在各处分别判断 <c>Trial</c> 的时机相同。
    /// </summary>
    private SocialTrial CurrentTrial => SocialTrial.For(Trial);

    private async Task EnsureTrialSetup(
        PlayerChoiceContext choiceContext,
        FalseThrone boss,
        CombatStateLike combatState)
    {
        if (SetupComplete)
        {
            await CurrentTrial.ResumeAsync(boss);
            await EnsureMissingTrialSummons(combatState);
            await boss.SyncTrialPowers();
            return;
        }

        SetupComplete = true;
        await CurrentTrial.SetupAsync(this, boss, combatState);

        await boss.SyncTrialPowers();
        boss.ForcePlannedMove(PlannedMove);
        RefreshLiberationPhaseBgm();
    }

    private async Task EnsureMissingTrialSummons(
        CombatStateLike combatState)
    {
        if (!CombatManager.Instance.IsInProgress)
        {
            return;
        }
        await CurrentTrial.SpawnMissingSummonsAsync(this, combatState);
    }

    internal async Task EnterPendingTrial(
        PlayerChoiceContext choiceContext,
        FalseThrone boss,
        CombatStateLike combatState)
    {
        if (!HasPendingTrial)
        {
            return;
        }

        SocialFloorTrial previous = Trial;
        SocialFloorTrial next = PendingTrial;
        HasPendingTrial = false;
        PendingTrial = next;
        Trial = next;

        await CleanupPreviousTrialState(previous, combatState);

        TrialRound = 0;
        SetupComplete = false;
        PlannedMove = ResolveDefaultMove(Trial, TrialRound);
        await EnsureTrialSetup(choiceContext, boss, combatState);
    }

    internal async Task CleanupPreviousTrialState(
        SocialFloorTrial previous,
        CombatStateLike combatState)
    {
        await SocialTrial.For(previous).CleanupAsync(this, combatState);
    }

    internal void QueueTrial(SocialFloorTrial next)
    {
        if (next <= Trial)
        {
            return;
        }

        HasPendingTrial = true;
        PendingTrial = next;
    }

    internal async Task ApplyCowardToAllLivingPlayers()
    {
        if (CowardApplied
            || FindCombatState() is not { } combatState)
        {
            return;
        }

        CowardApplied = true;
        foreach (var player in combatState.Players.Where(p => p.Creature.IsAlive))
            await SocialFloorPlayerMechanics.ApplyCoward(player);
    }

    private void CaptureRuntimeState()
    {
        if (FindCombatState() is not { } combatState)
        {
            return;
        }

        ParticipantCount = Math.Max(1, combatState.Players.Count);
        Creature? boss = combatState.Enemies.FirstOrDefault(
            static enemy => enemy.Monster is FalseThrone);
        if (boss != null)
        {
            SavedBossHp = boss.CurrentHp;
            SavedBossChao = boss is
                LibraryCreature libraryBoss
                    ? libraryBoss.CurrentChaoValue
                    : -1;
        }

        SavedSummonVitals = SerializeSummonVitals(combatState);
        SavedWisdomStacks = SerializePlayerValues(
            combatState.Players.Select(player =>
                new KeyValuePair<ulong, int>(
                    player.NetId,
                    SocialFloorPlayerMechanics.GetWisdomStacks(player))));
        SavedWisdomCards = SerializePlayerValues(
            combatState.Players.Select(player =>
                new KeyValuePair<ulong, int>(
                    player.NetId,
                    CountRemainingWisdomCards(player))));

        Player? catHolder = ResolvePlayer(
            combatState,
            ScaredyCatPlayerNetId);
        if (catHolder?.Creature.GetPower<SocialFloorScaredyCatPower>()
            is { } catPower)
        {
            LionCardsSubmitted = Math.Clamp(
                catPower.CardsSubmittedThisTurn,
                0,
                SocialFloorScaredyCatPower.CardLimit);
        }

        SocialFloorCouragePower? courage = catHolder?.Creature
            .GetPower<SocialFloorCouragePower>();
        if (courage != null
            || catHolder?.PlayerCombatState?.AllCards.Any(card =>
                card is SocialFloorCourageCard
                && card.Pile?.Type == PileType.Exhaust) == true)
        {
            CouragePlayed = true;
        }
        CouragePowerPresent = courage != null;
        CouragePendingActivations = Math.Clamp(
            courage?.PendingTurnStartActivations ?? 0,
            0,
            SocialFloorCouragePower.TotalActivations);
        CourageEnergyActive = courage?.IsEnergyOverrideActive ?? false;
        CourageRemoveAtTurnEnd =
            courage?.RemoveAtNextPlayerTurnEnd ?? false;

        Player? ozmaHolder = ResolvePlayer(combatState, OzmaPlayerNetId);
        if (ozmaHolder != null
            && SocialFloorPlayerMechanics.GetMagicalPowderCost(ozmaHolder)
                is { } currentPowderCost)
        {
            PowderCost = Math.Max(0, currentPowderCost);
        }
    }

    private async Task RestoreRuntimeStateAfterLoad(
        FalseThrone boss,
        CombatStateLike combatState)
    {
        if (!_loadedFromCustomState || _runtimeStateRestored)
        {
            return;
        }

        _runtimeStateRestored = true;
        RestoreEnemyStunStateAfterLoad(combatState);

        await CurrentTrial.RestoreBeforeSharedStateAsync(this, boss, combatState);

        // 勇气与懦弱不属于某一个试炼：勇气能力可能在狮子试炼之后仍在，懦弱整场只施加一次。
        Player? catHolder = ResolveLivingPlayer(
            combatState,
            ScaredyCatPlayerNetId);
        if (CouragePowerPresent && catHolder != null)
        {
            await SocialFloorPlayerMechanics.RestoreCourage(
                catHolder,
                CouragePendingActivations,
                CourageEnergyActive,
                CourageRemoveAtTurnEnd);
        }

        if (CowardApplied)
        {
            foreach (Player player in combatState.Players
                         .Where(static player => player.Creature.IsAlive))
            {
                await SocialFloorPlayerMechanics.RestoreCoward(player);
            }
        }

        await CurrentTrial.RestoreAfterSharedStateAsync(this, combatState);
    }

    private void RestoreEnemyVitalsAfterLoad(
        FalseThrone boss,
        CombatStateLike combatState)
    {
        if (!_loadedFromCustomState || _enemyVitalsRestored)
        {
            return;
        }
        _enemyVitalsRestored = true;

        if (SavedBossHp >= 0)
        {
            boss.Creature.SetCurrentHpInternal(Math.Clamp(
                SavedBossHp,
                0,
                boss.Creature.MaxHp));
        }
        if (SavedBossChao >= 0
            && boss.Creature is
                LibraryCreature libraryBoss)
        {
            RestoreChaoValueEarly(libraryBoss, SavedBossChao);
        }

        IReadOnlyDictionary<string, (int Hp, int Chao)> summonVitals =
            ParseSummonVitals(SavedSummonVitals);
        foreach (Creature summon in combatState.Enemies.Where(
                     static enemy => enemy.Monster
                         is EmeraldCrystal or ScowlingFace))
        {
            if (summon.SlotName is not { } slot
                || !summonVitals.TryGetValue(slot, out var vitals))
            {
                continue;
            }

            summon.SetCurrentHpInternal(Math.Clamp(
                vitals.Hp,
                1,
                summon.MaxHp));
            if (summon is
                LibraryCreature librarySummon)
            {
                RestoreChaoValueEarly(librarySummon, vitals.Chao);
            }
        }
    }

    private void RestoreEnemyStunStateAfterLoad(
        CombatStateLike combatState)
    {
        if (_enemyStunStateRestored)
        {
            return;
        }
        _enemyStunStateRestored = true;

        foreach (LibraryCreature creature in
                 combatState.Enemies
                     .OfType<LibraryCreature>()
                     .Where(static creature => creature.IsAlive
                         && creature.MaxChaoValue > 0
                         && creature.CurrentChaoValue == 0))
        {
            string? nextMoveId = creature.Monster?.NextMove?.Id;
            if (string.IsNullOrEmpty(nextMoveId))
            {
                continue;
            }
            creature.StunInternal(
                static _ => Task.CompletedTask,
                nextMoveId);
        }
    }

    private static void RestoreChaoValueEarly(
        LibraryCreature creature,
        int value)
    {
        int restored = Math.Clamp(value, 0, creature.MaxChaoValue);
        creature.SetCurrentChaoValueInternal(restored);
    }

    private static int CountRemainingWisdomCards(Player player) =>
        player.PlayerCombatState?.AllCards.Count(card =>
            card is ScarecrowWisdomStatusCard
            && card.Pile?.Type != PileType.Exhaust) ?? 0;

    private static string SerializePlayerValues(
        IEnumerable<KeyValuePair<ulong, int>> values) =>
        string.Join(
            ";",
            values.OrderBy(static entry => entry.Key).Select(entry =>
                entry.Key.ToString(CultureInfo.InvariantCulture)
                + ":"
                + Math.Max(0, entry.Value).ToString(
                    CultureInfo.InvariantCulture)));

    internal static IReadOnlyDictionary<ulong, int> ParsePlayerValues(
        string serialized)
    {
        var values = new Dictionary<ulong, int>();
        foreach (string entry in serialized.Split(
                     ';',
                     StringSplitOptions.RemoveEmptyEntries
                     | StringSplitOptions.TrimEntries))
        {
            string[] parts = entry.Split(':', 2);
            if (parts.Length == 2
                && ulong.TryParse(
                    parts[0],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out ulong netId)
                && int.TryParse(
                    parts[1],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int value))
            {
                values[netId] = Math.Max(0, value);
            }
        }
        return values;
    }

    private static string SerializeSummonVitals(
        CombatStateLike combatState) =>
        string.Join(
            ";",
            combatState.Enemies
                .Where(static enemy => enemy.IsAlive
                    && enemy.Monster is EmeraldCrystal or ScowlingFace
                    && enemy.SlotName != null)
                .OrderBy(static enemy => enemy.SlotName)
                .Select(enemy =>
                {
                    int chao = enemy is
                        LibraryCreature library
                            ? library.CurrentChaoValue
                            : -1;
                    return enemy.SlotName
                        + ":"
                        + enemy.CurrentHp.ToString(CultureInfo.InvariantCulture)
                        + ":"
                        + chao.ToString(CultureInfo.InvariantCulture);
                }));

    private static IReadOnlyDictionary<string, (int Hp, int Chao)>
        ParseSummonVitals(string serialized)
    {
        var values = new Dictionary<string, (int Hp, int Chao)>(
            StringComparer.Ordinal);
        foreach (string entry in serialized.Split(
                     ';',
                     StringSplitOptions.RemoveEmptyEntries
                     | StringSplitOptions.TrimEntries))
        {
            string[] parts = entry.Split(':', 3);
            if (parts.Length == 3
                && (CrystalSlots.Contains(parts[0], StringComparer.Ordinal)
                    || FaceSlots.Contains(parts[0], StringComparer.Ordinal))
                && int.TryParse(
                    parts[1],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int hp)
                && int.TryParse(
                    parts[2],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int chao)
                && hp > 0
                && chao >= 0)
            {
                values[parts[0]] = (hp, chao);
            }
        }
        return values;
    }

    private CombatStateLike? FindCombatState()
    {
        foreach (FalseThrone boss in MonstersWithSlots
                     .Select(static entry => entry.Item1)
                     .OfType<FalseThrone>())
        {
            try
            {
                if (boss.Creature.CombatState is { } combatState)
                {
                    return combatState;
                }
            }
            catch (InvalidOperationException)
            {
                // Generated encounter models are not always bound to a
                // Creature yet (for example during load normalization).
            }
        }

        return null;
    }

    internal static Player? ResolveLivingPlayer(
        CombatStateLike combatState,
        ulong? netId) =>
        netId is null
            ? null
            : combatState.Players.FirstOrDefault(player =>
                player.NetId == netId.Value
                && player.Creature.IsAlive);

    private static Player? ResolvePlayer(
        CombatStateLike combatState,
        ulong? netId) =>
        netId is null
            ? null
            : combatState.Players.FirstOrDefault(player =>
                player.NetId == netId.Value);

    internal static Player? ChooseRandomLivingPlayer(
        CombatStateLike combatState)
    {
        Player[] players = combatState.Players
            .Where(static player => player.Creature.IsAlive)
            .OrderBy(static player => player.NetId)
            .ToArray();
        return players.Length == 0
            ? null
            : players[combatState.RunState.Rng.CombatTargets.NextInt(
                players.Length)];
    }

    internal static IReadOnlyList<Creature> LivingPlayers(
        CombatStateLike combatState) =>
        combatState.PlayerCreatures
            .Where(static player => player.IsAlive)
            .OrderBy(static player => player.CombatId)
            .ToArray();

    private static FalseThroneMove ResolveDefaultMove(
        SocialFloorTrial trial,
        int round) =>
        SocialTrial.For(trial).ResolveDefaultMove(round);

    private static int CountBits(int value)
    {
        int count = 0;
        while (value != 0)
        {
            count += value & 1;
            value >>= 1;
        }
        return count;
    }

    private static int IndexOfSlot(
        IReadOnlyList<string> slots,
        string slot)
    {
        for (int index = 0; index < slots.Count; index++)
        {
            if (string.Equals(slots[index], slot, StringComparison.Ordinal))
            {
                return index;
            }
        }
        return -1;
    }
}
