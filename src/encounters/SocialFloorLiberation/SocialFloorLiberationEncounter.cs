using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.cards.ScarecrowSearchingForWisdom;
using LibraryOfRuina.cards.SocialFloorLiberation;
using LibraryOfRuina.compat;
using LibraryOfRuina.encounters.LanguageFloorLiberation;
using LibraryOfRuina.monsters.SocialFloorLiberation;
using LibraryOfRuina.powers.ScarecrowSearchingForWisdom;
using LibraryOfRuina.powers.SocialFloorLiberation;
using LibraryOfRuina.visuals.SocialFloorLiberation;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

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
    ILiberationPhaseBgmSource,
    IFloorLiberationEncounter
{
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
    private const int InitialPowderCost = 10;

    private const float EncounterCameraScaling = 0.82f;
    private static readonly Vector2 EncounterCameraOffset =
        Vector2.Down * 50f + Vector2.Left * 100f;

    public SocialFloorTrial Trial { get; private set; } =
        SocialFloorTrial.Initial;

    public int TrialRound { get; private set; }

    public bool SetupComplete { get; private set; }

    public bool PlayersHealed { get; private set; }

    public int DestroyedCrystalMask { get; private set; }

    public int DestroyedFaceMask { get; private set; }

    public bool HasPendingTrial { get; private set; }

    public SocialFloorTrial PendingTrial { get; private set; } =
        SocialFloorTrial.Initial;

    public ulong? ScaredyCatPlayerNetId { get; private set; }

    public ulong? OzmaPlayerNetId { get; private set; }

    public bool CowardApplied { get; private set; }

    public bool OzmaReplacementPending { get; private set; }

    public int PowderCost { get; private set; } = InitialPowderCost;

    public bool Transformed { get; private set; }

    public bool FinalStrikeTriggered { get; private set; }

    public FalseThroneMove PlannedMove { get; private set; } =
        FalseThroneMove.InitialSequence;

    public int ParticipantCount { get; private set; } = 1;

    public int SavedBossHp { get; private set; } = -1;

    public int SavedBossChao { get; private set; } = -1;

    public string SavedSummonVitals { get; private set; } = string.Empty;

    public string SavedWisdomStacks { get; private set; } = string.Empty;

    public string SavedWisdomCards { get; private set; } = string.Empty;

    public int LionCardsSubmitted { get; private set; }

    public bool CouragePlayed { get; private set; }

    public bool CouragePowerPresent { get; private set; }

    public int CouragePendingActivations { get; private set; }

    public bool CourageEnergyActive { get; private set; }

    public bool CourageRemoveAtTurnEnd { get; private set; }

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

        if (ShouldKeepWoodsmanSummons)
        {
            for (int index = 0; index < CrystalSlots.Count; index++)
            {
                if ((DestroyedCrystalMask & (1 << index)) == 0)
                {
                    monsters.Add((
                        ModelDb.Monster<EmeraldCrystal>().ToMutable(),
                        CrystalSlots[index]));
                }
            }
        }
        else if (Trial == SocialFloorTrial.Lion)
        {
            int hp = ResolveFaceHpForPlayerCount(ParticipantCount);
            for (int index = 0; index < FaceSlots.Count; index++)
            {
                if ((DestroyedFaceMask & (1 << index)) != 0)
                {
                    continue;
                }

                var face = (ScowlingFace)ModelDb
                    .Monster<ScowlingFace>()
                    .ToMutable();
                face.ConfigureInitialHp(hp);
                monsters.Add((face, FaceSlots[index]));
            }
        }

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
        bool isDeferredRageTransition = Trial == SocialFloorTrial.Home
            && HasPendingTrial
            && PendingTrial == SocialFloorTrial.Rage;
        if (HasPendingTrial && !isDeferredRageTransition)
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
        await HandleOzmaReplacement(combatState);
        if (Trial == SocialFloorTrial.Lion)
        {
            await ApplyLionRoundDebuffs(combatState, boss.Creature);
        }
    }

    internal async Task OnAfterEnemyTurn(
        PlayerChoiceContext choiceContext,
        FalseThrone boss,
        CombatStateLike combatState)
    {
        TrialRound++;
        if (Trial == SocialFloorTrial.Home
            && HasPendingTrial
            && PendingTrial == SocialFloorTrial.Rage)
        {
            await EnterPendingTrial(choiceContext, boss, combatState);
            return;
        }

        switch (Trial)
        {
            case SocialFloorTrial.Initial:
                QueueTrial(SocialFloorTrial.Woodsman);
                await EnterPendingTrial(choiceContext, boss, combatState);
                break;

            case SocialFloorTrial.Woodsman:
                if (TrialRound >= 2 && AreAllCrystalsDestroyed)
                {
                    QueueTrial(SocialFloorTrial.Scarecrow);
                    await EnterPendingTrial(choiceContext, boss, combatState);
                }
                else if (TrialRound >= 3)
                {
                    QueueTrial(SocialFloorTrial.Scarecrow);
                    await EnterPendingTrial(choiceContext, boss, combatState);
                }
                else
                {
                    PlannedMove = TrialRound >= 2
                        ? FalseThroneMove.BigMistake
                        : FalseThroneMove.Manners;
                    if (PlannedMove == FalseThroneMove.BigMistake)
                    {
                        await RemoveTrialSummons<EmeraldCrystal>(combatState);
                    }
                    boss.ForcePlannedMove(PlannedMove);
                }
                break;

            case SocialFloorTrial.Scarecrow:
                if (TrialRound >= 2)
                {
                    await ResolveScarecrowPenalty(choiceContext, combatState);
                    QueueTrial(SocialFloorTrial.Lion);
                    await EnterPendingTrial(choiceContext, boss, combatState);
                }
                else
                {
                    PlannedMove = FalseThroneMove.UnknownTrial;
                    boss.ForcePlannedMove(PlannedMove);
                }
                break;

            case SocialFloorTrial.Lion:
                if (AreAllFacesDestroyed)
                {
                    QueueTrial(SocialFloorTrial.Home);
                    await EnterPendingTrial(choiceContext, boss, combatState);
                }
                else if (TrialRound >= 2)
                {
                    await ApplyCowardToAllLivingPlayers();
                    await RemoveTrialSummons<ScowlingFace>(combatState);
                    QueueTrial(SocialFloorTrial.Home);
                    await EnterPendingTrial(choiceContext, boss, combatState);
                }
                else
                {
                    PlannedMove = FalseThroneMove.StunTrial;
                    boss.ForcePlannedMove(PlannedMove);
                }
                break;

            case SocialFloorTrial.Home:
                PlannedMove = ResolveHomeMove(
                    TrialRound,
                    boss.RollHomeMove);
                boss.ForcePlannedMove(PlannedMove);
                break;

            case SocialFloorTrial.Rage:
                PlannedMove = FalseThroneMove.FunIsOver;
                boss.ForcePlannedMove(PlannedMove);
                break;
        }
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
        if (Trial == SocialFloorTrial.Lion
            && ScaredyCatPlayerNetId is { } catNetId
            && netId != catNetId)
        {
            await ApplyCowardToAllLivingPlayers();
        }

        if (Trial == SocialFloorTrial.Rage
            && OzmaPlayerNetId == netId)
        {
            await SocialFloorPlayerMechanics.InvalidateOzma(creature.Player);
            OzmaReplacementPending = true;
            OzmaPlayerNetId = null;
        }
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

    private async Task EnsureTrialSetup(
        PlayerChoiceContext choiceContext,
        FalseThrone boss,
        CombatStateLike combatState)
    {
        if (SetupComplete)
        {
            if (Trial == SocialFloorTrial.Home)
            {
                await boss.OpenHomeChaoGate();
            }
            await EnsureMissingTrialSummons(combatState);
            await boss.SyncTrialPowers();
            return;
        }

        SetupComplete = true;
        switch (Trial)
        {
            case SocialFloorTrial.Woodsman:
                DestroyedCrystalMask = 0;
                if (ShouldKeepWoodsmanSummons)
                {
                    await SpawnMissingCrystals(combatState);
                }
                break;

            case SocialFloorTrial.Scarecrow:
                SavedWisdomStacks = string.Empty;
                SavedWisdomCards = string.Empty;
                await SocialFloorPlayerMechanics.AddWisdomCards(
                    combatState,
                    boss.UsesToughValues
                        ? ToughWisdomCardCount
                        : NormalWisdomCardCount);
                break;

            case SocialFloorTrial.Lion:
                DestroyedFaceMask = 0;
                LionCardsSubmitted = 0;
                CouragePlayed = false;
                CouragePowerPresent = false;
                CouragePendingActivations = 0;
                CourageEnergyActive = false;
                CourageRemoveAtTurnEnd = false;
                await SpawnMissingFaces(combatState);
                await GrantScaredyCat(combatState);
                break;

            case SocialFloorTrial.Home:
                await boss.OpenHomeChaoGate();
                break;

            case SocialFloorTrial.Rage:
                if (boss.Creature is LibraryCreature
                    libraryCreature)
                {
                    libraryCreature.RestorePreStunResistance();
                    await LibraryCreatureCmd
                        .SetCurrentChaoValue(
                            libraryCreature,
                            libraryCreature.MaxChaoValue);
                }
                await GrantOzma(combatState, InitialPowderCost);
                break;
        }

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
        if (ShouldKeepWoodsmanSummons)
        {
            await SpawnMissingCrystals(combatState);
        }
        else if (Trial == SocialFloorTrial.Lion)
        {
            await SpawnMissingFaces(combatState);
        }
    }

    private async Task EnterPendingTrial(
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
        switch (previous)
        {
            case SocialFloorTrial.Woodsman:
                // Trial has already advanced, so the conditional max-energy
                // restriction is lifted before any asynchronous cleanup runs.
                await RemoveTrialSummons<EmeraldCrystal>(combatState);
                break;

            case SocialFloorTrial.Scarecrow:
                await SocialFloorPlayerMechanics
                    .ClearScarecrowTrialState(combatState);
                SavedWisdomStacks = string.Empty;
                SavedWisdomCards = string.Empty;
                break;

            case SocialFloorTrial.Lion:
                await RemoveTrialSummons<ScowlingFace>(combatState);
                await SocialFloorPlayerMechanics.ClearLionTrialState(
                    combatState,
                    ScaredyCatPlayerNetId);
                LionCardsSubmitted = 0;
                CouragePowerPresent = false;
                CouragePendingActivations = 0;
                CourageEnergyActive = false;
                CourageRemoveAtTurnEnd = false;
                break;
        }
    }

    private void QueueTrial(SocialFloorTrial next)
    {
        if (next <= Trial)
        {
            return;
        }

        HasPendingTrial = true;
        PendingTrial = next;
    }

    private async Task SpawnMissingCrystals(CombatStateLike combatState)
    {
        for (int index = 0; index < CrystalSlots.Count; index++)
        {
            int bit = 1 << index;
            string slot = CrystalSlots[index];
            if ((DestroyedCrystalMask & bit) != 0
                || combatState.Enemies.Any(enemy =>
                    enemy.IsAlive
                    && enemy.Monster is EmeraldCrystal
                    && enemy.SlotName == slot))
            {
                continue;
            }

            Creature spawned = await CreatureCmd.Add(
                ModelDb.Monster<EmeraldCrystal>().ToMutable(),
                combatState,
                CombatSide.Enemy,
                slot);
            spawned.PrepareForNextTurn(combatState.PlayerCreatures);
        }

        combatState.SortEnemiesBySlotName();
    }

    private async Task SpawnMissingFaces(CombatStateLike combatState)
    {
        int hp = ResolveFaceHpForPlayerCount(combatState.Players.Count);
        for (int index = 0; index < FaceSlots.Count; index++)
        {
            int bit = 1 << index;
            string slot = FaceSlots[index];
            if ((DestroyedFaceMask & bit) != 0
                || combatState.Enemies.Any(enemy =>
                    enemy.IsAlive
                    && enemy.Monster is ScowlingFace
                    && enemy.SlotName == slot))
            {
                continue;
            }

            var face = (ScowlingFace)ModelDb
                .Monster<ScowlingFace>()
                .ToMutable();
            face.ConfigureInitialHp(hp);
            Creature spawned = await CreatureCmd.Add(
                face,
                combatState,
                CombatSide.Enemy,
                slot);
            spawned.PrepareForNextTurn(combatState.PlayerCreatures);
        }

        combatState.SortEnemiesBySlotName();
    }

    private static async Task RemoveTrialSummons<T>(
        CombatStateLike combatState) where T : MonsterModel
    {
        foreach (Creature summon in combatState.Enemies
                     .Where(static enemy => enemy.Monster is T)
                     .ToArray())
        {
            await LiberationPhaseCleanup.RemoveTransitionCreature(
                summon,
                combatState);
        }

        combatState.SortEnemiesBySlotName();
    }

    private async Task ResolveScarecrowPenalty(
        PlayerChoiceContext choiceContext,
        CombatStateLike combatState)
    {
        int required = combatState.RunState.AscensionLevel
            >= (int)AscensionLevel
                .ToughEnemies
            ? ToughWisdomRequirement
            : NormalWisdomRequirement;
        foreach (Player player in combatState.Players
                     .Where(static player => player.Creature.IsAlive)
                     .OrderBy(static player => player.NetId))
        {
            if (SocialFloorPlayerMechanics.GetWisdomStacks(player)
                >= required)
            {
                continue;
            }

            decimal loss = Math.Ceiling(
                player.Creature.CurrentHp
                * ScarecrowPenaltyHpLossPercent
                / 100m);
            if (loss > 0m)
            {
                await CreatureCmd.Damage(
                    choiceContext,
                    player.Creature,
                    loss,
                    ValueProp.Unblockable | ValueProp.Unpowered,
                    null,
                    null);
            }
        }
    }

    private async Task GrantScaredyCat(CombatStateLike combatState)
    {
        Player? holder = ResolveLivingPlayer(
            combatState,
            ScaredyCatPlayerNetId);
        holder ??= ChooseRandomLivingPlayer(combatState);
        if (holder == null)
        {
            return;
        }

        ScaredyCatPlayerNetId = holder.NetId;
        await SocialFloorPlayerMechanics.GrantScaredyCat(holder);
    }

    private async Task ApplyCowardToAllLivingPlayers()
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

    private async Task GrantOzma(
        CombatStateLike combatState,
        int initialCost)
    {
        Player? holder = ResolveLivingPlayer(combatState, OzmaPlayerNetId);
        holder ??= ChooseRandomLivingPlayer(combatState);
        if (holder == null)
        {
            return;
        }

        OzmaPlayerNetId = holder.NetId;
        PowderCost = Math.Max(0, initialCost);
        OzmaReplacementPending = false;
        await SocialFloorPlayerMechanics.GrantOzma(holder, PowderCost);
    }

    private async Task HandleOzmaReplacement(CombatStateLike combatState)
    {
        if (Trial != SocialFloorTrial.Rage || !OzmaReplacementPending)
        {
            return;
        }

        await GrantOzma(combatState, InitialPowderCost);
    }

    private static async Task ApplyLionRoundDebuffs(
        CombatStateLike combatState,
        Creature applier)
    {
        PlayerChoiceContext context = new ThrowingPlayerChoiceContext();
        foreach (Creature player in LivingPlayers(combatState))
        {
            await LibraryPowerCmd
                .Apply<LibraryWeakPower>(
                    context,
                    player,
                    999m,
                    turns: 0,
                    IsPermanent: false,
                    applier,
                    null);
            await LibraryPowerCmd
                .Apply<LibraryDisarmPower>(
                    context,
                    player,
                    999m,
                    turns: 0,
                    IsPermanent: false,
                    applier,
                    null);
        }
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

        IReadOnlyDictionary<ulong, int> wisdomStacks =
            ParsePlayerValues(SavedWisdomStacks);
        IReadOnlyDictionary<ulong, int> wisdomCards =
            ParsePlayerValues(SavedWisdomCards);
        if (Trial == SocialFloorTrial.Scarecrow)
        {
            // Combat rooms are reconstructed from encounter state. Normalize
            // any partially restored trial objects before recreating the exact
            // saved per-player state so loading is idempotent.
            await SocialFloorPlayerMechanics
                .ClearScarecrowTrialState(combatState);
            int initialCards = boss.UsesToughValues
                ? ToughWisdomCardCount
                : NormalWisdomCardCount;
            foreach (Player player in combatState.Players
                         .Where(static player => player.Creature.IsAlive)
                         .OrderBy(static player => player.NetId))
            {
                int stacks = Math.Max(
                    0,
                    Math.Min(
                        NormalWisdomCardCount,
                        wisdomStacks.GetValueOrDefault(player.NetId)));
                if (stacks > 0)
                {
                    await PowerCmdCompat.Ensure<ScarecrowWisdomPower>(
                        player.Creature,
                        stacks,
                        player.Creature,
                        null,
                        silent: true);
                }

                int cards = wisdomCards.TryGetValue(
                    player.NetId,
                    out int savedCardCount)
                        ? Math.Clamp(
                            savedCardCount,
                            0,
                            initialCards)
                        : Math.Max(0, initialCards - stacks);
                await CardPileCmdCompat
                    .AddToCombatAndPreview<ScarecrowWisdomStatusCard>(
                        player.Creature,
                        PileType.Discard,
                        cards,
                        addedByPlayer: false);
            }
        }

        if (Trial == SocialFloorTrial.Lion)
        {
            await SocialFloorPlayerMechanics.ClearLionTrialState(
                combatState,
                ScaredyCatPlayerNetId);
        }

        Player? catHolder = ResolveLivingPlayer(
            combatState,
            ScaredyCatPlayerNetId);
        if (Trial == SocialFloorTrial.Lion && catHolder != null)
        {
            await SocialFloorPlayerMechanics.RestoreScaredyCat(
                catHolder,
                LionCardsSubmitted,
                courageCardGranted: !CouragePlayed,
                isHolderActive: true,
                ensureCourageCard: !CouragePlayed);
        }

        if (CouragePowerPresent && catHolder != null)
        {
            await SocialFloorPlayerMechanics.RestoreCourage(
                catHolder,
                CouragePendingActivations,
                CourageEnergyActive,
                CourageRemoveAtTurnEnd);
        }

        if (CowardApplied && catHolder != null)
        {
            await SocialFloorPlayerMechanics.RestoreCoward(catHolder);
        }

        if (Trial == SocialFloorTrial.Rage
            && !OzmaReplacementPending)
        {
            Player? ozmaHolder = ResolveLivingPlayer(
                combatState,
                OzmaPlayerNetId);
            if (ozmaHolder == null)
            {
                OzmaPlayerNetId = null;
                OzmaReplacementPending = true;
            }
            else
            {
                await SocialFloorPlayerMechanics.RestoreOzma(
                    ozmaHolder,
                    PowderCost,
                    ensurePowder: !Transformed);
            }
        }
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

    private static IReadOnlyDictionary<ulong, int> ParsePlayerValues(
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

    private static Player? ResolveLivingPlayer(
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

    private static Player? ChooseRandomLivingPlayer(
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

    private static IReadOnlyList<Creature> LivingPlayers(
        CombatStateLike combatState) =>
        combatState.PlayerCreatures
            .Where(static player => player.IsAlive)
            .OrderBy(static player => player.CombatId)
            .ToArray();

    private static FalseThroneMove ResolveDefaultMove(
        SocialFloorTrial trial,
        int round) => trial switch
    {
        SocialFloorTrial.Initial => FalseThroneMove.InitialSequence,
        SocialFloorTrial.Woodsman => round >= 2
            ? FalseThroneMove.BigMistake
            : FalseThroneMove.Manners,
        SocialFloorTrial.Scarecrow => FalseThroneMove.UnknownTrial,
        SocialFloorTrial.Lion => FalseThroneMove.StunTrial,
        SocialFloorTrial.Home => ResolveHomeMove(
            round,
            static () =>
                FalseThroneMove.HomeOverflowingLightInsolence),
        SocialFloorTrial.Rage => FalseThroneMove.FunIsOver,
        _ => FalseThroneMove.InitialSequence
    };

    private static FalseThroneMove ResolveHomeMove(
        int round,
        Func<FalseThroneMove> normalMoveFactory) => round switch
    {
        <= 0 => FalseThroneMove.StunTrial,
        1 => FalseThroneMove.FriendlyGreeting,
        _ => normalMoveFactory()
    };

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
