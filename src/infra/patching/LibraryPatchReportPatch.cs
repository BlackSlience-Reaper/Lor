using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace LibraryOfRuina.infra.patching;

/// <summary>
/// 主菜单第一次就绪时全部模组都已加载完，此时才能看到其他模组装在同一目标上的补丁。只执行一次。
/// </summary>
[HarmonyPatch(typeof(NMainMenu), nameof(NMainMenu._Ready))]
internal static class LibraryPatchReportPatch
{
    private static bool _reported;

    private static void Postfix()
    {
        if (_reported)
        {
            return;
        }

        _reported = true;
        try
        {
            LibraryPatcher.ReportAfterAllModsLoaded(LibraryPatcher.HarmonyId);
        }
        catch (Exception exception)
        {
            // 报告只做诊断，不能影响主菜单。
            Log.Warn("[LibraryOfRuina.Patching] Patch report failed: " + exception);
        }
    }
}
