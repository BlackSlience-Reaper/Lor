using System;
using System.Reflection;
using Godot;
using HarmonyLib;
using LibraryOfRuina.audio;
using LibraryOfRuina.encounters.AddictedEmployee;
using LibraryOfRuina.encounters.AllAroundHelper;
using LibraryOfRuina.encounters.ArtFloorLiberation;
using LibraryOfRuina.encounters.BigBadWolf;
using LibraryOfRuina.encounters.BigBird;
using LibraryOfRuina.encounters.BlueStar;
using LibraryOfRuina.encounters.BrotherhoodOfIron;
using LibraryOfRuina.encounters.BurrowingHeaven;
using LibraryOfRuina.encounters.CosmicFragment;
using LibraryOfRuina.encounters.DawnOffice;
using LibraryOfRuina.encounters.DeadButterfly;
using LibraryOfRuina.encounters.DespairKnight;
using LibraryOfRuina.encounters.FairyFestival;
using LibraryOfRuina.encounters.ForsakenMurderer;
using LibraryOfRuina.encounters.FuneralOfTheDeadButterflies;
using LibraryOfRuina.encounters.GalaxyChild;
using LibraryOfRuina.encounters.HappyTeddy;
using LibraryOfRuina.encounters.HeartOfAspiration;
using LibraryOfRuina.encounters.HistoryFloorLiberation;
using LibraryOfRuina.encounters.HookOffice;
using LibraryOfRuina.encounters.JudgementBird;
using LibraryOfRuina.encounters.KuroKumo;
using LibraryOfRuina.encounters.LanguageFloorLiberation;
using LibraryOfRuina.encounters.LiteratureFloorLiberation;
using LibraryOfRuina.encounters.MusiciansOfBremen;
using LibraryOfRuina.encounters.PhilosophyFloorLiberation;
using LibraryOfRuina.encounters.PriceOfSilence;
using LibraryOfRuina.encounters.PunishingBird;
using LibraryOfRuina.encounters.QueenOfHatred;
using LibraryOfRuina.encounters.RedMist;
using LibraryOfRuina.encounters.RedShoes;
using LibraryOfRuina.encounters.ScarecrowSearchingForWisdom;
using LibraryOfRuina.encounters.SmilingBodies;
using LibraryOfRuina.encounters.SocialFloorLiberation;
using LibraryOfRuina.encounters.SpiderBud;
using LibraryOfRuina.encounters.SpinyBus;
using LibraryOfRuina.encounters.TechnologyFloorLiberation;
using LibraryOfRuina.encounters.TodaysShyLook;
using LibraryOfRuina.encounters.Tomerry;
using LibraryOfRuina.encounters.WarmheartedWoodsman;
using LibraryOfRuina.encounters.WedgeOffice;
using LibraryOfRuina.encounters.WrathServant;
using LibraryOfRuina.encounters.YunOffice;
using LibraryOfRuina.features.settings;
using LibraryOfRuina.specialguests.Kali;
using LibraryOfRuina.specialguests.Rnfmabj;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace LibraryOfRuina.encounters;

internal static class EncounterBgmController
{
    private sealed record SuspendedSessionSnapshot(
        string SessionKey,
        Type EncounterType,
        int TrackIndex,
        float PlaybackPositionSeconds);

    private enum TrackProgressionMode
    {
        OnMonsterDeath,
        OnRoundThreshold,
        Dynamic
    }

    private sealed record EncounterBgmConfig(
        string LogTag,
        string[] TrackPaths,
        TrackProgressionMode ProgressionMode,
        int[] RoundThresholds,
        float VolumeScale = 0.85f)
    {
        public Func<CombatStateLike, int>? DynamicTrackResolver { get; init; }

        public static EncounterBgmConfig DeathBased(string logTag, params string[] tracks)
        {
            return new EncounterBgmConfig(logTag, tracks, TrackProgressionMode.OnMonsterDeath, Array.Empty<int>());
        }

        public static EncounterBgmConfig RoundBased(
            string logTag,
            string[] tracks,
            float volumeScale = 0.85f,
            params int[] roundThresholds)
        {
            return new EncounterBgmConfig(
                logTag,
                tracks,
                TrackProgressionMode.OnRoundThreshold,
                roundThresholds,
                volumeScale);
        }

        public static EncounterBgmConfig PhaseBased(
            string logTag,
            string[] tracks,
            float volumeScale = 0.85f)
        {
            return new EncounterBgmConfig(
                logTag,
                tracks,
                TrackProgressionMode.Dynamic,
                Array.Empty<int>(),
                volumeScale)
            {
                DynamicTrackResolver = ResolveLiberationPhaseTrack
            };
        }

        public static EncounterBgmConfig DynamicSourceBased(
            string logTag,
            string[] tracks,
            float volumeScale = 0.85f)
        {
            return new EncounterBgmConfig(
                logTag,
                tracks,
                TrackProgressionMode.Dynamic,
                Array.Empty<int>(),
                volumeScale)
            {
                DynamicTrackResolver = ResolveDynamicSourceTrack
            };
        }
    }

    private const float FadeDurationSeconds = 1.25f;
    private const float MaxVolumeDb = 4f;
    private const float MinVolumeDb = -40f;

    private static readonly Dictionary<Type, EncounterBgmConfig> ConfigByEncounterType = new()
    {
        [typeof(KuroKumoNormal)] = EncounterBgmConfig.DeathBased(
            "KuroKumoBGM",
            "res://audio/bgm/kurokumo/kurokumo_guest_battle_1.ogg",
            "res://audio/bgm/kurokumo/kurokumo_guest_battle_2.ogg",
            "res://audio/bgm/kurokumo/kurokumo_guest_battle_3.ogg"),
        [typeof(MusiciansOfBremenNormal)] = EncounterBgmConfig.DeathBased(
            "MusiciansOfBremenBGM",
            "res://audio/bgm/musicians_of_bremen/musicians_of_bremen_battle_1.ogg",
            "res://audio/bgm/musicians_of_bremen/musicians_of_bremen_battle_2.ogg",
            "res://audio/bgm/musicians_of_bremen/musicians_of_bremen_battle_3.ogg"),
        [typeof(DawnOfficeNormal)] = EncounterBgmConfig.DeathBased(
            "DawnOfficeBGM",
            "res://audio/bgm/dawn_office/dawn_office_battle_1.ogg",
            "res://audio/bgm/dawn_office/dawn_office_battle_2.ogg",
            "res://audio/bgm/dawn_office/dawn_office_battle_3.ogg"),
        [typeof(WedgeOfficeNormal)] = EncounterBgmConfig.RoundBased(
            "WedgeOfficeBGM",
            GuestReceptionPoolRegistry.GeneralReceptionFloorBgmTracks,
            volumeScale: 0.85f,
            GuestReceptionPoolRegistry.StandardRoundThresholds),
        [typeof(YunOfficeNormal)] = EncounterBgmConfig.RoundBased(
            "YunOfficeBGM",
            GuestReceptionPoolRegistry.GeneralReceptionFloorBgmTracks,
            volumeScale: 0.85f,
            GuestReceptionPoolRegistry.StandardRoundThresholds),
        [typeof(HookOfficeStrong)] = EncounterBgmConfig.RoundBased(
            "HookOfficeBGM",
            GuestReceptionPoolRegistry.GeneralReceptionFloorBgmTracks,
            volumeScale: 0.85f,
            GuestReceptionPoolRegistry.StandardRoundThresholds),
        [typeof(BrotherhoodOfIronStrong)] = EncounterBgmConfig.RoundBased(
            "BrotherhoodOfIronBGM",
            GuestReceptionPoolRegistry.GeneralReceptionFloorBgmTracks,
            volumeScale: 0.85f,
            GuestReceptionPoolRegistry.StandardRoundThresholds),
        [typeof(FinnWeak)] = EncounterBgmConfig.DeathBased(
            "FinnBGM",
            "res://audio/bgm/finn/finn_battle_1.ogg"),
        [typeof(HappyTeddyWeak)] = EncounterBgmConfig.RoundBased(
            "HappyTeddyBGM",
            new[]
            {
                "res://audio/bgm/scorched_girl/scorched_girl_battle_1.ogg",
                "res://audio/bgm/scorched_girl/scorched_girl_battle_2.ogg",
                "res://audio/bgm/scorched_girl/scorched_girl_battle_3.ogg"
            },
            volumeScale: 0.85f,
            4,
            7),
        [typeof(FairyFestivalStrong)] = EncounterBgmConfig.RoundBased(
            "FairyFestivalBGM",
            new[]
            {
                "res://audio/bgm/fairy_festival/history_layer_1.ogg",
                "res://audio/bgm/fairy_festival/history_layer_2.ogg",
                "res://audio/bgm/fairy_festival/history_layer_3.ogg"
            },
            volumeScale: 0.85f,
            4,
            7),
        [typeof(HistoryFloorLiberationEncounter)] = EncounterBgmConfig.PhaseBased(
            "AngelaLiberationBGM",
            HistoryFloorLiberationEncounter.AngelaLiberationBgmTracks,
            volumeScale: 0.85f),
        [typeof(LiteratureFloorLiberationEncounter)] = EncounterBgmConfig.PhaseBased(
            "LiteratureFloorLiberationBGM",
            HistoryFloorLiberationEncounter.AngelaLiberationBgmTracks,
            volumeScale: 0.85f),
        [typeof(NaturalFloorLiberation.NaturalFloorLiberationEncounter)] = EncounterBgmConfig.PhaseBased(
            "NaturalFloorLiberationBGM",
            LanguageFloorLiberationEncounter.RolandLiberationBgmTracks,
            volumeScale: 0.85f),
        [typeof(TechnologyFloorLiberationEncounter)] = EncounterBgmConfig.PhaseBased(
            "TechnologyFloorLiberationBGM",
            HistoryFloorLiberationEncounter.AngelaLiberationBgmTracks,
            volumeScale: 0.85f),
        [typeof(ArtFloorLiberationEncounter)] = EncounterBgmConfig.PhaseBased(
            "ArtFloorLiberationBGM",
            HistoryFloorLiberationEncounter.AngelaLiberationBgmTracks,
            volumeScale: 0.85f),
        [typeof(LanguageFloorLiberationEncounter)] = EncounterBgmConfig.PhaseBased(
            "LanguageFloorLiberationBGM",
            LanguageFloorLiberationEncounter.RolandLiberationBgmTracks,
            volumeScale: 0.85f),
        [typeof(PhilosophyFloorLiberationEncounter)] = EncounterBgmConfig.DynamicSourceBased(
            "PhilosophyFloorLiberationBGM",
            LanguageFloorLiberationEncounter.RolandLiberationBgmTracks,
            volumeScale: 0.85f),
        [typeof(SocialFloorLiberationEncounter)] = EncounterBgmConfig.PhaseBased(
            "SocialFloorLiberationBGM",
            LanguageFloorLiberationEncounter.RolandLiberationBgmTracks,
            volumeScale: 0.85f),
        [typeof(QueenOfHatredStrong)] = EncounterBgmConfig.RoundBased(
            "QueenOfHatredBGM",
            GuestReceptionPoolRegistry.NaturalReceptionFloorBgmTracks,
            volumeScale: 0.85f,
            GuestReceptionPoolRegistry.StandardRoundThresholds),
        [typeof(RedMistElite)] = EncounterBgmConfig.DeathBased(
            "RedMistBGM",
            "res://audio/bgm/language_reception_floor/GeburaBattle1.ogg"),
        [typeof(KaliSpecialGuestEncounter)] = EncounterBgmConfig.DeathBased(
            "RedMistSpecialGuestBGM",
            "res://audio/bgm/language_reception_floor/GeburaBattle1.ogg"),
        [typeof(RnfmabjSpecialGuestEncounter)] = EncounterBgmConfig.DeathBased(
            "RnfmabjSpecialGuestBGM",
            RnfmabjSpecialGuestIds.BattleBgm),
        [typeof(ForsakenMurdererWeak)] = EncounterBgmConfig.RoundBased(
            "ForsakenMurdererBGM",
            GuestReceptionPoolRegistry.YesodReceptionFloorBgmTracks,
            volumeScale: 0.85f,
            GuestReceptionPoolRegistry.StandardRoundThresholds),
        [typeof(AllAroundHelperWeak)] = EncounterBgmConfig.RoundBased(
            "AllAroundHelperBGM",
            GuestReceptionPoolRegistry.YesodReceptionFloorBgmTracks,
            volumeScale: 0.85f,
            GuestReceptionPoolRegistry.StandardRoundThresholds),
        [typeof(AllAroundHelperStrong)] = EncounterBgmConfig.RoundBased(
            "AllAroundHelperBGM",
            GuestReceptionPoolRegistry.YesodReceptionFloorBgmTracks,
            volumeScale: 0.85f,
            GuestReceptionPoolRegistry.StandardRoundThresholds),
        [typeof(TodaysShyLookStrong)] = EncounterBgmConfig.RoundBased(
            "TodaysShyLookBGM",
            GuestReceptionPoolRegistry.LiteratureReceptionFloorBgmTracks,
            volumeScale: 0.85f,
            GuestReceptionPoolRegistry.StandardRoundThresholds),
        [typeof(SpiderBudStrong)] = EncounterBgmConfig.RoundBased(
            "SpiderBudBGM",
            GuestReceptionPoolRegistry.LiteratureReceptionFloorBgmTracks,
            volumeScale: 0.85f,
            GuestReceptionPoolRegistry.StandardRoundThresholds),
        [typeof(RedShoesStrong)] = EncounterBgmConfig.RoundBased(
            "RedShoesBGM",
            GuestReceptionPoolRegistry.LiteratureReceptionFloorBgmTracks,
            volumeScale: 0.85f,
            GuestReceptionPoolRegistry.StandardRoundThresholds),
        [typeof(DeadButterflyWeak)] = EncounterBgmConfig.RoundBased(
            "DeadButterflyBGM",
            GuestReceptionPoolRegistry.LiteratureReceptionFloorBgmTracks,
            volumeScale: 0.85f,
            GuestReceptionPoolRegistry.StandardRoundThresholds),
        [typeof(DeadButterflyStrong)] = EncounterBgmConfig.RoundBased(
            "DeadButterflyBGM",
            GuestReceptionPoolRegistry.LiteratureReceptionFloorBgmTracks,
            volumeScale: 0.85f,
            GuestReceptionPoolRegistry.StandardRoundThresholds),
        [typeof(FuneralOfTheDeadButterfliesEncounter)] = EncounterBgmConfig.DeathBased(
            "FuneralOfTheDeadButterfliesBGM",
            "res://audio/bgm/literature_reception_floor/literature_reception_floor_1.ogg"),
        [typeof(AddictedEmployeeWeak)] = EncounterBgmConfig.RoundBased(
            "AddictedEmployeeBGM",
            GuestReceptionPoolRegistry.YesodReceptionFloorBgmTracks,
            volumeScale: 0.85f,
            GuestReceptionPoolRegistry.StandardRoundThresholds),
        [typeof(AddictedEmployeeStrong)] = EncounterBgmConfig.RoundBased(
            "AddictedEmployeeBGM",
            GuestReceptionPoolRegistry.YesodReceptionFloorBgmTracks,
            volumeScale: 0.85f,
            GuestReceptionPoolRegistry.StandardRoundThresholds),
        [typeof(TomerryEncounter)] = EncounterBgmConfig.DeathBased(
            "TomerryBGM",
            "res://audio/bgm/warp_train/from_a_place_of_love.ogg"),
        [typeof(CosmicFragmentWeak)] = EncounterBgmConfig.RoundBased(
            "CosmicFragmentBGM",
            GuestReceptionPoolRegistry.NetzachReceptionFloorBgmTracks,
            volumeScale: 0.85f,
            GuestReceptionPoolRegistry.StandardRoundThresholds),
        [typeof(GalaxyChildWeak)] = EncounterBgmConfig.RoundBased(
            "GalaxyChildBGM",
            GuestReceptionPoolRegistry.NetzachReceptionFloorBgmTracks,
            volumeScale: 0.85f,
            GuestReceptionPoolRegistry.StandardRoundThresholds),
        [typeof(SpinyBusWeak)] = EncounterBgmConfig.RoundBased(
            "SpinyBusBGM",
            GuestReceptionPoolRegistry.NetzachReceptionFloorBgmTracks,
            volumeScale: 0.85f,
            GuestReceptionPoolRegistry.StandardRoundThresholds),
        [typeof(BigBadWolfWeak)] = EncounterBgmConfig.RoundBased(
            "BigBadWolfBGM",
            GuestReceptionPoolRegistry.LanguageReceptionFloorBgmTracks,
            volumeScale: 0.85f,
            GuestReceptionPoolRegistry.StandardRoundThresholds),
        [typeof(SmilingBodiesStrong)] = EncounterBgmConfig.RoundBased(
            "SmilingBodiesBGM",
            GuestReceptionPoolRegistry.LanguageReceptionFloorBgmTracks,
            volumeScale: 0.85f,
            GuestReceptionPoolRegistry.StandardRoundThresholds),
        [typeof(DespairKnightStrong)] = EncounterBgmConfig.RoundBased(
            "DespairKnightBGM",
            GuestReceptionPoolRegistry.NaturalReceptionFloorBgmTracks,
            volumeScale: 0.85f,
            GuestReceptionPoolRegistry.StandardRoundThresholds),
        [typeof(WrathServantStrong)] = EncounterBgmConfig.RoundBased(
            "WrathServantBGM",
            GuestReceptionPoolRegistry.NaturalReceptionFloorBgmTracks,
            volumeScale: 0.85f,
            GuestReceptionPoolRegistry.StandardRoundThresholds),
        [typeof(ScarecrowSearchingForWisdomWeak)] = EncounterBgmConfig.RoundBased(
            "ScarecrowSearchingForWisdomBGM",
            GuestReceptionPoolRegistry.SocialSciencesReceptionFloorBgmTracks,
            volumeScale: 0.85f,
            GuestReceptionPoolRegistry.StandardRoundThresholds),
        [typeof(HeartOfAspirationWeak)] = EncounterBgmConfig.RoundBased(
            "HeartOfAspirationBGM",
            GuestReceptionPoolRegistry.GeneralReceptionFloorBgmTracks,
            volumeScale: 0.85f,
            GuestReceptionPoolRegistry.StandardRoundThresholds),
        [typeof(BurrowingHeavenWeak)] = EncounterBgmConfig.RoundBased(
            "BurrowingHeavenBGM",
            GuestReceptionPoolRegistry.ReligionReceptionFloorBgmTracks,
            volumeScale: 0.85f,
            GuestReceptionPoolRegistry.StandardRoundThresholds),
        [typeof(WarmheartedWoodsmanStrong)] = EncounterBgmConfig.RoundBased(
            "WarmheartedWoodsmanBGM",
            GuestReceptionPoolRegistry.SocialSciencesReceptionFloorBgmTracks,
            volumeScale: 0.85f,
            GuestReceptionPoolRegistry.StandardRoundThresholds),
        [typeof(PriceOfSilenceStrong)] = EncounterBgmConfig.RoundBased(
            "PriceOfSilenceBGM",
            GuestReceptionPoolRegistry.ReligionReceptionFloorBgmTracks,
            volumeScale: 0.85f,
            GuestReceptionPoolRegistry.StandardRoundThresholds),
        [typeof(BigBirdStrong)] = EncounterBgmConfig.RoundBased(
            "BigBirdBGM",
            GuestReceptionPoolRegistry.PhilosophyReceptionFloorBgmTracks,
            volumeScale: 0.85f,
            GuestReceptionPoolRegistry.StandardRoundThresholds),
        [typeof(BlueStarStrong)] = EncounterBgmConfig.RoundBased(
            "BlueStarBGM",
            GuestReceptionPoolRegistry.ReligionReceptionFloorBgmTracks,
            volumeScale: 0.85f,
            GuestReceptionPoolRegistry.StandardRoundThresholds),
        [typeof(PunishingBirdStrong)] = EncounterBgmConfig.RoundBased(
            "PunishingBirdBGM",
            GuestReceptionPoolRegistry.PhilosophyReceptionFloorBgmTracks,
            volumeScale: 0.85f,
            GuestReceptionPoolRegistry.StandardRoundThresholds),
        [typeof(JudgementBirdElite)] = EncounterBgmConfig.RoundBased(
            "JudgementBirdBGM",
            GuestReceptionPoolRegistry.PhilosophyReceptionFloorBgmTracks,
            volumeScale: 0.85f,
            GuestReceptionPoolRegistry.StandardRoundThresholds),
    };

    private static readonly Dictionary<string, EncounterBgmConfig> GuestLayerConfigByScenePath =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [NormalizeResPath(GuestReceptionPoolRegistry.GeneralReceptionFloorLayerScenePath)] = EncounterBgmConfig.RoundBased(
                "GeneralReceptionFloorBGM",
                GuestReceptionPoolRegistry.GeneralReceptionFloorBgmTracks,
                volumeScale: 0.90f,
                GuestReceptionPoolRegistry.StandardRoundThresholds),
            [NormalizeResPath(GuestReceptionPoolRegistry.ReligionReceptionFloorLayerScenePath)] = EncounterBgmConfig.RoundBased(
                "ReligionReceptionFloorBGM",
                GuestReceptionPoolRegistry.ReligionReceptionFloorBgmTracks,
                volumeScale: 0.85f,
                GuestReceptionPoolRegistry.StandardRoundThresholds),
            [NormalizeResPath(GuestReceptionPoolRegistry.LiteratureReceptionFloorLayerScenePath)] = EncounterBgmConfig.RoundBased(
                "LiteratureReceptionFloorBGM",
                GuestReceptionPoolRegistry.LiteratureReceptionFloorBgmTracks,
                volumeScale: 0.85f,
                GuestReceptionPoolRegistry.StandardRoundThresholds),
            [NormalizeResPath(GuestReceptionPoolRegistry.NaturalReceptionFloorLayerScenePath)] = EncounterBgmConfig.RoundBased(
                "NaturalReceptionFloorBGM",
                GuestReceptionPoolRegistry.NaturalReceptionFloorBgmTracks,
                volumeScale: 0.85f,
                GuestReceptionPoolRegistry.StandardRoundThresholds),
            [NormalizeResPath(GuestReceptionPoolRegistry.LanguageReceptionFloorLayerScenePath)] = EncounterBgmConfig.RoundBased(
                "LanguageReceptionFloorBGM",
                GuestReceptionPoolRegistry.LanguageReceptionFloorBgmTracks,
                volumeScale: 0.85f,
                GuestReceptionPoolRegistry.StandardRoundThresholds),
            [NormalizeResPath(GuestReceptionPoolRegistry.YesodReceptionFloorLayerScenePath)] = EncounterBgmConfig.RoundBased(
                "YesodReceptionFloorBGM",
                GuestReceptionPoolRegistry.YesodReceptionFloorBgmTracks,
                volumeScale: 0.85f,
                GuestReceptionPoolRegistry.StandardRoundThresholds)
        };

    private static readonly HashSet<Creature> RegisteredCreatures = new();

    private static CombatStateLike? _activeCombatState;
    private static Type? _activeEncounterType;
    private static EncounterBgmConfig? _activeConfig;
    private static bool _isRunning;
    private static bool _isTransitioning;
    private static int _currentTrackIndex;
    private static int _targetTrackIndex;
    private static float _activeMaxVolumeDb = MaxVolumeDb;
    private static SuspendedSessionSnapshot? _suspendedSession;

    private static Node? _hostNode;
    private static AudioStreamPlayer? _playerA;
    private static AudioStreamPlayer? _playerB;
    private static AudioStreamPlayer? _activePlayer;
    private static AudioStreamPlayer? _inactivePlayer;
    private static Tween? _fadeTween;
    private static readonly FieldInfo? RunMusicCurrentAmbienceField =
        typeof(NRunMusicController).GetField("_currentAmbience", BindingFlags.Instance | BindingFlags.NonPublic);

    private static bool _isCombatEndSubscribed;
    private static bool _isCombatSetUpSubscribed;
    private static bool _isTurnStartedSubscribed;
    private static bool _isRoomExitedSubscribed;

    public static void RegisterMonster(Creature creature)
    {
        if (!LibraryOfRuinaSettings.RuntimeSideEffectsEnabled
            || ReverberationEnsembleBgmController.IsActScope)
        {
            return;
        }

        CombatStateLike? CombatState = creature.CombatState;
        if (CombatState?.Encounter == null)
        {
            return;
        }

        if (AbnormalityEliteBgmController.HasBgmForCombat(CombatState))
        {
            return;
        }

        Type encounterType = CombatState.Encounter.GetType();
        if (!ConfigByEncounterType.TryGetValue(encounterType, out EncounterBgmConfig? defaultConfig))
        {
            return;
        }

        EncounterBgmConfig resolvedConfig = ResolveConfigForEncounter(CombatState, defaultConfig, out string? matchedLayerPath);
        string monsterId = creature.Monster?.Id.Entry ?? "UNKNOWN_MONSTER";
        if (matchedLayerPath != null)
        {
            Log.Info("[" + resolvedConfig.LogTag + "] Matched background layer binding: " + matchedLayerPath);
        }

        Log.Info("[" + resolvedConfig.LogTag + "] RegisterMonster: " + monsterId);
        EnsureSessionStarted(resolvedConfig, encounterType, CombatState);
        if (RegisteredCreatures.Add(creature))
        {
            creature.Died += OnMonsterDied;
        }
    }

    public static void UnregisterMonster(Creature creature)
    {
        if (RegisteredCreatures.Remove(creature))
        {
            creature.Died -= OnMonsterDied;
        }
    }

    public static bool HasBgmForEncounter(CombatStateLike? CombatState)
    {
        if (!LibraryOfRuinaSettings.RuntimeSideEffectsEnabled
            || ReverberationEnsembleBgmController.IsActScope)
        {
            return false;
        }

        if (CombatState?.Encounter == null)
        {
            return false;
        }

        return ConfigByEncounterType.ContainsKey(CombatState.Encounter.GetType());
    }

    internal static bool IsRunning => _isRunning;

    public static void ForceCurrentEncounterTrack(string trackPath, string logTag, float volumeScale = 0.85f)
    {
        if (!LibraryOfRuinaSettings.RuntimeSideEffectsEnabled)
        {
            return;
        }

        if (!_isRunning || _activeCombatState == null || _activeEncounterType == null)
        {
            return;
        }

        var config = EncounterBgmConfig.RoundBased(
            logTag,
            new[] { trackPath },
            volumeScale);
        Log.Info("[" + logTag + "] Force current encounter track: " + trackPath);
        EnsureSessionStarted(config, _activeEncounterType, _activeCombatState, forceRestart: true);
    }

    public static void RefreshCurrentEncounterTrack()
    {
        if (!_isRunning || _activeCombatState == null || _activeConfig == null)
        {
            return;
        }

        int resolvedTarget = ResolveTargetTrackIndex(_activeConfig, _activeCombatState.RoundNumber);
        if (resolvedTarget <= _targetTrackIndex)
        {
            return;
        }

        _targetTrackIndex = resolvedTarget;
        Log.Info(
            "[" + _activeConfig.LogTag + "] Dynamic phase refresh, target track -> "
            + _targetTrackIndex);
        TryAdvanceTrack();
    }

    public static void OnRunCleaningUp(bool graceful)
    {
        if (!_isRunning || _activeCombatState == null || _activeCombatState.Encounter == null)
        {
            return;
        }

        if (!graceful || !CombatManager.Instance.IsInProgress)
        {
            StopSession(restoreRunMusic: false);
            return;
        }

        string logTag = _activeConfig?.LogTag ?? "EncounterBGM";
        Log.Info("[" + logTag + "] Run cleanup while combat BGM is active, suspending session.");
        StopSession(restoreRunMusic: false, preserveSuspendedSession: true);
    }

    public static void StopRuntimeSession()
    {
        if (_isRunning)
        {
            StopSession(restoreRunMusic: true);
        }
    }

    private static EncounterBgmConfig ResolveConfigForEncounter(
        CombatStateLike CombatState,
        EncounterBgmConfig defaultConfig,
        out string? matchedLayerPath)
    {
        matchedLayerPath = null;
        if (CombatState.Encounter == null)
        {
            return defaultConfig;
        }

        Type encounterType = CombatState.Encounter.GetType();

        if (!GuestReceptionPoolRegistry.IsGuestEncounterType(encounterType))
        {
            return defaultConfig;
        }

        if (TryResolveGuestLayerConfig(CombatState, out EncounterBgmConfig? layerConfig, out string? matchedGuestLayerPath))
        {
            matchedLayerPath = matchedGuestLayerPath;
            return layerConfig!;
        }

        return defaultConfig;
    }

    private static bool TryResolveGuestLayerConfig(
        CombatStateLike CombatState,
        out EncounterBgmConfig? config,
        out string? matchedLayerPath)
    {
        config = null;
        matchedLayerPath = null;

        HashSet<string> selectedLayerPaths = ResolveSelectedLayerPaths(CombatState);
        if (selectedLayerPaths.Count == 0)
        {
            return false;
        }

        foreach (string selectedLayerPath in selectedLayerPaths)
        {
            if (!GuestLayerConfigByScenePath.TryGetValue(selectedLayerPath, out EncounterBgmConfig? layerConfig))
            {
                continue;
            }

            config = layerConfig;
            matchedLayerPath = selectedLayerPath;
            return true;
        }

        return false;
    }

    private static HashSet<string> ResolveSelectedLayerPaths(CombatStateLike CombatState)
    {
        var selectedLayerPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (CombatState.Encounter == null)
        {
            return selectedLayerPaths;
        }

        try
        {
            IEnumerable<string> assetPaths = CombatState.Encounter.GetAssetPaths(CombatState.RunState);
            foreach (string assetPath in assetPaths)
            {
                if (string.IsNullOrWhiteSpace(assetPath))
                {
                    continue;
                }

                string normalizedPath = NormalizeResPath(assetPath);
                if (!normalizedPath.Contains("/layers/", StringComparison.OrdinalIgnoreCase) ||
                    !normalizedPath.EndsWith(".tscn", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                selectedLayerPaths.Add(normalizedPath);
            }
        }
        catch (Exception ex)
        {
            string encounterName = CombatState.Encounter.Id.Entry;
            Log.Warn(
                "[EncounterBGM] Failed to resolve selected background layers for encounter " +
                encounterName + ": " + ex.Message);
        }

        return selectedLayerPaths;
    }

    private static void EnsureSessionStarted(
        EncounterBgmConfig config,
        Type encounterType,
        CombatStateLike CombatState,
        bool forceRestart = false)
    {
        if (ReverberationEnsembleBgmController.IsActScope)
        {
            return;
        }

        if (!forceRestart &&
            _isRunning &&
            ReferenceEquals(_activeCombatState, CombatState) &&
            _activeEncounterType == encounterType)
        {
            return;
        }

        if (_isRunning || _activeCombatState != null)
        {
            StopSession(restoreRunMusic: false);
        }

        _activeCombatState = CombatState;
        _activeEncounterType = encounterType;
        _activeConfig = config;
        string sessionKey = BuildSessionKey(CombatState, encounterType);
        int initialTrackIndex = 0;
        if (CombatState.Encounter is NaturalFloorLiberation.NaturalFloorLiberationEncounter { CurrentPhase: 5 })
        {
            initialTrackIndex = ResolveLiberationPhaseTrackIndex(5);
        }

        float initialPlaybackPositionSeconds = 0f;
        if (TryConsumeSuspendedSession(sessionKey, encounterType, config, out SuspendedSessionSnapshot? suspendedSession)
            && suspendedSession != null)
        {
            initialTrackIndex = suspendedSession.TrackIndex;
            initialPlaybackPositionSeconds = suspendedSession.PlaybackPositionSeconds;
            Log.Info(
                "[" + config.LogTag + "] Resuming suspended track " + initialTrackIndex +
                " at " + initialPlaybackPositionSeconds.ToString("0.00") + "s.");
        }

        _currentTrackIndex = initialTrackIndex;
        _targetTrackIndex = initialTrackIndex;
        _activeMaxVolumeDb = ResolveEffectiveMaxVolumeDb(config);

        EnsureHostAndPlayers();
        if (_hostNode == null || _activePlayer == null || _inactivePlayer == null)
        {
            return;
        }

        EnsureCombatEndSubscription();
        EnsureCombatSetUpSubscription();
        EnsureTurnStartedSubscription();
        EnsureRoomExitedSubscription();

        AudioStream? firstTrack = LoadTrack(config, _currentTrackIndex);
        if (firstTrack == null)
        {
            Log.Error("[" + config.LogTag + "] Failed to load initial track " + _currentTrackIndex + ".");
            return;
        }

        NRunMusicController.Instance?.StopMusic();
        StopAndResetPlayer(_activePlayer);
        StopAndResetPlayer(_inactivePlayer);
        _activePlayer.Stream = firstTrack;
        _activePlayer.VolumeDb = _activeMaxVolumeDb;
        _inactivePlayer.VolumeDb = MinVolumeDb;
        LibraryBgmPlaybackCoordinator.StopBeforePlayback(_activePlayer, _inactivePlayer);
        _activePlayer.Play(MathF.Max(0f, initialPlaybackPositionSeconds));
        _isRunning = true;
        Log.Info("[" + config.LogTag + "] Started track " + _currentTrackIndex + ". IsPlaying=" + _activePlayer.IsPlaying());

        _targetTrackIndex = Math.Max(_currentTrackIndex, ResolveTargetTrackIndex(config, CombatState.RoundNumber));
        TryAdvanceTrack();
    }

    private static void EnsureHostAndPlayers()
    {
        if (!IsAlive(_hostNode))
        {
            _hostNode = NRun.Instance ?? (Node?)NGame.Instance;
        }

        if (_hostNode == null)
        {
            Log.Error("[EncounterBGM] Host node is null.");
            return;
        }

        if (!IsAlive(_playerA))
        {
            _playerA = CreatePlayer("EncounterBgmPlayerA");
            _hostNode.AddChildSafely(_playerA);
        }

        if (!IsAlive(_playerB))
        {
            _playerB = CreatePlayer("EncounterBgmPlayerB");
            _hostNode.AddChildSafely(_playerB);
        }

        _activePlayer = _playerA;
        _inactivePlayer = _playerB;
    }

    private static AudioStreamPlayer CreatePlayer(string name)
    {
        return new AudioStreamPlayer
        {
            Name = name,
            Bus = "Master",
            VolumeDb = MinVolumeDb
        };
    }

    private static AudioStream? LoadTrack(EncounterBgmConfig config, int trackIndex)
    {
        if (trackIndex < 0 || trackIndex >= config.TrackPaths.Length)
        {
            return null;
        }

        string trackPath = config.TrackPaths[trackIndex];
        AudioStream? stream = ResourceLoader.Load<AudioStream>(trackPath);
        if (stream == null)
        {
            Log.Error("[" + config.LogTag + "] ResourceLoader failed: " + trackPath);
            return null;
        }

        Log.Info("[" + config.LogTag + "] Loaded track resource: " + trackPath);
        if (stream is AudioStreamOggVorbis ogg)
        {
            AudioStreamOggVorbis looped = (AudioStreamOggVorbis)ogg.Duplicate();
            looped.Loop = true;
            return looped;
        }

        return stream;
    }

    private static void OnMonsterDied(Creature creature)
    {
        if (!_isRunning || _activeCombatState == null || _activeConfig == null)
        {
            return;
        }

        if (!ReferenceEquals(creature.CombatState, _activeCombatState))
        {
            return;
        }

        if (_activeConfig.ProgressionMode != TrackProgressionMode.OnMonsterDeath)
        {
            return;
        }

        if (_targetTrackIndex < _activeConfig.TrackPaths.Length - 1)
        {
            _targetTrackIndex++;
            Log.Info("[" + _activeConfig.LogTag + "] Monster died, target track -> " + _targetTrackIndex);
            TryAdvanceTrack();
        }
    }

    private static void EnsureTurnStartedSubscription()
    {
        if (_isTurnStartedSubscribed)
        {
            return;
        }

        CombatManager? combatManager = CombatManager.Instance;
        if (combatManager == null)
        {
            return;
        }

        combatManager.TurnStarted += OnTurnStarted;
        _isTurnStartedSubscribed = true;
    }

    private static void OnTurnStarted(CombatStateLike state)
    {
        if (!LibraryOfRuinaSettings.RuntimeSideEffectsEnabled)
        {
            StopSession(restoreRunMusic: true);
            return;
        }

        if (!_isRunning || _activeCombatState == null || _activeConfig == null)
        {
            return;
        }

        if (!ReferenceEquals(state, _activeCombatState))
        {
            return;
        }

        RefreshActiveVolumeFromGlobalSettings();

        if (_activeConfig.ProgressionMode is not (TrackProgressionMode.OnRoundThreshold or TrackProgressionMode.Dynamic))
        {
            return;
        }

        int resolvedTarget = ResolveTargetTrackIndex(_activeConfig, state.RoundNumber);
        if (resolvedTarget <= _targetTrackIndex)
        {
            return;
        }

        _targetTrackIndex = resolvedTarget;
        Log.Info(
            "[" + _activeConfig.LogTag + "] Round " + state.RoundNumber +
            ", target track -> " + _targetTrackIndex);
        TryAdvanceTrack();
    }

    private static void TryAdvanceTrack()
    {
        if (!_isRunning || _isTransitioning || _activeConfig == null)
        {
            return;
        }

        if (_targetTrackIndex <= _currentTrackIndex)
        {
            return;
        }

        StartCrossfade(_currentTrackIndex + 1);
    }

    private static void StartCrossfade(int nextTrackIndex)
    {
        if (_hostNode == null || _activePlayer == null || _inactivePlayer == null || _activeConfig == null)
        {
            return;
        }

        string logTag = _activeConfig.LogTag;
        AudioStream? nextTrack = LoadTrack(_activeConfig, nextTrackIndex);
        if (nextTrack == null)
        {
            _currentTrackIndex = nextTrackIndex;
            TryAdvanceTrack();
            return;
        }

        KillFadeTween();

        AudioStreamPlayer fromPlayer = _activePlayer;
        AudioStreamPlayer toPlayer = _inactivePlayer;
        if (!IsAlive(fromPlayer) || !IsAlive(toPlayer))
        {
            return;
        }

        toPlayer.Stop();
        toPlayer.Stream = nextTrack;
        toPlayer.VolumeDb = MinVolumeDb;
        LibraryBgmPlaybackCoordinator.StopBeforePlayback(fromPlayer, toPlayer);
        toPlayer.Play();

        _isTransitioning = true;

        _fadeTween = _hostNode.CreateTween();
        _fadeTween.SetParallel();
        _fadeTween.TweenProperty(fromPlayer, "volume_db", MinVolumeDb, FadeDurationSeconds);
        _fadeTween.TweenProperty(toPlayer, "volume_db", _activeMaxVolumeDb, FadeDurationSeconds);
        _fadeTween.Finished += () =>
        {
            fromPlayer.Stop();
            fromPlayer.VolumeDb = MinVolumeDb;
            _activePlayer = toPlayer;
            _inactivePlayer = fromPlayer;
            _currentTrackIndex = nextTrackIndex;
            _isTransitioning = false;
            _fadeTween = null;
            Log.Info("[" + logTag + "] Crossfade finished. Current track=" + _currentTrackIndex);
            TryAdvanceTrack();
        };
    }

    private static void EnsureCombatEndSubscription()
    {
        if (_isCombatEndSubscribed)
        {
            return;
        }

        CombatManager? combatManager = CombatManager.Instance;
        if (combatManager == null)
        {
            return;
        }

        combatManager.CombatEnded += OnCombatEnded;
        _isCombatEndSubscribed = true;
    }

    private static void EnsureCombatSetUpSubscription()
    {
        if (_isCombatSetUpSubscribed)
        {
            return;
        }

        CombatManager? combatManager = CombatManager.Instance;
        if (combatManager == null)
        {
            return;
        }

        combatManager.CombatSetUp += OnCombatSetUp;
        _isCombatSetUpSubscribed = true;
    }

    private static void EnsureRoomExitedSubscription()
    {
        if (_isRoomExitedSubscribed)
        {
            return;
        }

        RunManager? runManager = RunManager.Instance;
        if (runManager == null)
        {
            return;
        }

        runManager.RoomExited += OnRoomExited;
        _isRoomExitedSubscribed = true;
    }

    private static void OnCombatEnded(CombatRoom room)
    {
        if (_activeEncounterType == null || room.Encounter == null || room.Encounter.GetType() != _activeEncounterType)
        {
            return;
        }

        string logTag = _activeConfig?.LogTag ?? "EncounterBGM";
        Log.Info("[" + logTag + "] Combat ended.");
        bool shouldRestoreRunMusic = !NonCombatRunBgmController.IsCombatLossPending
            && (RunManager.Instance?.IsInProgress ?? false);
        StopSession(shouldRestoreRunMusic);
    }

    private static void OnCombatSetUp(CombatStateLike state)
    {
        if (!_isRunning || _activeCombatState == null)
        {
            return;
        }

        if (ReferenceEquals(state, _activeCombatState))
        {
            return;
        }

        string logTag = _activeConfig?.LogTag ?? "EncounterBGM";
        string nextEncounter = state.Encounter?.Id.Entry ?? state.Encounter?.GetType().Name ?? "UNKNOWN_ENCOUNTER";
        Log.Info("[" + logTag + "] New combat setup detected (" + nextEncounter + "), stopping previous BGM session.");
        StopSession(restoreRunMusic: false);
    }

    private static void OnRoomExited()
    {
        if (!_isRunning)
        {
            return;
        }

        string logTag = _activeConfig?.LogTag ?? "EncounterBGM";
        Log.Info("[" + logTag + "] Room exited while BGM session active, forcing stop.");
        StopSession(restoreRunMusic: false, preserveSuspendedSession: true);
    }

    private static void StopSession(bool restoreRunMusic, bool preserveSuspendedSession = false)
    {
        string logTag = _activeConfig?.LogTag ?? "EncounterBGM";
        SuspendedSessionSnapshot? suspendedSession = preserveSuspendedSession ? CaptureSuspendedSession() : null;

        KillFadeTween();
        _isTransitioning = false;
        _isRunning = false;
        _currentTrackIndex = 0;
        _targetTrackIndex = 0;
        _activeMaxVolumeDb = MaxVolumeDb;
        _activeCombatState = null;
        _activeEncounterType = null;
        _activeConfig = null;
        if (preserveSuspendedSession)
        {
            _suspendedSession = suspendedSession;
            if (_suspendedSession != null)
            {
                Log.Info(
                    "[" + logTag + "] Suspended track " + _suspendedSession.TrackIndex +
                    " at " + _suspendedSession.PlaybackPositionSeconds.ToString("0.00") + "s.");
            }
        }
        else
        {
            _suspendedSession = null;
        }

        Log.Info("[" + logTag + "] StopSession restoreRunMusic=" + restoreRunMusic);

        foreach (Creature creature in RegisteredCreatures)
        {
            creature.Died -= OnMonsterDied;
        }

        RegisteredCreatures.Clear();

        StopAndResetPlayer(_playerA);
        StopAndResetPlayer(_playerB);
        DisposePlayer(ref _playerA);
        DisposePlayer(ref _playerB);

        _activePlayer = null;
        _inactivePlayer = null;

        if (_isCombatEndSubscribed)
        {
            CombatManager? combatManager = CombatManager.Instance;
            if (combatManager != null)
            {
                combatManager.CombatEnded -= OnCombatEnded;
            }

            _isCombatEndSubscribed = false;
        }

        if (_isCombatSetUpSubscribed)
        {
            CombatManager? combatManager = CombatManager.Instance;
            if (combatManager != null)
            {
                combatManager.CombatSetUp -= OnCombatSetUp;
            }

            _isCombatSetUpSubscribed = false;
        }

        if (_isTurnStartedSubscribed)
        {
            CombatManager? combatManager = CombatManager.Instance;
            if (combatManager != null)
            {
                combatManager.TurnStarted -= OnTurnStarted;
            }

            _isTurnStartedSubscribed = false;
        }

        if (_isRoomExitedSubscribed)
        {
            RunManager? runManager = RunManager.Instance;
            if (runManager != null)
            {
                runManager.RoomExited -= OnRoomExited;
            }

            _isRoomExitedSubscribed = false;
        }

        if (restoreRunMusic)
        {
            RestoreRunMusicSafely(logTag);
        }
    }

    private static SuspendedSessionSnapshot? CaptureSuspendedSession()
    {
        if (_activeCombatState == null || _activeEncounterType == null || _activeConfig == null)
        {
            return null;
        }

        string sessionKey = BuildSessionKey(_activeCombatState, _activeEncounterType);
        int trackIndex = _currentTrackIndex;
        float playbackPositionSeconds = 0f;
        AudioStreamPlayer? player = _activePlayer;
        if (IsAlive(player) && player!.Stream != null)
        {
            try
            {
                if (_isTransitioning && IsAlive(_inactivePlayer) && _inactivePlayer!.Stream != null)
                {
                    player = _inactivePlayer;
                    trackIndex = Math.Min(_currentTrackIndex + 1, _activeConfig.TrackPaths.Length - 1);
                }

                if (IsAlive(player))
                {
                    playbackPositionSeconds = MathF.Max(0f, player!.GetPlaybackPosition());
                }
            }
            catch (ObjectDisposedException)
            {
                playbackPositionSeconds = 0f;
            }
        }

        return new SuspendedSessionSnapshot(sessionKey, _activeEncounterType, trackIndex, playbackPositionSeconds);
    }

    private static bool TryConsumeSuspendedSession(
        string sessionKey,
        Type encounterType,
        EncounterBgmConfig config,
        out SuspendedSessionSnapshot? suspendedSession)
    {
        suspendedSession = null;
        SuspendedSessionSnapshot? candidate = _suspendedSession;
        _suspendedSession = null;
        if (candidate == null)
        {
            return false;
        }

        if (candidate.SessionKey != sessionKey || candidate.EncounterType != encounterType)
        {
            return false;
        }

        if (candidate.TrackIndex < 0 || candidate.TrackIndex >= config.TrackPaths.Length)
        {
            return false;
        }

        suspendedSession = candidate;
        return true;
    }

    private static string BuildSessionKey(CombatStateLike CombatState, Type encounterType)
    {
        string seed = CombatState.RunState.Rng.StringSeed;
        string location = CombatState.RunState.CurrentActIndex + ":" + (CombatState.RunState.CurrentMapCoord?.ToString() ?? "null");
        string encounterId = CombatState.Encounter?.Id.Entry ?? encounterType.FullName ?? encounterType.Name;
        return seed + "|" + location + "|" + encounterId;
    }

    private static void RestoreRunMusicSafely(string logTag)
    {
        if (MainMenuBgmController.TryResumeOwnedRunMusic())
        {
            return;
        }

        NRunMusicController? runMusicController = NRunMusicController.Instance;
        if (runMusicController == null)
        {
            Log.Warn("[" + logTag + "] Run music restore skipped: NRunMusicController.Instance is null.");
            return;
        }

        bool ambienceCacheReset = ResetRunMusicAmbienceCache(runMusicController, logTag);
        try
        {
            runMusicController.UpdateMusic();
            runMusicController.UpdateTrack();
            Log.Info("[" + logTag + "] Restored run music after encounter BGM session. ambienceCacheReset=" + ambienceCacheReset);
        }
        catch (Exception ex)
        {
            Log.Error("[" + logTag + "] Failed to restore run music: " + ex);
        }
    }

    
    private static bool ResetRunMusicAmbienceCache(NRunMusicController runMusicController, string logTag)
    {
        if (RunMusicCurrentAmbienceField == null)
        {
            Log.Error("[" + logTag + "] Failed to reset run music ambience cache: NRunMusicController._currentAmbience field was not found.");
            return false;
        }

        try
        {
            RunMusicCurrentAmbienceField.SetValue(runMusicController, null);
            Log.Info("[" + logTag + "] Reset run music ambience cache before restore.");
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("[" + logTag + "] Failed to reset run music ambience cache: " + ex);
            return false;
        }
    }

    private static bool IsAlive(GodotObject? obj)
    {
        return obj != null
            && GodotObject.IsInstanceValid(obj)
            && (obj is not Node node
                || (!node.IsQueuedForDeletion() && node.IsInsideTree()));
    }

    private static string NormalizeResPath(string path)
    {
        return path.Replace('\\', '/').Trim();
    }

    private static void KillFadeTween()
    {
        if (!IsAlive(_fadeTween))
        {
            _fadeTween = null;
            return;
        }

        try
        {
            _fadeTween!.Kill();
        }
        catch (ObjectDisposedException)
        {
        }
        finally
        {
            _fadeTween = null;
        }
    }

    private static void StopAndResetPlayer(AudioStreamPlayer? player)
    {
        if (!IsAlive(player))
        {
            return;
        }

        try
        {
            player!.Stop();
            player.VolumeDb = MinVolumeDb;
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private static void DisposePlayer(ref AudioStreamPlayer? player)
    {
        if (!IsAlive(player))
        {
            player = null;
            return;
        }

        try
        {
            player!.QueueFree();
        }
        catch (ObjectDisposedException)
        {
        }
        finally
        {
            player = null;
        }
    }

    private static float ResolveScaledMaxVolumeDb(float volumeScale)
    {
        float clampedScale = Mathf.Clamp(volumeScale, 0f, 1f);
        if (clampedScale <= 0f)
        {
            return MinVolumeDb;
        }

        if (Mathf.IsEqualApprox(clampedScale, 1f))
        {
            return MaxVolumeDb;
        }

        return MaxVolumeDb + Mathf.LinearToDb(clampedScale);
    }

    private static float ResolveEffectiveMaxVolumeDb(EncounterBgmConfig config)
    {
        float globalBgmScale = ResolveGlobalBgmVolumeScale();
        float effectiveScale = config.VolumeScale * globalBgmScale;
        return ResolveScaledMaxVolumeDb(effectiveScale);
    }

    private static void RefreshActiveVolumeFromGlobalSettings()
    {
        if (_activeConfig == null)
        {
            return;
        }

        float resolvedMaxVolumeDb = ResolveEffectiveMaxVolumeDb(_activeConfig);
        if (Mathf.IsEqualApprox(resolvedMaxVolumeDb, _activeMaxVolumeDb))
        {
            return;
        }

        _activeMaxVolumeDb = resolvedMaxVolumeDb;
        if (_isTransitioning)
        {
            return;
        }

        if (IsAlive(_activePlayer))
        {
            _activePlayer!.VolumeDb = _activeMaxVolumeDb;
        }
    }

    private static float ResolveGlobalBgmVolumeScale()
    {
        try
        {
            float rawVolume = SaveManager.Instance?.SettingsSave?.VolumeBgm ?? 1f;
            float clampedVolume = Mathf.Clamp(rawVolume, 0f, 1f);
            return Mathf.Pow(clampedVolume, 2f);
        }
        catch (Exception ex)
        {
            Log.Warn("[EncounterBGM] Failed to read global BGM volume, fallback to 1.0: " + ex.Message);
            return 1f;
        }
    }

    private static int ResolveTargetTrackIndex(EncounterBgmConfig config, int roundNumber)
    {
        if (config.ProgressionMode == TrackProgressionMode.Dynamic
            && config.DynamicTrackResolver != null
            && _activeCombatState != null)
        {
            return Math.Clamp(
                config.DynamicTrackResolver(_activeCombatState),
                0,
                config.TrackPaths.Length - 1);
        }

        if (config.ProgressionMode != TrackProgressionMode.OnRoundThreshold || config.RoundThresholds.Length == 0)
        {
            return 0;
        }

        int targetIndex = 0;
        for (int i = 0; i < config.RoundThresholds.Length; i++)
        {
            if (roundNumber < config.RoundThresholds[i])
            {
                break;
            }

            targetIndex = Math.Min(i + 1, config.TrackPaths.Length - 1);
        }

        return targetIndex;
    }

    private static int ResolveLiberationPhaseTrack(CombatStateLike combatState)
    {
        return combatState.Encounter is ILiberationPhaseBgmSource source
            ? ResolveLiberationPhaseTrackIndex(source.CurrentPhase)
            : 0;
    }

    private static int ResolveDynamicSourceTrack(CombatStateLike combatState)
    {
        int trackIndex = 0;
        foreach (Creature enemy in combatState.Enemies)
        {
            if (enemy.Monster is IEncounterDynamicBgmTrackSource source)
            {
                trackIndex = Math.Max(trackIndex, source.CurrentEncounterBgmTrackIndex);
            }
        }

        return trackIndex;
    }

    internal static int ResolveLiberationPhaseTrackIndex(int phase) =>
        Math.Clamp((Math.Max(1, phase) - 1) / 2, 0, 2);

}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.CleanUp), typeof(bool))]
internal static class EncounterBgmRunCleanupPatch
{
    private static void Prefix(bool graceful)
    {
        EncounterBgmController.OnRunCleaningUp(graceful);
    }
}
