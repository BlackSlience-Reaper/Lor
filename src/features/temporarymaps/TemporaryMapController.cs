using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.core.networking;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.TestSupport;

namespace LibraryOfRuina.features.temporarymaps;

public static class TemporaryMapController
{
    private static bool _initialized;

    public static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        TemporaryMapRegistry.InitializeDefaults();
        RunManager.Instance.RunStarted += TemporaryMapSessionManager.OnRunStarted;
        RunManager.Instance.RoomEntered += TemporaryMapSessionManager.OnRoomEntered;
        _initialized = true;
    }

    public static bool IsActive(IRunState? runState)
    {
        return TemporaryMapSessionManager.IsActive(runState);
    }

    public static Task EnterFromEvent(Player owner, string definitionId)
    {
        if (!TemporaryMapRegistry.TryGetDefinition(definitionId, out _))
        {
            Log.Warn("[TemporaryMap] Ignored event entry for unknown definition '" + definitionId + "'.");
            return Task.CompletedTask;
        }

        if (!LibraryNetwork.IsLocalOwner(owner))
        {
            return Task.CompletedTask;
        }

        TemporaryMapAction.EnqueueEnter(owner, definitionId);
        return Task.CompletedTask;
    }

    public static Task EnterFromDebugCommand(Player owner, string definitionId)
    {
        if (!TemporaryMapRegistry.TryGetDefinition(definitionId, out _))
        {
            Log.Warn("[TemporaryMap] Ignored debug entry for unknown definition '" + definitionId + "'.");
            return Task.CompletedTask;
        }

        if (!LibraryNetwork.IsLocalOwner(owner))
        {
            return Task.CompletedTask;
        }

        TemporaryMapAction.EnqueueEnter(owner, definitionId);
        return Task.CompletedTask;
    }

    internal static Task EnterFromSyncedAction(Player owner, string definitionId)
    {
        if (!TemporaryMapRegistry.TryGetDefinition(definitionId, out TemporaryMapDefinition? definition))
        {
            Log.Warn("[TemporaryMap] Ignored synced entry for unknown definition '" + definitionId + "'.");
            return Task.CompletedTask;
        }

        return EnterDeferred(owner, definition);
    }

    internal static bool TryCreateRoomForCurrentNode(ref AbstractRoom result)
    {
        RunState? state = RunManager.Instance.DebugOnlyGetState();
        if (state == null
            || !state.CurrentMapCoord.HasValue
            || !TemporaryMapSessionManager.TryGetSession(state, out TemporaryMapSession? session)
            || !session.TemporaryMap.TryGetNode(state.CurrentMapCoord.Value, out TemporaryMapNodeDefinition? node))
        {
            return true;
        }

        result = node.CreateRoom(state);
        return false;
    }

    internal static void QueueRestoreFromSave(SerializableRun save, RunState state)
    {
        TemporaryMapSessionManager.QueueRestoreFromSave(save, state);
    }

    internal static bool TryRestoreSavedSessionForCurrentRun(ActMap map)
    {
        return TemporaryMapSessionManager.TryRestoreSavedSessionForCurrentRun(map);
    }

    internal static bool TryRestoreCompletedCurrentRun()
    {
        return TemporaryMapSessionManager.TryRestoreCompletedCurrentRun();
    }

    private static async Task EnterDeferred(Player owner, TemporaryMapDefinition definition)
    {
        RunManager runManager = RunManager.Instance;
        if (owner.RunState is not RunState state)
        {
            Log.Warn("[TemporaryMap] Tried to enter a temporary map without a run state.");
            return;
        }

        if (TemporaryMapSessionManager.IsActive(state))
        {
            Log.Warn("[TemporaryMap] Tried to enter a temporary map while one is already active.");
            return;
        }

        if (!TemporaryMapSessionManager.AddPendingEntry(state))
        {
            Log.Warn("[TemporaryMap] Ignored a duplicate temporary-map entry request.");
            return;
        }

        try
        {
            await TemporaryMapSessionManager.AwaitNextProcessFrame();
            if (!ReferenceEquals(runManager.DebugOnlyGetState(), state))
            {
                Log.Warn("[TemporaryMap] Temporary-map entry was cancelled because the active run changed.");
                return;
            }

            if (TestMode.IsOff && NGame.Instance != null)
            {
                await NGame.Instance.Transition.RoomFadeOut();
            }

            SerializableActModel originalActSave = state.Act.ToSave();
            MapCoord? entryMapCoord = state.CurrentMapCoord;
            await TemporaryMapRunAccessor.ExitCurrentRooms(runManager);
            TemporaryMapRunAccessor.ClearScreens(runManager);

            TemporaryMapActMap temporaryMap = TemporaryMapActMap.Create(definition);
            TemporaryMapSessionManager.SetSession(
                state,
                new TemporaryMapSession(
                    definition,
                    state.Map,
                    state.VisitedMapCoords.ToList(),
                    state.MapPointHistory.Select(static history => (IReadOnlyList<MapPointHistoryEntry>)history.ToList()).ToList(),
                    state.ActFloor,
                    originalActSave,
                    entryMapCoord,
                    temporaryMap));

            state.Map = temporaryMap;
            state.ClearVisitedMapCoordsDebug();
            state.AddVisitedMapCoord(temporaryMap.StartingMapPoint.coord);
            TemporaryMapSessionManager.RefreshLocationSynchronizers(state);
            TemporaryMapSessionManager.SetMapScreen(temporaryMap, state, initMarker: false);

            Log.Info("[TemporaryMap] Entering temporary map '" + definition.DisplayName + "'.");
            await TemporaryMapRunAccessor.EnterRoomInternal(runManager, new MapRoom());
            await TemporaryMapRunAccessor.FadeIn(runManager, showTransition: true);
        }
        finally
        {
            TemporaryMapSessionManager.RemovePendingEntry(state);
        }
    }
}
