using System.Reflection;
using HarmonyLib;
using LibraryOfRuina.acts;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.patches;

[HarmonyPatch]
internal static class HistoryFloorMapBackgroundPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.PropertyGetter(typeof(ActModel), nameof(ActModel.MapTopBgPath));
        yield return AccessTools.PropertyGetter(typeof(ActModel), nameof(ActModel.MapMidBgPath));
        yield return AccessTools.PropertyGetter(typeof(ActModel), nameof(ActModel.MapBotBgPath));
    }

    // TemplateActModel 的前置补丁先返回模板路径，后置补丁再替换历史层的三段地图。
    // 统一修改路径入口，使资源预加载与实际地图显示使用同一套贴图。
    [HarmonyPostfix]
    private static void Postfix(
        ActModel __instance,
        MethodBase __originalMethod,
        ref string __result)
    {
        if (__instance is not Malkuth)
        {
            return;
        }

        __result = __originalMethod.Name switch
        {
            "get_MapTopBgPath" => Malkuth.MapTopBackgroundPath,
            "get_MapMidBgPath" => Malkuth.MapMiddleBackgroundPath,
            "get_MapBotBgPath" => Malkuth.MapBottomBackgroundPath,
            _ => __result
        };
    }
}
