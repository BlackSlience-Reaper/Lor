using HarmonyLib;
using LibraryOfRuina.framework.visuals;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.patches.visuals;

// 原版只在怪物自带 Spine 动画时发 "Dead"，贴图外观上挂的 Spine 身体收不到，在这里补发。
// 只处理会移除节点的死亡：假死（银河之友等）不移除节点，保持站着，复活时也就不用把身体扶起来。
// 死亡流程已经在进行时原版会直接返回，这里同样不再重播。
[HarmonyPatch(typeof(NCreature), nameof(NCreature.StartDeathAnim))]
internal static class SpineSpriteDeathAnimPatch
{
    private static void Prefix(NCreature __instance, bool shouldRemove)
    {
        if (!shouldRemove || __instance.DeathAnimationTask is { IsCompleted: false })
        {
            return;
        }

        // 场景动画外观（文学层）挂了 Spine 身体时同样补发
        switch (__instance.Visuals)
        {
            case SpineSpriteAttackCreatureVisuals visuals:
                visuals.PlayDeath();
                break;
            case SceneAnimatedCreatureVisuals scene:
                scene.PlayDeath();
                break;
        }
    }
}
