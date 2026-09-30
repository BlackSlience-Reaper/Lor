using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using LibraryOfRuina.core;

namespace LibraryOfRuina.patches;

[HarmonyPatch(typeof(NCombatRoom), nameof(NCombatRoom.AddCreature))]
internal static class LibrarySpawnLayoutPatch
{
    [HarmonyPostfix]
    private static void Postfix(NCombatRoom __instance, Creature creature)
    {
        if (!__instance.IsNodeReady()
            || creature.Monster?.GetType().Assembly != typeof(LibraryOfRuinaInitializer).Assembly
            || creature.PetOwner != null
            || string.IsNullOrEmpty(creature.SlotName))
        {
            return;
        }

        // 原版只在进场和窗口缩放时适配阵容；召唤与换阶段会占用新的槽位。
        // 等本次节点布局完成后重算，让生物、命中框和状态栏随共同父节点一起调整。
        Callable.From(() =>
        {
            if (!GodotObject.IsInstanceValid(__instance)
                || !__instance.IsInsideTree()
                || __instance.IsQueuedForDeletion())
            {
                return;
            }

            __instance.Call(NCombatRoom.MethodName.AdjustCreatureScaleForAspectRatio);
        }).CallDeferred();
    }
}
