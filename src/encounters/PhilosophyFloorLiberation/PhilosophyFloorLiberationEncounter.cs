using System.Linq;
using Godot;
using LibraryOfRuina.encounters.LanguageFloorLiberation;
using LibraryOfRuina.monsters.PhilosophyFloorLiberation;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.encounters.PhilosophyFloorLiberation;

public sealed class PhilosophyFloorLiberationEncounter :
    EncounterModel,
    IFloorLiberationEncounter
{
    internal const string TwilightSlot = "twilight";
    internal const string EncounterScenePath =
        "res://scenes/encounters/philosophy_floor_liberation_encounter.tscn";
    internal const string BackgroundScenePath =
        "res://scenes/backgrounds/philosophy_floor_liberation_encounter/philosophy_floor_liberation_encounter_background.tscn";
    internal const string BackgroundLayerScenePath =
        "res://scenes/backgrounds/philosophy_floor_liberation_encounter/layers/philosophy_floor_liberation_encounter_bg_00_a.tscn";
    internal const string EggAnimationLibraryPath =
        "res://scenes/backgrounds/philosophy_floor_liberation_encounter/philosophy_floor_liberation_egg_animations.tres";
    internal const string EndBirdAnimationLibraryPath =
        "res://scenes/backgrounds/philosophy_floor_liberation_encounter/philosophy_floor_liberation_end_bird_animations.tres";
    internal const string BossNodeResourcePath =
        "res://images/map/placeholder/philosophy_floor_liberation_encounter_icon";

    private const string StateVersionKey = "TwilightStateVersion";
    private const string AliveEggMaskKey = "TwilightAliveEggMask";
    private const string ActiveEggKey = "TwilightActiveEgg";
    private const string PlannedModeKey = "TwilightPlannedMode";
    private const string HasPlannedModeKey = "TwilightHasPlannedMode";
    private const string ModeCycleStepKey = "TwilightModeCycleStep";
    private const string JudgmentBranchEntriesKey =
        "TwilightJudgmentBranchEntries";
    private const string SinTraceBranchEntriesKey =
        "TwilightSinTraceBranchEntries";
    private const string PunishmentBranchEntriesKey =
        "TwilightPunishmentBranchEntries";
    private const string NextEndFallbackIsOneKey =
        "TwilightNextEndFallbackIsOne";
    private const string PlannedBranchCounterKey =
        "TwilightPlannedBranchCounter";
    private const string PlannedUsesEndFallbackKey =
        "TwilightPlannedUsesEndFallback";
    private const string PlannedOtherFirstActionKey =
        "TwilightPlannedOtherFirstAction";
    private const string PlannedOtherSecondActionKey =
        "TwilightPlannedOtherSecondAction";
    private const string LastEggScheduleRoundKey =
        "TwilightLastEggScheduleRound";
    private const string BrokenEggRecoveryPendingKey =
        "TwilightBrokenEggRecoveryPending";
    private const string IntroCgPlayedKey = "TwilightIntroCgPlayed";
    private const string PlannedTargetCombatIdsKey =
        "TwilightPlannedTargetCombatIds";
    private const string SmallBeakProcessedRoundKey =
        "TwilightSmallBeakProcessedRound";
    private const string SmallBeakProcessedPlayerCombatIdsKey =
        "TwilightSmallBeakProcessedPlayerCombatIds";
    private const int CurrentStateVersion = 2;

    private const float EncounterCameraScaling = 0.82f;
    private static readonly Vector2 EncounterCameraOffset =
        Vector2.Down * 50f + Vector2.Left * 100f;
    private PhilosophyFloorTwilightPersistentState _persistentState = new();

    public override RoomType RoomType => RoomType.Boss;

    public override bool HasScene => true;

    public override IReadOnlyList<string> Slots => [TwilightSlot];

    protected override bool HasCustomBackground => true;

    public override MegaSkeletonDataResource? BossNodeSpineResource => null;

    public override string BossNodePath => BossNodeResourcePath;

    public override float GetCameraScaling() => EncounterCameraScaling;

    public override Vector2 GetCameraOffset() => EncounterCameraOffset;

    public string LiberationFloorId =>
        LiberationFloorIds.Philosophy;

    public bool IsFullyLiberated => true;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<PhilosophyFloorTwilight>()
    ];

    public override IEnumerable<string> ExtraAssetPaths =>
        ModelDb.Monster<PhilosophyFloorTwilight>().AssetPaths
            .Concat(new[]
            {
                EncounterScenePath,
                BackgroundScenePath,
                BackgroundLayerScenePath,
                EggAnimationLibraryPath,
                EndBirdAnimationLibraryPath,
                "res://images/ui/run_history/philosophy_floor_liberation_encounter.png",
                "res://images/ui/run_history/philosophy_floor_liberation_encounter_outline.png",
                BossNodeResourcePath + ".png",
                BossNodeResourcePath + "_outline.png"
            })
            .Concat(LanguageFloorLiberationEncounter.RolandLiberationBgmTracks)
            .Distinct();

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        var twilight = (PhilosophyFloorTwilight)ModelDb
            .Monster<PhilosophyFloorTwilight>()
            .ToMutable();
        twilight.RestorePersistentState(_persistentState);
        return [(twilight, TwilightSlot)];
    }

    public override Dictionary<string, string> SaveCustomState()
    {
        if (HaveMonstersBeenGenerated)
        {
            PhilosophyFloorTwilight? twilight = MonstersWithSlots
                .Select(static entry => entry.Item1)
                .OfType<PhilosophyFloorTwilight>()
                .FirstOrDefault();
            if (twilight != null)
            {
                _persistentState = twilight.CapturePersistentState();
            }
        }

        PhilosophyFloorTwilightPersistentState value = _persistentState;
        return new Dictionary<string, string>
        {
            [StateVersionKey] = EncounterStateBag.FormatInvariant(CurrentStateVersion),
            [AliveEggMaskKey] = EncounterStateBag.FormatInvariant(value.AliveEggMask),
            [ActiveEggKey] = EncounterStateBag.FormatInvariantEnum(value.ActiveEgg),
            [PlannedModeKey] = EncounterStateBag.FormatInvariantEnum(value.PlannedMode),
            [HasPlannedModeKey] = value.HasPlannedMode.ToString(),
            [ModeCycleStepKey] = EncounterStateBag.FormatInvariant(value.ModeCycleStep),
            [JudgmentBranchEntriesKey] =
                EncounterStateBag.FormatInvariant(value.JudgmentBranchEntries),
            [SinTraceBranchEntriesKey] =
                EncounterStateBag.FormatInvariant(value.SinTraceBranchEntries),
            [PunishmentBranchEntriesKey] =
                EncounterStateBag.FormatInvariant(value.PunishmentBranchEntries),
            [NextEndFallbackIsOneKey] =
                value.NextEndFallbackIsOne.ToString(),
            [PlannedBranchCounterKey] =
                EncounterStateBag.FormatInvariantEnum(value.PlannedBranchCounter),
            [PlannedUsesEndFallbackKey] =
                value.PlannedUsesEndFallback.ToString(),
            [PlannedOtherFirstActionKey] =
                EncounterStateBag.FormatInvariantEnum(value.PlannedOtherFirstAction),
            [PlannedOtherSecondActionKey] =
                EncounterStateBag.FormatInvariantEnum(value.PlannedOtherSecondAction),
            [LastEggScheduleRoundKey] =
                EncounterStateBag.FormatInvariant(value.LastEggScheduleRound),
            [BrokenEggRecoveryPendingKey] =
                value.BrokenEggRecoveryPending.ToString(),
            [IntroCgPlayedKey] = value.IntroCgPlayed.ToString(),
            [PlannedTargetCombatIdsKey] =
                EncounterStateBag.FormatInvariantIntArray(value.PlannedTargetCombatIds),
            [SmallBeakProcessedRoundKey] =
                EncounterStateBag.FormatInvariant(value.SmallBeakProcessedRound),
            [SmallBeakProcessedPlayerCombatIdsKey] =
                EncounterStateBag.FormatInvariantIntArray(
                    value.SmallBeakProcessedPlayerCombatIds)
        };
    }

    public override void LoadCustomState(Dictionary<string, string> state)
    {
        var bag = new EncounterStateBag(state);
        _persistentState = new PhilosophyFloorTwilightPersistentState
        {
            AliveEggMask = bag.ReadInvariantInt(
                AliveEggMaskKey,
                PhilosophyFloorTwilight.AllEggMask),
            ActiveEgg = bag.ReadInvariantEnum(
                ActiveEggKey,
                PhilosophyFloorTwilightEgg.BigEyes),
            PlannedMode = bag.ReadInvariantEnum(
                PlannedModeKey,
                PhilosophyFloorTwilightMode.Surveillance),
            HasPlannedMode = bag.ReadBool(HasPlannedModeKey),
            ModeCycleStep = bag.ReadInvariantInt(ModeCycleStepKey),
            JudgmentBranchEntries =
                bag.ReadInvariantInt(JudgmentBranchEntriesKey),
            SinTraceBranchEntries =
                bag.ReadInvariantInt(SinTraceBranchEntriesKey),
            PunishmentBranchEntries =
                bag.ReadInvariantInt(PunishmentBranchEntriesKey),
            NextEndFallbackIsOne = bag.ReadBool(
                NextEndFallbackIsOneKey,
                fallback: true),
            PlannedBranchCounter = bag.ReadInvariantEnum(
                PlannedBranchCounterKey,
                PhilosophyFloorTwilightBranchCounter.None),
            PlannedUsesEndFallback =
                bag.ReadBool(PlannedUsesEndFallbackKey),
            PlannedOtherFirstAction = bag.ReadInvariantEnum(
                PlannedOtherFirstActionKey,
                PhilosophyFloorTwilightAction.Talon),
            PlannedOtherSecondAction = bag.ReadInvariantEnum(
                PlannedOtherSecondActionKey,
                PhilosophyFloorTwilightAction.Talon),
            LastEggScheduleRound = bag.ReadInvariantInt(
                LastEggScheduleRoundKey,
                fallback: -1),
            BrokenEggRecoveryPending =
                bag.ReadBool(BrokenEggRecoveryPendingKey),
            IntroCgPlayed = bag.ReadBool(IntroCgPlayedKey),
            PlannedTargetCombatIds = bag.ReadInvariantIntArray(
                PlannedTargetCombatIdsKey,
                [0, 0, 0, 0]),
            SmallBeakProcessedRound = bag.ReadInvariantInt(
                SmallBeakProcessedRoundKey,
                fallback: -1),
            SmallBeakProcessedPlayerCombatIds = bag.ReadInvariantIntArray(
                SmallBeakProcessedPlayerCombatIdsKey,
                [])
        };
    }
}
