using System.Reflection;
using HarmonyLib;
using LibraryOfRuina.acts;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.patches;

[HarmonyPatch]
internal static class TechnologyFloorMapBackgroundPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.PropertyGetter(typeof(ActModel), nameof(ActModel.MapTopBgPath));
        yield return AccessTools.PropertyGetter(typeof(ActModel), nameof(ActModel.MapMidBgPath));
        yield return AccessTools.PropertyGetter(typeof(ActModel), nameof(ActModel.MapBotBgPath));
    }

    // 在模板路径转发完成后，仅替换科技层资源，保持预加载和显示路径一致。
    [HarmonyPostfix]
    private static void Postfix(
        ActModel __instance,
        MethodBase __originalMethod,
        ref string __result)
    {
        if (__instance is not Yesod)
        {
            return;
        }

        __result = __originalMethod.Name switch
        {
            "get_MapTopBgPath" => Yesod.MapTopBackgroundPath,
            "get_MapMidBgPath" => Yesod.MapMiddleBackgroundPath,
            "get_MapBotBgPath" => Yesod.MapBottomBackgroundPath,
            _ => __result
        };
    }
}
