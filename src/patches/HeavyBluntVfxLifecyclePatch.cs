using System;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;

namespace LibraryOfRuina.patches;

// 粒子可能在异步等待期间失效，每次播放时检查，退出场景时结束播放并释放特效。
[HarmonyPatch(typeof(NHeavyBluntVfx), nameof(NHeavyBluntVfx._Ready))]
internal static class HeavyBluntVfxLifecyclePatch
{
    [HarmonyPrefix]
    private static bool Prefix(
        NHeavyBluntVfx __instance,
        Vector2 ____debugPosition,
        Godot.Collections.Array<GpuParticles2D> ____anticipationParticles,
        Godot.Collections.Array<GpuParticles2D> ____impactParticles)
    {
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
