using System;
using System.Linq;
using LibraryOfRuina.guests;
using LibraryOfRuina.specialguests;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.encounters;

internal static class GuestReceptionPoolRegistry
{
    internal const string SharedBackgroundTitle = "guest_reception_pool";

    internal const string GeneralReceptionFloorLayerScenePath =
        "res://scenes/backgrounds/guest_reception_pool/layers/guest_reception_pool_bg_00_general_reception_floor.tscn";

    internal const string ReligionReceptionFloorLayerScenePath =
        "res://scenes/backgrounds/guest_reception_pool/layers/guest_reception_pool_bg_00_religion_reception_floor.tscn";

    internal const string LiteratureReceptionFloorLayerScenePath =
        "res://scenes/backgrounds/guest_reception_pool/layers/guest_reception_pool_bg_00_literature_reception_floor.tscn";

    internal const string NaturalReceptionFloorLayerScenePath =
        "res://scenes/backgrounds/guest_reception_pool/layers/guest_reception_pool_bg_00_natural_reception_floor.tscn";

    internal const string LanguageReceptionFloorLayerScenePath =
        "res://scenes/backgrounds/guest_reception_pool/layers/guest_reception_pool_bg_00_language_reception_floor.tscn";

    internal const string YesodReceptionFloorLayerScenePath =
        "res://scenes/backgrounds/guest_reception_pool/layers/guest_reception_pool_bg_00_yesod_reception_floor.tscn";

    internal static readonly string[] GuestReceptionFloorLayerScenePaths =
    {
        GeneralReceptionFloorLayerScenePath,
        ReligionReceptionFloorLayerScenePath,
        LiteratureReceptionFloorLayerScenePath,
        NaturalReceptionFloorLayerScenePath,
        LanguageReceptionFloorLayerScenePath,
        YesodReceptionFloorLayerScenePath
    };

    internal static readonly string[] GeneralReceptionFloorBgmTracks =
    {
        "res://audio/bgm/general_reception_floor/general_reception_floor_1.ogg",
        "res://audio/bgm/general_reception_floor/general_reception_floor_2.ogg",
        "res://audio/bgm/general_reception_floor/general_reception_floor_3.ogg"
    };

    internal static readonly string[] ReligionReceptionFloorBgmTracks =
    {
        "res://audio/bgm/religion_reception_floor/religion_reception_floor_1.ogg",
        "res://audio/bgm/religion_reception_floor/religion_reception_floor_2.ogg",
        "res://audio/bgm/religion_reception_floor/religion_reception_floor_3.ogg"
    };

    internal static readonly string[] LiteratureReceptionFloorBgmTracks =
    {
        "res://audio/bgm/literature_reception_floor/literature_reception_floor_1.ogg",
        "res://audio/bgm/literature_reception_floor/literature_reception_floor_2.ogg",
        "res://audio/bgm/literature_reception_floor/literature_reception_floor_3.ogg"
    };

    internal static readonly string[] NaturalReceptionFloorBgmTracks =
    {
        "res://audio/bgm/natural_reception_floor/natural_reception_floor_1.ogg",
        "res://audio/bgm/natural_reception_floor/natural_reception_floor_2.ogg",
        "res://audio/bgm/natural_reception_floor/natural_reception_floor_3.ogg"
    };

    internal static readonly string[] LanguageReceptionFloorBgmTracks =
    {
        "res://audio/bgm/language_reception_floor/GeburaBattle1.ogg",
        "res://audio/bgm/language_reception_floor/GeburaBattle2.ogg",
        "res://audio/bgm/language_reception_floor/GeburaBattle3.ogg"
    };

    internal static readonly string[] YesodReceptionFloorBgmTracks =
    {
        "res://audio/bgm/yesod_reception_floor/YesodBattle1.ogg",
        "res://audio/bgm/yesod_reception_floor/YesodBattle2.ogg",
        "res://audio/bgm/yesod_reception_floor/YesodBattle3.ogg"
    };

    internal static readonly string[] SocialSciencesReceptionFloorBgmTracks =
    {
        "res://audio/bgm/social_sciences_reception_floor/ChesedBattle1.ogg",
        "res://audio/bgm/social_sciences_reception_floor/ChesedBattle2.ogg",
        "res://audio/bgm/social_sciences_reception_floor/ChesedBattle3.ogg"
    };

    internal static readonly string[] NetzachReceptionFloorBgmTracks =
    {
        "res://audio/bgm/netzach_reception_floor/NetzachBattle1.ogg",
        "res://audio/bgm/netzach_reception_floor/NetzachBattle2.ogg",
        "res://audio/bgm/netzach_reception_floor/NetzachBattle3.ogg"
    };

    internal static readonly string[] PhilosophyReceptionFloorBgmTracks =
    {
        "res://audio/bgm/philosophy_reception_floor/BinahBattle1.ogg",
        "res://audio/bgm/philosophy_reception_floor/BinahBattle2.ogg",
        "res://audio/bgm/philosophy_reception_floor/BinahBattle3.ogg"
    };

    internal static readonly int[] StandardRoundThresholds = { 4, 7 };

    private static readonly Queue<string> LayerCycleQueue = new();

    private static ulong? _activeRunSeed;
    private static string? _lastSelectedLayerScenePath;

    internal static bool IsGuestEncounterType(Type encounterType)
    {
        return typeof(IGuestReceptionEncounter).IsAssignableFrom(encounterType);
    }

    /// <summary>
    /// Returns only regular guest encounters registered by the current Act.
    /// This preserves each encounter's native act, normal/elite, and weak-room
    /// appearance conditions.  Future guests can add an extra run-state gate
    /// through <see cref="IGuestReceptionEncounter.CanAppearForBookShadow"/>.
    /// </summary>
    internal static IReadOnlyList<EncounterModel> GetGuestEncounterCandidates(
        ActModel act,
        RoomType roomType,
        bool isWeak,
        IRunState runState)
    {
        IEnumerable<EncounterModel> pool = roomType switch
        {
            RoomType.Monster when isWeak => act.AllWeakEncounters,
            RoomType.Monster => act.AllRegularEncounters,
            RoomType.Elite => act.AllEliteEncounters,
            _ => Array.Empty<EncounterModel>()
        };

        return pool
            .Where(static encounter => encounter is IGuestReceptionEncounter)
            .Where(encounter => encounter.RoomType == roomType
                                && (roomType != RoomType.Monster || encounter.IsWeak == isWeak)
                                && encounter is not ISpecialGuestEncounterStage)
            .Where(encounter => ((IGuestReceptionEncounter)encounter)
                .CanAppearForBookShadow(runState))
            .DistinctBy(static encounter => encounter.Id)
            .OrderBy(static encounter => encounter.Id.Entry, StringComparer.Ordinal)
            .ToArray();
    }

    internal static string DrawNextLayerScenePath(Rng rng)
    {
        ResetLayerCycleIfRunChanged();

        if (LayerCycleQueue.Count == 0)
        {
            RebuildLayerCycleQueue(rng);
        }

        string selectedLayerPath = LayerCycleQueue.Dequeue();
        _lastSelectedLayerScenePath = selectedLayerPath;
        return selectedLayerPath;
    }

    private static void RebuildLayerCycleQueue(Rng rng)
    {
        List<string> shuffledLayers = GuestReceptionFloorLayerScenePaths.ToList();
        rng.Shuffle(shuffledLayers);

        if (!string.IsNullOrEmpty(_lastSelectedLayerScenePath) &&
            shuffledLayers.Count > 1 &&
            string.Equals(shuffledLayers[0], _lastSelectedLayerScenePath, StringComparison.OrdinalIgnoreCase))
        {
            string repeatedLayer = shuffledLayers[0];
            shuffledLayers.RemoveAt(0);
            shuffledLayers.Add(repeatedLayer);
        }

        LayerCycleQueue.Clear();
        foreach (string layerPath in shuffledLayers)
        {
            LayerCycleQueue.Enqueue(layerPath);
        }
    }

    private static void ResetLayerCycleIfRunChanged()
    {
        ulong? currentRunSeed = RunManager.Instance?.DebugOnlyGetState()?.Rng.Seed;
        if (_activeRunSeed == currentRunSeed)
        {
            return;
        }

        _activeRunSeed = currentRunSeed;
        LayerCycleQueue.Clear();
        _lastSelectedLayerScenePath = null;
    }
}
