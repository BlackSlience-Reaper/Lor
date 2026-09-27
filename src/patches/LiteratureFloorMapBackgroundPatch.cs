using System.Reflection;
using HarmonyLib;
using LibraryOfRuina.acts;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.patches;

[HarmonyPatch]
internal static class LiteratureFloorMapBackgroundPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.PropertyGetter(typeof(ActModel), nameof(ActModel.MapTopBgPath));
        yield return AccessTools.PropertyGetter(typeof(ActModel), nameof(ActModel.MapMidBgPath));
        yield return AccessTools.PropertyGetter(typeof(ActModel), nameof(ActModel.MapBotBgPath));
    }

    // 在模板路径转发完成后，仅替换文学层资源，保持预加载和显示路径一致。
    [HarmonyPostfix]
    private static void Postfix(
        ActModel __instance,
        MethodBase __originalMethod,
        ref string __result)
    {
        if (__instance is not Hod)
        {
            return;
        }

        __result = __originalMethod.Name switch
        {
            "get_MapTopBgPath" => Hod.MapTopBackgroundPath,
            "get_MapMidBgPath" => Hod.MapMiddleBackgroundPath,
            "get_MapBotBgPath" => Hod.MapBottomBackgroundPath,
            _ => __result
        };
    }
}
