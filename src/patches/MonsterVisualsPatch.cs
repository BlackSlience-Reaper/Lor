using System;
using HarmonyLib;
using LibraryOfRuina.infra.patching;
using LibraryOfRuina.patches.visuals;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;

// ReSharper disable UnusedType.Global

namespace LibraryOfRuina.patches;

[HarmonyPatch(typeof(MonsterModel), nameof(MonsterModel.CreateVisuals))]
[LibraryPatch(Reason = "原版 CreateVisuals 非虚，只会实例化 VisualsPath（可覆写）指向的场景；本模组外观多为运行时用代码拼装的精灵节点，没有对应场景可指，换路径做不到。只作用于 MonsterVisualCatalog 登记的本模组怪物 id。")]
public static class MonsterModelCreateVisualsPatch
{
    private static bool Prefix(MonsterModel __instance, ref NCreatureVisuals __result)
    {
        if (!WrappedMonsterVisualFactory.ShouldWrap(__instance))
            return true;

        try
        {
            __result = WrappedMonsterVisualFactory.Create(__instance);
            return false;
        }
        catch (Exception ex)
        {
            MonsterVisualDebug.Write($"CreateVisuals FAILED id={__instance.Id.Entry}: {ex}");
            Log.Error($"[LibraryOfRuina] CreateVisuals failed for {__instance.Id.Entry}: {ex}");
            throw;
        }
    }
}
