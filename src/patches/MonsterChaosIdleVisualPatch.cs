using HarmonyLib;
using LibraryLib.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.patches;

internal interface IChaosIdleVisuals
{
    void RefreshChaosIdle();
}

internal static class MonsterChaosIdleVisualPatch
{
    internal static bool ShouldHoldHitPose(NCreatureVisuals visuals) =>
        visuals.GetParent() is NCreature
        {
            Entity: LibraryCreature { IsChaoed: true, IsAlive: true }
        };

    // 状态命令立即更新画面，逐帧同步同时覆盖读档、换场景和延迟创建视觉。
    internal static void Refresh(LibraryCreature creature)
    {
        if (creature.GetCreatureNode()?.Visuals is IChaosIdleVisuals visuals)
        {
            visuals.RefreshChaosIdle();
        }
    }
}

[HarmonyPatch(typeof(LibraryCreature), nameof(LibraryCreature.SaveAndSetStunResistance))]
internal static class MonsterChaosEnterVisualPatch
{
    private static void Postfix(LibraryCreature __instance)
    {
        MonsterChaosIdleVisualPatch.Refresh(__instance);
    }
}

[HarmonyPatch(typeof(LibraryCreature), nameof(LibraryCreature.RestorePreStunResistance))]
internal static class MonsterChaosRecoveryVisualPatch
{
    private static void Postfix(LibraryCreature __instance)
    {
        MonsterChaosIdleVisualPatch.Refresh(__instance);
    }
}
