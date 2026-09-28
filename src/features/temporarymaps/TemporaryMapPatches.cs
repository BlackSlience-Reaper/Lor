using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;
using LibraryOfRuina.infra.patching;

namespace LibraryOfRuina.features.temporarymaps;

[HarmonyPatch(typeof(RunManager), "ToSave")]
internal static class TemporaryMapSavePatch
{
    [HarmonyPostfix]
    private static void Postfix(SerializableRun __result)
    {
        RunState? state = RunManager.Instance.DebugOnlyGetState();
        if (state == null || !TemporaryMapSessionManager.TryGetSession(state, out TemporaryMapSession? session))
        {
            TemporaryMapSaveStateStore.Clear();
            return;
        }

        if (state.CurrentActIndex >= 0 && state.CurrentActIndex < __result.Acts.Count)
        {
            __result.Acts[state.CurrentActIndex].SavedMap = SerializableActMap.FromActMap(state.Map);
        }

        __result.VisitedMapCoords = [.. state.VisitedMapCoords];
        __result.MapPointHistory = state.MapPointHistory
            .Select(static history => history.ToList())
            .ToList();
        __result.MapDrawings = null;

        TemporaryMapSaveStateStore.Save(__result, state, session);
    }
}

[HarmonyPatch(typeof(RunState), nameof(RunState.FromSerializable))]
internal static class TemporaryMapLoadPatch
{
    [HarmonyPostfix]
    private static void Postfix(SerializableRun save, RunState __result)
    {
        TemporaryMapController.QueueRestoreFromSave(save, __result);
    }
}

[HarmonyPatch(typeof(NMapScreen), nameof(NMapScreen.Open))]
internal static class TemporaryMapMapOpenPatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        RunState? state = RunManager.Instance.DebugOnlyGetState();
        if (state?.Map != null)
        {
            TemporaryMapController.TryRestoreSavedSessionForCurrentRun(state.Map);
        }

        TemporaryMapController.TryRestoreCompletedCurrentRun();
    }
}

[HarmonyPatch(typeof(NMapScreen), nameof(NMapScreen.SetMap))]
internal static class TemporaryMapMapScreenPatch
{
    [HarmonyPostfix]
    private static void Postfix(NMapScreen __instance, ActMap map)
    {
        TemporaryMapController.TryRestoreSavedSessionForCurrentRun(map);
        if (!TemporaryMapController.TryRestoreCompletedCurrentRun())
        {
            TemporaryMapSessionManager.HideSpecialNodesAndPaths(__instance, map);
        }
    }
}

[HarmonyPatch(typeof(RunManager), "CreateRoom")]
[LibraryPatch(Reason = "RunManager.CreateRoom 私有非虚且无建房 Hook，后缀会先让原版消耗遭遇/事件队列；仅在本模组临时地图会话激活且当前坐标属于临时地图节点时接管。")]
internal static class TemporaryMapCreateRoomPatch
{
    [HarmonyPrefix]
    private static bool Prefix(ref AbstractRoom __result)
    {
        return TemporaryMapController.TryCreateRoomForCurrentNode(ref __result);
    }
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.ProceedFromTerminalRewardsScreen))]
internal static class TemporaryMapTerminalRewardsProceedPatch
{
    [HarmonyPostfix]
    private static void Postfix(ref Task __result)
    {
        __result = RestoreTemporaryMapAfterProceed(__result);
    }

    private static async Task RestoreTemporaryMapAfterProceed(Task proceedTask)
    {
        await proceedTask;
        TemporaryMapController.TryRestoreCompletedCurrentRun();
    }
}
