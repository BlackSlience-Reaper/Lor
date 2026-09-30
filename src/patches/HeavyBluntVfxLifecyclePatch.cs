using System;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using LibraryOfRuina.infra.lifecycle;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;
using LibraryOfRuina.features.ftue;
using MegaCrit.Sts2.Core.Combat;
using LibraryOfRuina.infra.patching;

namespace LibraryOfRuina.patches;

// 粒子可能在异步等待期间失效，每次播放时检查，退出场景时结束播放并释放特效。
// 只在本模组遭遇里替换原版播放流程（例如解放战换阶段场景时）；其他战斗保持原版。
[HarmonyPatch(typeof(NHeavyBluntVfx), nameof(NHeavyBluntVfx._Ready))]
[LibraryPatch(Reason = "原版 _Ready 只启动私有的 PlaySequence，它在异步等待之后直接 Restart 冲击粒子、释放节点，不检查节点是否已失效（例如解放战换阶段换场景时）而抛异常；特效节点由原版场景实例化，无法换成子类覆写 _Ready。只在本模组遭遇中改用带有效性检查的播放流程。")]
internal static class HeavyBluntVfxLifecyclePatch
{
    [HarmonyPrefix]
    private static bool Prefix(
        NHeavyBluntVfx __instance,
        Vector2 ____debugPosition,
        Godot.Collections.Array<GpuParticles2D> ____anticipationParticles,
        Godot.Collections.Array<GpuParticles2D> ____impactParticles)
    {
        if (!FtueGuard.IsLibraryOfRuinaEncounter(CurrentCombat.State?.Encounter))
        {
            return true;
        }

        TaskHelper.RunSafely(PlaySequence(
            __instance,
            ____debugPosition,
            ____anticipationParticles,
            ____impactParticles));
        return false;
    }

    // 重击蓄力与余波沿用原版播放时长，单位为秒。
    private const float AnticipationSeconds = 0.2f;

    // 重击命中后的粒子保留时长，单位为秒。
    private const float ImpactSeconds = 2f;

    private static async Task PlaySequence(
        NHeavyBluntVfx vfx,
        Vector2 position,
        Godot.Collections.Array<GpuParticles2D>? anticipation,
        Godot.Collections.Array<GpuParticles2D>? impact)
    {
        try
        {
            vfx.GlobalPosition = position;
            RestartParticles(anticipation);
            await WaitForSeconds(vfx, AnticipationSeconds);
            NGame.Instance?.ScreenShake(ShakeStrength.Strong, ShakeDuration.Short);
            RestartParticles(impact);
            await WaitForSeconds(vfx, ImpactSeconds);
        }
        catch (OperationCanceledException)
        {
            // 战斗场景退出会取消逐帧等待。
        }
        finally
        {
            if (GodotObject.IsInstanceValid(vfx) && !vfx.IsQueuedForDeletion())
            {
                vfx.QueueFree();
            }
        }
    }

    private static async Task WaitForSeconds(NHeavyBluntVfx vfx, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += await vfx.AwaitProcessFrame();
        }
    }

    private static void RestartParticles(Godot.Collections.Array<GpuParticles2D>? particles)
    {
        if (particles == null)
        {
            return;
        }

        foreach (GpuParticles2D particle in particles)
        {
            if (GodotObject.IsInstanceValid(particle) && !particle.IsQueuedForDeletion())
            {
                particle.Restart();
            }
        }
    }
}
