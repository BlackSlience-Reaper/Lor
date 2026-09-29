using System.Threading.Tasks;
using HarmonyLib;
using LibraryOfRuina.content.liberation;
using LibraryOfRuina.content.liberation.Art;
using LibraryOfRuina.content.liberation.History;
using LibraryOfRuina.content.liberation.Language;
using LibraryOfRuina.content.liberation.Literature;
using LibraryOfRuina.content.liberation.Natural;
using LibraryOfRuina.content.liberation.Technology;
using LibraryOfRuina.events;
using LibraryOfRuina.infra.patching;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.patches.dispatch;

/// <summary>
/// 楼层解放结算流程。允许先古出现是后缀；进入下一幕与终局奖励跳转仍是跳过型前缀（原版无 Hook，理由见各自的
/// LibraryPatch 元数据），终局奖励跳转按原来的安装顺序依次调用各层处理，遇到接管的一层即停止。
/// </summary>
internal static class LiberationSettlementPatches
{
    /// <summary>
    /// 结算事件总是允许出现，不受先古出现规则限制。原版 Hook 体只是询问监听者（0.111 原版没有覆写者），
    /// 用最后执行的后缀覆盖结果即可，不必跳过原方法。开场回血改由各结算事件覆写 BeforeEventStarted 处理。
    /// </summary>
    [HarmonyPatch(typeof(Hook), nameof(Hook.ShouldAllowAncient))]
    [LibraryPatch(Reason = "ShouldAllowAncient 是全体监听者的与运算，监听者只能否决不能放行，事件本身不是监听者，选项生成方法是 sealed；只对本模组楼层解放结算事件放行，否则第三方先古限制会让结算只剩“继续”。")]
    private static class AllowAncient
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(AncientEventModel ancient, ref bool __result)
        {
            if (ancient is ILibrarySettlementEvent)
            {
                __result = true;
            }
        }
    }

    /// <summary>结算事件结束后直接进入下一幕。</summary>
    [HarmonyPatch(typeof(NEventRoom), nameof(NEventRoom.Proceed))]
    [LibraryPatch(Reason = "NEventRoom.Proceed 是被硬编码为事件结束回调的静态方法且无 Hook；仅当前房间为本模组楼层解放结算事件时改为进入下一幕。绕过了换幕投票，联机需实测。")]
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
    [LibraryPatch(Reason = "ProceedFromTerminalRewardsScreen 公开非虚无 Hook，原版体会立即打开地图（后缀无法撤销）；仅当前房间为本模组楼层解放遭遇且满足结算条件时转入结算事件。")]
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
