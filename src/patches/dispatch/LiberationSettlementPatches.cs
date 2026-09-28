using System.Threading.Tasks;
using HarmonyLib;
using LibraryOfRuina.events;
using LibraryOfRuina.patches.ArtFloorLiberation;
using LibraryOfRuina.patches.HistoryFloorLiberation;
using LibraryOfRuina.patches.LanguageFloorLiberation;
using LibraryOfRuina.patches.LiteratureFloorLiberation;
using LibraryOfRuina.patches.NaturalFloorLiberation;
using LibraryOfRuina.patches.TechnologyFloorLiberation;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.patches.dispatch;

/// <summary>
/// 楼层解放结算流程。原来六个楼层各在四个目标上挂一个跳过型前缀，按结算事件或解放遭遇的类型互斥；
/// 其中三类在六层完全相同，合并为按 <see cref="ILibrarySettlementEvent"/> 判断，终局奖励跳转保留各层自己的处理，
/// 按原来的安装顺序依次调用，遇到接管的一层即停止（与多个跳过型前缀时相同）。
/// 这些仍是跳过型前缀，是否改成后缀或自有覆写在阶段 3c 裁决。
/// </summary>
internal static class LiberationSettlementPatches
{
    /// <summary>结算事件不走先古开场回血。</summary>
    [HarmonyPatch(typeof(AncientEventModel), "BeforeEventStarted")]
    private static class SkipAncientHeal
    {
        private static bool Prefix(AncientEventModel __instance, ref Task __result)
        {
            if (__instance is not ILibrarySettlementEvent)
            {
                return true;
            }

            __result = Task.CompletedTask;
            return false;
        }
    }

    /// <summary>结算事件总是允许出现，不受先古出现规则限制。</summary>
    [HarmonyPatch(typeof(Hook), nameof(Hook.ShouldAllowAncient))]
    private static class AllowAncient
    {
        private static bool Prefix(AncientEventModel ancient, ref bool __result)
        {
            if (ancient is not ILibrarySettlementEvent)
            {
                return true;
            }

            __result = true;
            return false;
        }
    }

    /// <summary>结算事件结束后直接进入下一幕。</summary>
    [HarmonyPatch(typeof(NEventRoom), nameof(NEventRoom.Proceed))]
    private static class ProceedToNextAct
    {
        private static bool Prefix(ref Task __result)
        {
            if (RunManager.Instance.DebugOnlyGetState()?.CurrentRoom is not EventRoom { CanonicalEvent: ILibrarySettlementEvent })
            {
                return true;
            }

            __result = RunManager.Instance.EnterNextAct();
            return false;
        }
    }

    /// <summary>解放战斗胜利后，终局奖励界面继续时转入该层的结算事件。</summary>
    [HarmonyPatch(typeof(RunManager), nameof(RunManager.ProceedFromTerminalRewardsScreen))]
    private static class RedirectToSettlement
    {
        private static bool Prefix(RunManager __instance, ref Task __result) =>
            TechnologyFloorLiberationSettlementRedirect.TryRedirect(__instance, ref __result)
            && NaturalFloorSettlementRedirect.TryRedirect(__instance, ref __result)
            && LiteratureFloorLiberationSettlementRedirect.TryRedirect(__instance, ref __result)
            && LanguageFloorLiberationSettlementRedirect.TryRedirect(__instance, ref __result)
            && HistoryFloorLiberationSettlementRedirect.TryRedirect(__instance, ref __result)
            && ArtFloorLiberationSettlementRedirect.TryRedirect(__instance, ref __result);
    }
}
