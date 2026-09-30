using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using LibraryOfRuina.infra.helpers;

namespace LibraryOfRuina.patches;

[HarmonyPatch(typeof(EventRelicPool), "GenerateAllRelics")]
public static class LibraryOfRuinaEventRelicPoolPatch
{
    private static readonly Type[] RelicTypes = LibraryAssemblyTypes.All
        .Where(type => !type.IsAbstract && typeof(RelicModel).IsAssignableFrom(type))
        .ToArray();

    // 原版 RelicPoolModel.AllRelics 只调用一次 GenerateAllRelics 并缓存到进程结束，这里不能按设置或本局取舍：
    // 按首次调用时的设置决定会让之后所有局（联机时各端首次调用的时机也不同）共用那一刻的结果。
    // 本局关闭图书馆内容时由 MonsterExtensionEventRelicPoolGatePatch 在 GetUnlockedRelics 里按本局过滤。
    [HarmonyPostfix]
    public static void Postfix(ref IEnumerable<RelicModel> __result)
    {
        __result = __result
            .Concat(RelicTypes
                .Where(ModelDb.Contains)
                .Select(type => ModelDb.GetById<RelicModel>(ModelDb.GetId(type))))
            .DistinctBy(relic => relic.Id);
    }
}
