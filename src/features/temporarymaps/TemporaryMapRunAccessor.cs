using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace LibraryOfRuina.features.temporarymaps;

internal static class TemporaryMapRunAccessor
{
    private static readonly MethodInfo? RunManagerClearScreensMethod =
        AccessTools.Method(typeof(RunManager), "ClearScreens");

    private static readonly MethodInfo? RunManagerFadeInMethod =
        AccessTools.Method(typeof(RunManager), "FadeIn", [typeof(bool)]);

    private static readonly MethodInfo? RunManagerExitCurrentRoomsMethod =
        AccessTools.Method(typeof(RunManager), "ExitCurrentRooms");

    private static readonly MethodInfo? RunManagerEnterRoomInternalMethod =
        AccessTools.Method(typeof(RunManager), "EnterRoomInternal", [typeof(AbstractRoom), typeof(bool)]);

    private static readonly FieldInfo? ActRoomsField =
        AccessTools.Field(typeof(ActModel), "_rooms");

    private static readonly FieldInfo? MapPointHistoryField =
        AccessTools.Field(typeof(RunState), "_mapPointHistory");

    public static void ClearScreens(RunManager runManager)
    {
        RunManagerClearScreensMethod?.Invoke(runManager, null);
    }

    public static async Task FadeIn(RunManager runManager, bool showTransition)
    {
        if (RunManagerFadeInMethod?.Invoke(runManager, [showTransition]) is Task task)
        {
            await task;
        }
    }

    public static async Task ExitCurrentRooms(RunManager runManager)
    {
        if (RunManagerExitCurrentRoomsMethod?.Invoke(runManager, null) is Task task)
        {
            await task;
        }
    }

    public static async Task EnterRoomInternal(RunManager runManager, AbstractRoom room)
    {
        if (RunManagerEnterRoomInternalMethod?.Invoke(runManager, [room, false]) is Task task)
        {
            await task;
        }
        else
        {
            await runManager.EnterRoom(room);
        }
    }

    public static void RestoreActRooms(RunState state, SerializableActModel originalActSave)
    {
        if (ActRoomsField == null)
        {
            Log.Warn("[TemporaryMap] Could not restore act room set.");
            return;
        }

        ActRoomsField.SetValue(state.Act, RoomSet.FromSave(originalActSave.SerializableRooms));
    }

    public static bool TryGetMapPointHistory(RunState state, out List<List<MapPointHistoryEntry>> mapPointHistory)
    {
        if (MapPointHistoryField?.GetValue(state) is List<List<MapPointHistoryEntry>> concreteHistory)
        {
            mapPointHistory = concreteHistory;
            return true;
        }

        mapPointHistory = [];
        return false;
    }
}
