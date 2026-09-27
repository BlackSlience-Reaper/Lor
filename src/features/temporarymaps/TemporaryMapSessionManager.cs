using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;
using Environment = System.Environment;

namespace LibraryOfRuina.features.temporarymaps;

internal sealed record TemporaryMapSession(
    TemporaryMapDefinition Definition,
    ActMap OriginalMap,
    IReadOnlyList<MapCoord> OriginalVisitedMapCoords,
    IReadOnlyList<IReadOnlyList<MapPointHistoryEntry>> OriginalMapPointHistory,
    int OriginalActFloor,
    SerializableActModel OriginalActSave,
    MapCoord? EntryMapCoord,
    TemporaryMapActMap TemporaryMap);

internal sealed class TemporaryMapSessionStore
{
    private readonly Dictionary<RunState, TemporaryMapSession> _sessions = new();
    private readonly Dictionary<RunState, TemporaryMapRestoreSnapshot> _pendingRestoreSnapshots = new();
    private readonly HashSet<RunState> _pendingEntries = [];
    private readonly HashSet<RunState> _pendingReturns = [];
    private readonly HashSet<RunState> _suppressCompletionUntilTerminalProceed = [];

    public bool TryGetSession(RunState state, out TemporaryMapSession session)
    {
        return _sessions.TryGetValue(state, out session!);
    }

    public void SetSession(RunState state, TemporaryMapSession session)
    {
        _sessions[state] = session;
    }

    public bool RemoveSession(RunState state)
    {
        return _sessions.Remove(state);
    }

    public void QueueRestore(RunState state, TemporaryMapRestoreSnapshot snapshot)
    {
        _pendingRestoreSnapshots[state] = snapshot;
    }

    public bool TryGetPendingRestore(RunState state, out TemporaryMapRestoreSnapshot snapshot)
    {
        return _pendingRestoreSnapshots.TryGetValue(state, out snapshot!);
    }

    public bool RemovePendingRestore(RunState state)
    {
        return _pendingRestoreSnapshots.Remove(state);
    }

    public bool AddPendingEntry(RunState state)
    {
        return _pendingEntries.Add(state);
    }

    public bool RemovePendingEntry(RunState state)
    {
        return _pendingEntries.Remove(state);
    }

    public bool AddPendingReturn(RunState state)
    {
        return _pendingReturns.Add(state);
    }

    public bool RemovePendingReturn(RunState state)
    {
        return _pendingReturns.Remove(state);
    }

    public void SuppressCompletionUntilTerminalProceed(RunState state)
    {
        _suppressCompletionUntilTerminalProceed.Add(state);
    }

    public bool IsCompletionSuppressedUntilTerminalProceed(RunState state)
    {
        return _suppressCompletionUntilTerminalProceed.Contains(state);
    }

    public bool RemoveCompletionSuppression(RunState state)
    {
        return _suppressCompletionUntilTerminalProceed.Remove(state);
    }

    public void ClearForRunStarted(RunState state)
    {
        _pendingRestoreSnapshots.TryGetValue(state, out TemporaryMapRestoreSnapshot? pendingRestore);
        bool hadSuppression = _suppressCompletionUntilTerminalProceed.Contains(state);

        _sessions.Clear();
        _pendingRestoreSnapshots.Clear();
        _pendingEntries.Clear();
        _pendingReturns.Clear();
        _suppressCompletionUntilTerminalProceed.Clear();

        if (pendingRestore != null)
        {
            _pendingRestoreSnapshots[state] = pendingRestore;
        }

        if (hadSuppression)
        {
            _suppressCompletionUntilTerminalProceed.Add(state);
        }
    }
}

internal static class TemporaryMapSessionManager
{
    private static readonly TemporaryMapSessionStore SessionStore = new();

    public static bool IsActive(IRunState? runState)
    {
        return runState is RunState concreteState && SessionStore.TryGetSession(concreteState, out _);
    }

    public static bool TryGetSession(RunState state, out TemporaryMapSession session)
    {
        return SessionStore.TryGetSession(state, out session!);
    }

    public static bool IsCurrentTemporaryMap(ActMap map)
    {
        RunState? state = RunManager.Instance.DebugOnlyGetState();
        return state != null
            && SessionStore.TryGetSession(state, out TemporaryMapSession? session)
            && ReferenceEquals(session.TemporaryMap, map);
    }

    public static void SetSession(RunState state, TemporaryMapSession session)
    {
        SessionStore.SetSession(state, session);
    }

    public static bool AddPendingEntry(RunState state)
    {
        return SessionStore.AddPendingEntry(state);
    }

    public static bool RemovePendingEntry(RunState state)
    {
        return SessionStore.RemovePendingEntry(state);
    }

    public static void QueueRestoreFromSave(SerializableRun save, RunState state)
    {
        TemporaryMapRestoreSnapshot? snapshot = TemporaryMapSaveStateStore.Load(save);
        if (snapshot == null)
        {
            return;
        }

        SessionStore.QueueRestore(state, snapshot);
        if (ShouldWaitForTerminalRewardsProceed(save, snapshot))
        {
            SessionStore.SuppressCompletionUntilTerminalProceed(state);
            Log.Info("[TemporaryMap] Delaying temporary-map restore completion until terminal proceed.");
        }

        Log.Info("[TemporaryMap] Queued temporary-map restore from save for definition '" + snapshot.DefinitionId + "'.");
    }

    public static bool TryRestoreSavedSessionForCurrentRun(ActMap map)
    {
        RunState? state = RunManager.Instance.DebugOnlyGetState();
        if (state == null || !SessionStore.TryGetPendingRestore(state, out TemporaryMapRestoreSnapshot? snapshot))
        {
            return false;
        }

        if (!TemporaryMapRegistry.TryGetDefinition(snapshot.DefinitionId, out TemporaryMapDefinition? definition))
        {
            Log.Warn("[TemporaryMap] Missing temporary-map definition '" + snapshot.DefinitionId + "' while restoring save.");
            SessionStore.RemovePendingRestore(state);
            return false;
        }

        TemporaryMapActMap temporaryMap = TemporaryMapActMap.Create(definition);
        if (state.CurrentActIndex != snapshot.CurrentActIndex
            || snapshot.CurrentMapCoord is { } currentCoord && !MapContainsCoord(temporaryMap, currentCoord))
        {
            Log.Warn("[TemporaryMap] Ignored stale temporary-map restore snapshot for definition '" + definition.Id + "'.");
            SessionStore.RemovePendingRestore(state);
            return false;
        }

        ActMap originalMap = new SavedActMap(snapshot.OriginalMap);
        List<IReadOnlyList<MapPointHistoryEntry>> originalHistory =
            CopyHistoryByCounts(state.MapPointHistory, snapshot.OriginalMapPointHistoryCounts);

        SessionStore.SetSession(
            state,
            new TemporaryMapSession(
                definition,
                originalMap,
                snapshot.OriginalVisitedMapCoords.ToList(),
                originalHistory,
                snapshot.OriginalActFloor,
                snapshot.OriginalActSave,
                snapshot.CurrentMapCoord,
                temporaryMap));

        state.Map = temporaryMap;
        state.ActFloor = snapshot.CurrentActFloor;
        SessionStore.RemovePendingRestore(state);
        RefreshLocationSynchronizers(state);

        if (!ReferenceEquals(map, temporaryMap))
        {
            SetMapScreen(temporaryMap, state, snapshot.CurrentMapCoord.HasValue);
        }

        Log.Info("[TemporaryMap] Restored temporary-map session '" + definition.Id + "' from save.");
        return true;
    }

    public static bool TryRestoreCompletedCurrentRun()
    {
        RunState? state = RunManager.Instance.DebugOnlyGetState();
        if (state == null || !SessionStore.TryGetSession(state, out TemporaryMapSession? session))
        {
            return false;
        }

        if (SessionStore.IsCompletionSuppressedUntilTerminalProceed(state))
        {
            return false;
        }

        if (!state.CurrentMapCoord.HasValue
            || state.CurrentMapCoord.Value != session.TemporaryMap.TerminalCoord
            || !HasVisitedCoord(state.VisitedMapCoords, session.TemporaryMap.TerminalCoord))
        {
            return false;
        }

        return RequestRestoreOriginalMap(state, session);
    }

    public static void OnRunStarted(RunState state)
    {
        if (Environment.GetCommandLineArgs().Any(static arg =>
                arg.TrimStart('-').StartsWith("lor-verify-", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        SessionStore.ClearForRunStarted(state);
    }

    public static void OnRoomEntered()
    {
        RunState? state = RunManager.Instance.DebugOnlyGetState();
        if (state?.CurrentRoom is not MapRoom
            || !SessionStore.TryGetSession(state, out TemporaryMapSession? session)
            || SessionStore.IsCompletionSuppressedUntilTerminalProceed(state)
            || !HasVisitedCoord(state.VisitedMapCoords, session.TemporaryMap.TerminalCoord))
        {
            return;
        }

        RequestRestoreOriginalMap(state, session);
    }

    internal static Task RestoreOriginalMapFromSyncedAction(Player owner)
    {
        if (owner.RunState is not RunState state)
        {
            Log.Warn("[TemporaryMap] Tried to restore a temporary map without a run state.");
            return Task.CompletedTask;
        }

        if (!SessionStore.TryGetSession(state, out TemporaryMapSession? session))
        {
            SessionStore.RemovePendingReturn(state);
            Log.Warn("[TemporaryMap] Ignored a temporary-map return request because no session was active.");
            return Task.CompletedTask;
        }

        RestoreOriginalMap(state, session);
        return Task.CompletedTask;
    }

    public static void RefreshLocationSynchronizers(RunState state)
    {
        RunManager.Instance.MapSelectionSynchronizer.OnLocationChanged(state.MapLocation);
        RunManager.Instance.RunLocationTargetedBuffer.OnLocationChanged(state.RunLocation);
    }

    public static void SetMapScreen(ActMap map, RunState state, bool initMarker)
    {
        NMapScreen? mapScreen = NMapScreen.Instance;
        if (mapScreen == null)
        {
            return;
        }

        mapScreen.SetMap(map, state.Rng.Seed, clearDrawings: true);
        if (initMarker && state.CurrentMapCoord is { } currentCoord && map.HasPoint(currentCoord))
        {
            mapScreen.InitMarker(currentCoord);
        }

        mapScreen.SetTravelEnabled(true);
        mapScreen.RefreshAllMapPointVotes();
    }

    public static async Task AwaitNextProcessFrame()
    {
        if (NGame.Instance != null)
        {
            await NGame.Instance.AwaitProcessFrame();
        }
        else
        {
            await Task.Yield();
        }
    }

    public static void HideSpecialNodesAndPaths(NMapScreen screen, ActMap map)
    {
        if (map is not TemporaryMapActMap && !IsCurrentTemporaryMap(map))
        {
            return;
        }

        HideSpecialPoint(screen, "_startingPointNode");
        HideSpecialPoint(screen, "_bossPointNode");
        HideSpecialPaths(screen, map.StartingMapPoint.coord, map.BossMapPoint.coord);
    }

    public static bool HasVisitedCoord(IEnumerable<MapCoord> visitedCoords, MapCoord coord)
    {
        return visitedCoords.Any(visitedCoord => visitedCoord == coord);
    }

    public static bool MapContainsCoord(ActMap map, MapCoord coord)
    {
        if (map.HasPoint(coord)
            || map.StartingMapPoint.coord == coord
            || map.BossMapPoint.coord == coord)
        {
            return true;
        }

        return map.SecondBossMapPoint?.coord == coord;
    }

    private static void RestoreOriginalMap(RunState state, TemporaryMapSession session)
    {
        SessionStore.RemoveCompletionSuppression(state);
        SessionStore.RemoveSession(state);
        SessionStore.RemovePendingReturn(state);

        state.Map = session.OriginalMap;
        state.ClearVisitedMapCoordsDebug();
        foreach (MapCoord visitedCoord in session.OriginalVisitedMapCoords)
        {
            state.AddVisitedMapCoord(visitedCoord);
        }

        RestoreMapPointHistory(state, session.OriginalMapPointHistory);
        state.ActFloor = session.OriginalActFloor;
        TemporaryMapRunAccessor.RestoreActRooms(state, session.OriginalActSave);
        RefreshLocationSynchronizers(state);
        SetMapScreen(session.OriginalMap, state, state.CurrentMapCoord.HasValue);

        Log.Info("[TemporaryMap] Returned from temporary map '" + session.Definition.DisplayName + "'.");
    }

    private static bool RequestRestoreOriginalMap(RunState state, TemporaryMapSession session)
    {
        if (!SessionStore.AddPendingReturn(state))
        {
            return true;
        }

        var netService = RunManager.Instance.NetService;
        if (netService.Type is NetGameType.Client or NetGameType.Replay)
        {
            return true;
        }

        Player? actingPlayer = state.Players.FirstOrDefault(
            player => player.NetId == netService.NetId);
        if (actingPlayer == null)
        {
            if (netService.Type == NetGameType.Singleplayer)
            {
                Log.Warn("[TemporaryMap] Falling back to local singleplayer restore because no player was available.");
                RestoreOriginalMap(state, session);
                return true;
            }

            SessionStore.RemovePendingReturn(state);
            Log.Error("[TemporaryMap] Host player was unavailable; synchronized return will retry on the next trigger.");
            return false;
        }

        TemporaryMapAction.EnqueueReturn(actingPlayer);
        Log.Info("[TemporaryMap] Queued synced return from temporary map '" + session.Definition.DisplayName + "'.");
        return true;
    }

    private static void RestoreMapPointHistory(
        RunState state,
        IReadOnlyList<IReadOnlyList<MapPointHistoryEntry>> originalHistory)
    {
        if (!TemporaryMapRunAccessor.TryGetMapPointHistory(state, out List<List<MapPointHistoryEntry>> mapPointHistory))
        {
            Log.Warn("[TemporaryMap] Could not restore temporary-map map history.");
            return;
        }

        mapPointHistory.Clear();
        foreach (IReadOnlyList<MapPointHistoryEntry> history in originalHistory)
        {
            mapPointHistory.Add(history.ToList());
        }
    }

    private static bool ShouldWaitForTerminalRewardsProceed(
        SerializableRun save,
        TemporaryMapRestoreSnapshot snapshot)
    {
        if (save.PreFinishedRoom is not { IsPreFinished: true })
        {
            return false;
        }

        if (!TemporaryMapRegistry.TryGetDefinition(snapshot.DefinitionId, out TemporaryMapDefinition? definition))
        {
            return false;
        }

        return HasVisitedCoord(save.VisitedMapCoords, definition.RuntimeTerminalCoord);
    }

    private static List<IReadOnlyList<MapPointHistoryEntry>> CopyHistoryByCounts(
        IReadOnlyList<IReadOnlyList<MapPointHistoryEntry>> source,
        IReadOnlyList<int> counts)
    {
        List<IReadOnlyList<MapPointHistoryEntry>> copied = [];
        for (int i = 0; i < counts.Count; i++)
        {
            IReadOnlyList<MapPointHistoryEntry> history = i < source.Count ? source[i] : [];
            int clampedCount = Math.Clamp(counts[i], 0, history.Count);
            copied.Add(history.Take(clampedCount).ToList());
        }

        return copied;
    }

    private static void HideSpecialPoint(NMapScreen screen, string fieldName)
    {
        if (AccessTools.Field(typeof(NMapScreen), fieldName)?.GetValue(screen) is not CanvasItem canvasItem)
        {
            return;
        }

        canvasItem.Hide();
    }

    private static void HideSpecialPaths(NMapScreen screen, params MapCoord[] hiddenCoords)
    {
        if (AccessTools.Field(typeof(NMapScreen), "_paths")?.GetValue(screen) is not IDictionary paths)
        {
            return;
        }

        foreach (DictionaryEntry entry in paths)
        {
            if (entry.Key is not ValueTuple<MapCoord, MapCoord> path
                || (!IsSpecialCoord(path.Item1, hiddenCoords) && !IsSpecialCoord(path.Item2, hiddenCoords))
                || entry.Value is not IEnumerable<TextureRect> pathSprites)
            {
                continue;
            }

            foreach (TextureRect pathSprite in pathSprites)
            {
                pathSprite.Hide();
            }
        }
    }

    private static bool IsSpecialCoord(MapCoord coord, IReadOnlyList<MapCoord> hiddenCoords)
    {
        return hiddenCoords.Any(hiddenCoord => hiddenCoord == coord);
    }
}
