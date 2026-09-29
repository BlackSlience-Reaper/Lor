using System;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.framework.audio;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;
using MegaCrit.Sts2.Core.TestSupport;
using static LibraryOfRuina.framework.visuals.common.VfxPrimitives;

namespace LibraryOfRuina.content.liberation.Philosophy;

// PeaceForAll 的拍地：背景 EndBird 左臂下拍与这里的蓄力光、落地冲击波、粒子同时播放；
// 0.90 秒落地时触发伤害回调，左臂动画独立复位，全部结束后才释放根节点。
internal static partial class PhilosophyFloorLiberationVfx
{
    internal static Task PlayPeaceSlamAsync(
        Creature attacker,
        IReadOnlyList<Creature> targets) =>
        PlayPeaceSlamAsync(
            attacker,
            targets,
            static () => Task.CompletedTask);

    internal static async Task PlayPeaceSlamAsync(
        Creature attacker,
        IReadOnlyList<Creature> targets,
        Func<Task> onImpact)
    {
        ArgumentNullException.ThrowIfNull(onImpact);
        AttackVfxSnapshot snapshot = Capture(attacker, targets);
        Task armSlam =
            PhilosophyFloorLiberationBackgroundController.PlayLeftArmSlam(
                snapshot.Direction);
        if (TestMode.IsOn
            || !TryCreateRoot(
                "PhilosophyTwilightPeaceSlam",
                snapshot,
                out Node2D root))
        {
            await onImpact();
            await armSlam;
            return;
        }

        Texture2D? burstTexture = LoadTexture(MeleeBurstPath);
        Texture2D? waveTexture = LoadTexture(MeleeWavePath);
        if (burstTexture == null || waveTexture == null)
        {
            root.QueueFree();
            await onImpact();
            await armSlam;
            return;
        }

        var additive = new CanvasItemMaterial
        {
            BlendMode = CanvasItemMaterial.BlendModeEnum.Add
        };
        Vector2 impact = ResolveImpactPoint(snapshot, root, 490f);
        Sprite2D windup = CreateSprite(
            "PeaceSlamWindup",
            burstTexture,
            new Vector2(snapshot.Direction * 170f, -150f),
            new Vector2(0.32f, 0.32f),
            new Color(1f, 0.18f, 0.04f, 0f),
            additive,
            5);
        windup.FlipH = snapshot.Direction > 0f;
        root.AddChildSafely(windup);
        Tween windupTween = root.CreateTween();
        windupTween.SetParallel();
        windupTween.TweenProperty(windup, "modulate:a", 0.55f, 0.08f)
            .SetDelay(0.08f);
        windupTween.TweenProperty(
            windup,
            "scale",
            new Vector2(1.1f, 1.1f),
            0.18f)
            .SetDelay(0.18f)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.Out);
        windupTween.TweenProperty(windup, "modulate:a", 0.34f, 0.34f)
            .SetDelay(0.36f);
        windupTween.TweenProperty(windup, "modulate:a", 0f, 0.12f)
            .SetDelay(0.84f);
        // BlackForest's arm reaches the floor at 0.90 s. Damage is tied to
        // this explicit impact point while the arm independently restores.
        if (!await Wait(root, 0.90))
        {
            QueueFree(root);
            await onImpact();
            await armSlam;
            return;
        }

        LocalOggOneShotPlayer.Play(PeaceSlamSfxPath, -1f);
        NGame.Instance?.ScreenShake(
            ShakeStrength.Strong,
            ShakeDuration.Normal,
            snapshot.Direction < 0f ? 180f : 0f);
        Sprite2D impactBurst = CreateSprite(
            "PeaceSlamImpactBurst",
            burstTexture,
            impact + new Vector2(0f, -132f),
            Vector2.One * 0.35f,
            new Color(1f, 0.96f, 0.55f, 0f),
            additive,
            8);
        Sprite2D upperWave = CreateSprite(
            "PeaceSlamUpperWave",
            waveTexture,
            impact + new Vector2(0f, -18f),
            new Vector2(0.08f, 0.45f),
            new Color(1f, 1f, 1f, 0f),
            additive,
            5);
        Sprite2D lowerWave = CreateSprite(
            "PeaceSlamLowerWave",
            waveTexture,
            impact + new Vector2(0f, 46f),
            new Vector2(0.05f, 0.32f),
            new Color(1f, 1f, 1f, 0f),
            additive,
            4);
        Sprite2D handImpact = CreateSprite(
            "PeaceSlamHandImpact",
            waveTexture,
            impact + new Vector2(0f, -310f),
            new Vector2(0.16f, 0.28f),
            new Color(1f, 1f, 1f, 0f),
            additive,
            10);
        Sprite2D impactFlash = CreateSprite(
            "PeaceSlamImpactFlash",
            burstTexture,
            impact + new Vector2(0f, -128f),
            Vector2.Zero,
            new Color(1f, 0.95f, 0.30f, 0f),
            additive,
            9);
        impactBurst.FlipH = snapshot.Direction > 0f;
        upperWave.FlipH = snapshot.Direction > 0f;
        lowerWave.FlipH = snapshot.Direction > 0f;
        handImpact.FlipH = snapshot.Direction > 0f;
        impactFlash.FlipH = snapshot.Direction > 0f;
        root.AddChildSafely(impactBurst);
        root.AddChildSafely(upperWave);
        root.AddChildSafely(lowerWave);
        root.AddChildSafely(handImpact);
        root.AddChildSafely(impactFlash);

        Tween impactTween = root.CreateTween();
        impactTween.SetParallel();
        impactTween.TweenProperty(impactBurst, "modulate:a", 1f, 0.06f);
        impactTween.TweenProperty(
            impactBurst,
            "scale",
            Vector2.One * 1.55f,
            0.18f)
            .SetTrans(Tween.TransitionType.Expo)
            .SetEase(Tween.EaseType.Out);
        impactTween.TweenProperty(
            impactBurst,
            "scale",
            Vector2.One * 2.55f,
            0.42f)
            .SetDelay(0.16f)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.Out);
        impactTween.TweenProperty(impactBurst, "modulate:a", 0f, 0.36f)
            .SetDelay(0.24f);
        impactTween.TweenProperty(upperWave, "modulate:a", 0.94f, 0.05f)
            .SetDelay(0.02f);
        impactTween.TweenProperty(
            upperWave,
            "scale",
            new Vector2(1.2f, 0.86f),
            0.28f)
            .SetDelay(0.02f)
            .SetTrans(Tween.TransitionType.Expo)
            .SetEase(Tween.EaseType.Out);
        impactTween.TweenProperty(upperWave, "modulate:a", 0f, 0.28f)
            .SetDelay(0.20f);
        impactTween.TweenProperty(lowerWave, "modulate:a", 0.8f, 0.06f)
            .SetDelay(0.05f);
        impactTween.TweenProperty(
            lowerWave,
            "scale",
            new Vector2(1.05f, 0.62f),
            0.34f)
            .SetDelay(0.05f)
            .SetTrans(Tween.TransitionType.Expo)
            .SetEase(Tween.EaseType.Out);
        impactTween.TweenProperty(lowerWave, "modulate:a", 0f, 0.30f)
            .SetDelay(0.22f);
        impactTween.TweenProperty(handImpact, "modulate:a", 1f, 0.025f);
        impactTween.TweenProperty(
            handImpact,
            "scale",
            new Vector2(1.25f, 0.75f),
            0.09f)
            .SetTrans(Tween.TransitionType.Expo)
            .SetEase(Tween.EaseType.Out);
        impactTween.TweenProperty(
            handImpact,
            "scale",
            new Vector2(1.85f, 1.05f),
            0.24f)
            .SetDelay(0.07f)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.Out);
        impactTween.TweenProperty(handImpact, "modulate:a", 0f, 0.22f)
            .SetDelay(0.10f);
        impactTween.TweenProperty(impactFlash, "modulate:a", 1f, 0.025f);
        impactTween.TweenProperty(
            impactFlash,
            "scale",
            Vector2.One * 1.16f,
            0.07f)
            .SetTrans(Tween.TransitionType.Expo)
            .SetEase(Tween.EaseType.Out);
        impactTween.TweenProperty(
            impactFlash,
            "scale",
            Vector2.One * 1.95f,
            0.24f)
            .SetDelay(0.06f)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.Out);
        impactTween.TweenProperty(impactFlash, "modulate:a", 0f, 0.20f)
            .SetDelay(0.08f);
        GpuParticles2D peaceSparks = CreateBurstParticles(
            root,
            "PeaceSlamGpuSparks",
            burstTexture,
            impact + new Vector2(0f, -132f),
            new Vector2(snapshot.Direction, -0.42f),
            new Color(1f, 0.82f, 0.24f, 0.84f),
            additive,
            11,
            amount: 24,
            lifetime: 0.52,
            speedMin: 125f,
            speedMax: 410f,
            scaleMin: 0.025f,
            scaleMax: 0.10f,
            spread: 74f,
            gravityY: 220f);
        GpuParticles2D peaceDust = CreateBurstParticles(
            root,
            "PeaceSlamGpuDust",
            waveTexture,
            impact + new Vector2(0f, 26f),
            new Vector2(snapshot.Direction, -0.08f),
            new Color(1f, 0.46f, 0.12f, 0.52f),
            additive,
            6,
            amount: 14,
            lifetime: 0.60,
            speedMin: 55f,
            speedMax: 190f,
            scaleMin: 0.012f,
            scaleMax: 0.048f,
            spread: 88f,
            gravityY: 80f);
        Task impactFx = Task.WhenAll(
            armSlam,
            Wait(root, 0.75),
            AwaitParticleTail(root, peaceSparks),
            AwaitParticleTail(root, peaceDust));
        try
        {
            await onImpact();
            await impactFx;
        }
        finally
        {
            QueueFree(root);
        }
    }
}
