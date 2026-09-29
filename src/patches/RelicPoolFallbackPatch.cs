using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using LibraryOfRuina.core;

namespace LibraryOfRuina.patches;

[HarmonyPatch(typeof(RelicModel), nameof(RelicModel.Pool), MethodType.Getter)]
public static class RelicPoolFallbackPatch
{
    [HarmonyFinalizer]
    public static Exception? Finalizer(
        RelicModel __instance,
        ref RelicPoolModel __result,
        Exception? __exception)
    {
        if (__exception == null)
        {
            return null;
        }

        if (__exception is not InvalidOperationException || !IsLibraryOfRuinaRelic(__instance))
        {
            return __exception;
        }

        __result = ModelDb.RelicPool<EventRelicPool>();
        return null;
    }

    private static bool IsLibraryOfRuinaRelic(RelicModel relic) =>
        relic.GetType().Assembly == typeof(LibraryOfRuinaInitializer).Assembly;
}
