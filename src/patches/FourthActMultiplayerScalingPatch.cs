using HarmonyLib;
using LibraryOfRuina.acts;
using LibraryOfRuina.helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Singleton;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.patches;

[HarmonyPatch(typeof(MultiplayerScalingModel), nameof(MultiplayerScalingModel.GetMultiplayerScaling))]
internal static class FourthActMultiplayerScalingPatch
{
    // 第四幕普通及精英遭遇的多人缩放系数，实际倍率还需乘以人数。
    private const decimal FourthActEncounterScaling = 1.4m;

    // 第四幕 Boss 遭遇的多人缩放系数，实际倍率还需乘以人数。
    private const decimal FourthActBossScaling = 1.5m;

    // actIndex alone cannot tell this act apart from another mod's fourth act, so the current
    // run's act is checked as well; any other fourth act keeps its own (or vanilla) scaling.
    [HarmonyPrefix]
    [HarmonyPriority(Priority.Low)]
    private static bool Prefix(EncounterModel? encounter, int actIndex, ref decimal __result)
    {
        if (actIndex != ReverberationEnsembleAct.ActNumber - 1
            || (RunManager.Instance.DebugOnlyGetState()?.Act is not ReverberationEnsembleAct
                && !ModOwnership.IsOwn(encounter)))
        {
            return true;
        }

        __result = encounter?.RoomType == RoomType.Boss
            ? FourthActBossScaling
            : FourthActEncounterScaling;
        return false;
    }
}
