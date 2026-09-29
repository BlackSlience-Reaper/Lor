using System;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.framework.audio;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;
using MegaCrit.Sts2.Core.TestSupport;
using static LibraryOfRuina.framework.visuals.common.VfxPrimitives;

namespace LibraryOfRuina.visuals.PhilosophyFloorLiberation;

// TornMouth、Punishment 的每一击：血色斩痕与爆点，按击数上下错开、交替斜角。
internal static partial class PhilosophyFloorLiberationVfx
{
    internal static Task PlayBloodStrikeAsync(
        Creature attacker,
        Creature target,
        int hitIndex) =>
        PlayBloodStrikeAsync(
            attacker,
            target,
            hitIndex,
            static () => Task.CompletedTask);

    internal static async Task PlayBloodStrikeAsync(
        Creature attacker,
        Creature target,
        int hitIndex,
        Func<Task> onImpact)
    {
        ArgumentNullException.ThrowIfNull(onImpact);
        AttackVfxSnapshot snapshot = Capture(attacker, [target]);
        LocalOggOneShotPlayer.Play(PunishmentSfxPath, -2f);
        if (TestMode.IsOn
            || !TryCreateRoot(
                "PhilosophyTwilightBloodStrike",
                snapshot,
                out Node2D root))
        {
            await onImpact();
            return;
        }

        Texture2D? burstTexture = LoadTexture(MeleeBurstPath);
        Texture2D? waveTexture = LoadTexture(MeleeWavePath);
        if (burstTexture == null || waveTexture == null)
        {
            root.QueueFree();
            await onImpact();
            return;
        }

        var additive = new CanvasItemMaterial
        {
            BlendMode = CanvasItemMaterial.BlendModeEnum.Add
        };
        Vector2 impact = ResolveImpactPoint(snapshot, root, 500f)
            + new Vector2(0f, (hitIndex % 3 - 1) * 28f);
        float slashRotation = snapshot.Direction
            * ((hitIndex & 1) == 0 ? -0.24f : 0.22f);
        Sprite2D slash = CreateSprite(
            "SmallBeakBloodSlash",
            waveTexture,
            impact,
            new Vector2(0.05f, 0.30f),
            new Color(1f, 0.03f, 0.01f, 0.94f),
            additive,
            9);
        Sprite2D burst = CreateSprite(
            "SmallBeakBloodBurst",
            burstTexture,
            impact,
            new Vector2(0.20f, 0.20f),
            new Color(1f, 0.08f, 0.02f, 0.86f),
            additive,
            10);
        slash.Rotation = slashRotation;
        slash.FlipH = snapshot.Direction > 0f;
        burst.FlipH = snapshot.Direction > 0f;
        root.AddChildSafely(slash);
        root.AddChildSafely(burst);

        Tween strike = root.CreateTween();
        strike.SetParallel();
        strike.SetTrans(Tween.TransitionType.Expo);
        strike.SetEase(Tween.EaseType.Out);
        strike.TweenProperty(slash, "scale", new Vector2(1.08f, 0.82f), 0.24f);
        strike.TweenProperty(slash, "modulate:a", 0f, 0.34f);
        strike.TweenProperty(burst, "scale", new Vector2(1.34f, 1.34f), 0.28f);
        strike.TweenProperty(burst, "modulate:a", 0f, 0.34f);
        NGame.Instance?.ScreenShake(
            ShakeStrength.Weak,
            ShakeDuration.Short,
            snapshot.Direction < 0f ? 180f : 0f);
        GpuParticles2D bloodParticles = CreateBurstParticles(
            root,
            "SmallBeakGpuBlood",
            burstTexture,
            impact,
            new Vector2(snapshot.Direction, -0.10f),
            new Color(0.94f, 0.015f, 0.005f, 0.84f),
            additive,
            11,
            amount: 16,
            lifetime: 0.32,
            speedMin: 90f,
            speedMax: 280f,
            scaleMin: 0.018f,
            scaleMax: 0.075f,
            spread: 70f,
            gravityY: 160f);
        try
        {
            await onImpact();
            await Task.WhenAll(
                Wait(root, 0.36),
                AwaitParticleTail(root, bloodParticles));
        }
        finally
        {
            QueueFree(root);
        }
    }
}
