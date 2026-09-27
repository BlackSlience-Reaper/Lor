using System;
using System.Globalization;
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
            [StateVersionKey] = FormatInt(CurrentStateVersion),
            [AliveEggMaskKey] = FormatInt(value.AliveEggMask),
            [ActiveEggKey] = FormatEnum(value.ActiveEgg),
            [PlannedModeKey] = FormatEnum(value.PlannedMode),
            [HasPlannedModeKey] = value.HasPlannedMode.ToString(),
            [ModeCycleStepKey] = FormatInt(value.ModeCycleStep),
            [JudgmentBranchEntriesKey] =
                FormatInt(value.JudgmentBranchEntries),
            [SinTraceBranchEntriesKey] =
                FormatInt(value.SinTraceBranchEntries),
            [PunishmentBranchEntriesKey] =
                FormatInt(value.PunishmentBranchEntries),
            [NextEndFallbackIsOneKey] =
                value.NextEndFallbackIsOne.ToString(),
            [PlannedBranchCounterKey] =
                FormatEnum(value.PlannedBranchCounter),
            [PlannedUsesEndFallbackKey] =
                value.PlannedUsesEndFallback.ToString(),
            [PlannedOtherFirstActionKey] =
                FormatEnum(value.PlannedOtherFirstAction),
            [PlannedOtherSecondActionKey] =
                FormatEnum(value.PlannedOtherSecondAction),
            [LastEggScheduleRoundKey] =
                FormatInt(value.LastEggScheduleRound),
            [BrokenEggRecoveryPendingKey] =
                value.BrokenEggRecoveryPending.ToString(),
            [IntroCgPlayedKey] = value.IntroCgPlayed.ToString(),
            [PlannedTargetCombatIdsKey] =
                FormatIntArray(value.PlannedTargetCombatIds),
            [SmallBeakProcessedRoundKey] =
                FormatInt(value.SmallBeakProcessedRound),
            [SmallBeakProcessedPlayerCombatIdsKey] =
                FormatIntArray(
                    value.SmallBeakProcessedPlayerCombatIds)
        };
    }

    public override void LoadCustomState(Dictionary<string, string> state)
    {
        _persistentState = new PhilosophyFloorTwilightPersistentState
        {
            AliveEggMask = ReadInt(
                state,
                AliveEggMaskKey,
                PhilosophyFloorTwilight.AllEggMask),
            ActiveEgg = ReadEnum(
                state,
                ActiveEggKey,
                PhilosophyFloorTwilightEgg.BigEyes),
            PlannedMode = ReadEnum(
                state,
                PlannedModeKey,
                PhilosophyFloorTwilightMode.Surveillance),
            HasPlannedMode = ReadBool(state, HasPlannedModeKey),
            ModeCycleStep = ReadInt(state, ModeCycleStepKey),
            JudgmentBranchEntries = ReadInt(
                state,
                JudgmentBranchEntriesKey),
            SinTraceBranchEntries = ReadInt(
                state,
                SinTraceBranchEntriesKey),
            PunishmentBranchEntries = ReadInt(
                state,
                PunishmentBranchEntriesKey),
            NextEndFallbackIsOne = ReadBool(
                state,
                NextEndFallbackIsOneKey,
                fallback: true),
            PlannedBranchCounter = ReadEnum(
                state,
                PlannedBranchCounterKey,
                PhilosophyFloorTwilightBranchCounter.None),
            PlannedUsesEndFallback = ReadBool(
                state,
                PlannedUsesEndFallbackKey),
            PlannedOtherFirstAction = ReadEnum(
                state,
                PlannedOtherFirstActionKey,
                PhilosophyFloorTwilightAction.Talon),
            PlannedOtherSecondAction = ReadEnum(
                state,
                PlannedOtherSecondActionKey,
                PhilosophyFloorTwilightAction.Talon),
            LastEggScheduleRound = ReadInt(
                state,
                LastEggScheduleRoundKey,
                fallback: -1),
            BrokenEggRecoveryPending = ReadBool(
                state,
                BrokenEggRecoveryPendingKey),
            IntroCgPlayed = ReadBool(state, IntroCgPlayedKey),
            PlannedTargetCombatIds = ReadIntArray(
                state,
                PlannedTargetCombatIdsKey,
                [0, 0, 0, 0]),
            SmallBeakProcessedRound = ReadInt(
                state,
                SmallBeakProcessedRoundKey,
                fallback: -1),
            SmallBeakProcessedPlayerCombatIds = ReadIntArray(
                state,
                SmallBeakProcessedPlayerCombatIdsKey,
                [])
        };
    }

    private static string FormatInt(int value) =>
        value.ToString(CultureInfo.InvariantCulture);

    private static string FormatEnum<T>(T value) where T : struct, Enum =>
        Convert.ToInt32(value, CultureInfo.InvariantCulture)
            .ToString(CultureInfo.InvariantCulture);

    private static string FormatIntArray(IEnumerable<int> values) =>
        string.Join(",", values.Select(FormatInt));

    private static int ReadInt(
        IReadOnlyDictionary<string, string> state,
        string key,
        int fallback = 0) =>
        state.TryGetValue(key, out string? text)
        && int.TryParse(
            text,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out int value)
            ? value
            : fallback;

    private static bool ReadBool(
        IReadOnlyDictionary<string, string> state,
        string key,
        bool fallback = false) =>
        state.TryGetValue(key, out string? text)
        && bool.TryParse(text, out bool value)
            ? value
            : fallback;

    private static T ReadEnum<T>(
        IReadOnlyDictionary<string, string> state,
        string key,
        T fallback) where T : struct, Enum
    {
        int raw = ReadInt(state, key, Convert.ToInt32(fallback));
        T value = (T)Enum.ToObject(typeof(T), raw);
        if (typeof(T).IsDefined(typeof(FlagsAttribute), inherit: false))
        {
            int allowedBits = Enum.GetValues<T>()
                .Aggregate(
                    0,
                    static (mask, entry) =>
                        mask | Convert.ToInt32(entry));
            return (raw & ~allowedBits) == 0 ? value : fallback;
        }

        return Enum.IsDefined(value) ? value : fallback;
    }

    private static int[] ReadIntArray(
        IReadOnlyDictionary<string, string> state,
        string key,
        int[] fallback)
    {
        if (!state.TryGetValue(key, out string? text))
        {
            return [.. fallback];
        }
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        string[] parts = text.Split(
            ',',
            StringSplitOptions.RemoveEmptyEntries
            | StringSplitOptions.TrimEntries);
        var values = new int[parts.Length];
        for (int index = 0; index < parts.Length; index++)
        {
            if (!int.TryParse(
                    parts[index],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out values[index]))
            {
                return [.. fallback];
            }
        }
        return values;
    }
}
