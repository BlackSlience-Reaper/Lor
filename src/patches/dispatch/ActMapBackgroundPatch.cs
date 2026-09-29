using System.Reflection;
using HarmonyLib;
using LibraryOfRuina.content.acts;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.patches.dispatch;

/// <summary>
/// 本模组各幕的地图背景。ActLikeIt2 的前缀先把模板幕路径转发过来，这里在后缀里只替换本模组幕的路径，
/// 保持预加载与显示路径一致。原来 8 个楼层与残响乐团各有一个后缀，按幕类型互斥，合并后结果不变。
/// </summary>
[HarmonyPatch]
internal static class ActMapBackgroundPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.PropertyGetter(typeof(ActModel), nameof(ActModel.MapTopBgPath));
        yield return AccessTools.PropertyGetter(typeof(ActModel), nameof(ActModel.MapMidBgPath));
        yield return AccessTools.PropertyGetter(typeof(ActModel), nameof(ActModel.MapBotBgPath));
    }

    [HarmonyPostfix]
    private static void Postfix(ActModel __instance, MethodBase __originalMethod, ref string __result)
    {
        switch (__instance)
        {
            case LibraryOfRuinaActModel act:
                __result = __originalMethod.Name switch
                {
                    "get_MapTopBgPath" => act.MapTopBackground,
                    "get_MapMidBgPath" => act.MapMiddleBackground,
                    "get_MapBotBgPath" => act.MapBottomBackground,
                    _ => __result
                };
                break;
            case ReverberationEnsembleAct:
                __result = ReverberationEnsembleAct.MapAssetRoot + (__originalMethod.Name switch
                {
                    "get_MapTopBgPath" => "map_top_reverberation_ensemble.png",
                    "get_MapMidBgPath" => "map_middle_reverberation_ensemble.png",
                    _ => "map_bottom_reverberation_ensemble.png"
                });
                break;
        }
    }
}
