using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using LibraryOfRuina.features.settings;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using LibraryOfRuina.helpers;

namespace LibraryOfRuina.patches;

[HarmonyPatch(typeof(EventRelicPool), "GenerateAllRelics")]
public static class LibraryOfRuinaEventRelicPoolPatch
{
    private static readonly Type[] RelicTypes = LibraryAssemblyTypes.All
        .Where(type => !type.IsAbstract && typeof(RelicModel).IsAssignableFrom(type))
        .ToArray();

    [HarmonyPostfix]
    public static void Postfix(ref IEnumerable<RelicModel> __result)
    {
        if (!LibraryOfRuinaSettings.MonsterExtensionEnabled)
        {
            return;
        }

        __result = __result
            .Concat(RelicTypes
                .Where(ModelDb.Contains)
                .Select(type => ModelDb.GetById<RelicModel>(ModelDb.GetId(type))))
            .DistinctBy(relic => relic.Id);
    }
}
