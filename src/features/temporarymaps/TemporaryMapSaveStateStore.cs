using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace LibraryOfRuina.features.temporarymaps;

internal sealed record TemporaryMapRestoreSnapshot(
    string DefinitionId,
    int CurrentActIndex,
    int CurrentActFloor,
    MapCoord? CurrentMapCoord,
    SerializableActMap OriginalMap,
    IReadOnlyList<MapCoord> OriginalVisitedMapCoords,
    IReadOnlyList<int> OriginalMapPointHistoryCounts,
    int OriginalActFloor,
    SerializableActModel OriginalActSave);

internal static class TemporaryMapSaveStateStore
{
    private sealed class PersistedState
    {
        public int Version { get; set; }

        public long StartTime { get; set; }

        public string DefinitionId { get; set; } = string.Empty;

        public int CurrentActIndex { get; set; }

        public int CurrentActFloor { get; set; }

        public MapCoord? CurrentMapCoord { get; set; }

        public SerializableActMap? OriginalMap { get; set; }

        public List<MapCoord>? OriginalVisitedMapCoords { get; set; }

        public List<int>? OriginalMapPointHistoryCounts { get; set; }

        public int OriginalActFloor { get; set; }

        public SerializableActModel? OriginalActSave { get; set; }
    }

    private const int CurrentVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static void Save(SerializableRun run, RunState state, TemporaryMapSession session)
    {
        try
        {
            PersistedState persistedState = new()
            {
                Version = CurrentVersion,
                StartTime = run.StartTime,
                DefinitionId = session.Definition.Id,
                CurrentActIndex = state.CurrentActIndex,
                CurrentActFloor = state.ActFloor,
                CurrentMapCoord = state.CurrentMapCoord,
                OriginalMap = SerializableActMap.FromActMap(session.OriginalMap),
                OriginalVisitedMapCoords = session.OriginalVisitedMapCoords.ToList(),
                OriginalMapPointHistoryCounts = session.OriginalMapPointHistory.Select(static history => history.Count).ToList(),
                OriginalActFloor = session.OriginalActFloor,
                OriginalActSave = session.OriginalActSave
            };

            string statePath = GetStatePath();
            Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);
            File.WriteAllText(statePath, JsonSerializer.Serialize(persistedState, JsonOptions));
        }
        catch (Exception exception)
        {
            Log.Warn("[TemporaryMap] Failed to persist temporary-map restore state: " + exception);
        }
    }

    public static TemporaryMapRestoreSnapshot? Load(SerializableRun save)
    {
        try
        {
            string statePath = GetStatePath();
            if (!File.Exists(statePath))
            {
                return null;
            }

            PersistedState? persistedState = JsonSerializer.Deserialize<PersistedState>(
                File.ReadAllText(statePath),
                JsonOptions);
            if (persistedState == null
                || persistedState.Version != CurrentVersion
                || persistedState.StartTime != save.StartTime
                || persistedState.CurrentActIndex != save.CurrentActIndex
                || string.IsNullOrWhiteSpace(persistedState.DefinitionId)
                || persistedState.OriginalMap == null)
            {
                return null;
            }

            SerializableActModel? originalActSave = persistedState.OriginalActSave;
            if (originalActSave == null
                && persistedState.CurrentActIndex >= 0
                && persistedState.CurrentActIndex < save.Acts.Count)
            {
                originalActSave = save.Acts[persistedState.CurrentActIndex];
            }

            if (originalActSave == null)
            {
                return null;
            }

            return new TemporaryMapRestoreSnapshot(
                persistedState.DefinitionId,
                persistedState.CurrentActIndex,
                persistedState.CurrentActFloor,
                persistedState.CurrentMapCoord,
                persistedState.OriginalMap,
                persistedState.OriginalVisitedMapCoords ?? [],
                persistedState.OriginalMapPointHistoryCounts ?? [],
                persistedState.OriginalActFloor,
                originalActSave);
        }
        catch (Exception exception)
        {
            Log.Warn("[TemporaryMap] Failed to load temporary-map restore state: " + exception);
            return null;
        }
    }

    public static void Clear()
    {
        try
        {
            string statePath = GetStatePath();
            if (File.Exists(statePath))
            {
                File.Delete(statePath);
            }
        }
        catch (Exception exception)
        {
            Log.Warn("[TemporaryMap] Failed to clear temporary-map restore state: " + exception);
        }
    }

    private static string GetStatePath()
    {
        return ProjectSettings.GlobalizePath(
            SaveManager.Instance.GetProfileScopedPath(Path.Combine("LibraryOfRuina", "temporary_map_state.json")));
    }
}
