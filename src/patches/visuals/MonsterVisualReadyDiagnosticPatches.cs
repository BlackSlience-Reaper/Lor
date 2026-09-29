using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.patches;

[HarmonyPatch(typeof(NCreatureVisuals), nameof(NCreatureVisuals._Ready))]
internal static class MonsterVisualsReadyDebugPatch
{
    private static Exception? Finalizer(NCreatureVisuals __instance, Exception? __exception)
    {
        if (__exception == null) return null;
        MonsterModel? monster = (__instance.GetParent() as NCreature)?.Entity?.Monster;
        if (monster != null && WrappedMonsterVisualFactory.ShouldWrap(monster))
        {
            MonsterVisualDebug.Write($"NCreatureVisuals._Ready exception id={monster.Id.Entry}: {__exception}");
        }
        return __exception;
    }
}

[HarmonyPatch(typeof(NCreature), nameof(NCreature._Ready))]
internal static class MonsterNodeReadyDebugPatch
{
    private static Exception? Finalizer(NCreature __instance, Exception? __exception)
    {
        if (__exception == null) return null;
        MonsterModel? monster = __instance.Entity?.Monster;
        if (monster != null && WrappedMonsterVisualFactory.ShouldWrap(monster))
        {
            MonsterVisualDebug.Write($"NCreature._Ready exception id={monster.Id.Entry}: {__exception}");
        }
        return __exception;
    }
}
