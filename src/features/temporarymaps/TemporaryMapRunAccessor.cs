using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;
using MegaCrit.Sts2.Core.Saves.Runs;
using LibraryOfRuina.interop;

namespace LibraryOfRuina.features.temporarymaps;

internal static class TemporaryMapRunAccessor
{





    public static void ClearScreens(RunManager runManager)
    {
        VanillaPrivate.RunManagerClearScreens.Invoke(runManager);
    }

    public static Task FadeIn(RunManager runManager, bool showTransition) => RunManagerCompat.FadeIn(runManager, showTransition);

    public static async Task ExitCurrentRooms(RunManager runManager)
    {
        if (VanillaPrivate.RunManagerExitCurrentRooms.Invoke(runManager) is Task task)
        {
            await task;
        }
    }

    public static async Task EnterRoomInternal(RunManager runManager, AbstractRoom room)
    {
        if (VanillaPrivate.RunManagerEnterRoomInternal.Invoke(runManager, [room, false]) is Task task)
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
        if (!VanillaPrivate.ActModelRooms.IsAvailable)
        {
            Log.Warn("[TemporaryMap] Could not restore act room set.");
            return;
        }

        VanillaPrivate.ActModelRooms.Set(state.Act, RoomSet.FromSave(originalActSave.SerializableRooms));
    }

    public static bool TryGetMapPointHistory(RunState state, out List<List<MapPointHistoryEntry>> mapPointHistory)
    {
        if (VanillaPrivate.RunStateMapPointHistory.Get(state) is List<List<MapPointHistoryEntry>> concreteHistory)
        {
            mapPointHistory = concreteHistory;
            return true;
        }

        mapPointHistory = [];
        return false;
    }
}
