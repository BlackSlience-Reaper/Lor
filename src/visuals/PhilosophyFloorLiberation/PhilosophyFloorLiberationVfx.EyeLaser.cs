using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.backgrounds.PhilosophyFloorLiberation;
using LibraryOfRuina.framework.audio;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;
using MegaCrit.Sts2.Core.TestSupport;
using static LibraryOfRuina.framework.visuals.common.VfxPrimitives;

namespace LibraryOfRuina.visuals.PhilosophyFloorLiberation;

// ForestLight、BrilliantEyes 的眼睛弹幕：背景 EndBird 的十六只眼先蓄力 1 秒，再各沿一条三次贝塞尔曲线
// （取自原作的十条路径模板）飞向计划目标；发射点与目标在飞行中逐帧重新解析。
internal static partial class PhilosophyFloorLiberationVfx
{
    // CreatureMap_ApocalypseBird.prefab contains five cubic path shapes under
    // each wing's EyePivots node. Their controls are retained for curvature;
    // the 16 launch anchors themselves come from BlackForest EndBird's live
    // EYES/eye1..eye16 scene positions.
    private const int EyeTrailSpriteCount = 12;
    private static readonly EyeCurveTemplate[] EyeCurveTemplates =
    [
        // Left wing: body(-0.051, 0.681) + wing(-1.448, 0.618) + Lp.
        new(new(-3.179f, 3.639f), new(-2.98f, 1.17f), new(-5.59f, -0.65f), new(-5.59f, -0.65f)),
        new(new(-3.449f, 5.179f), new(-3.00f, 1.22f), new(-6.68f, -0.17f), new(-6.74f, -0.23f)),
        new(new(-1.019f, 4.639f), new(-2.39f, 2.02f), new(-4.46f, 0.76f), new(-4.44f, 0.76f)),
        new(new(-1.819f, 4.979f), new(-3.35f, 2.03f), new(-5.96f, 0.02f), new(-5.96f, 0.06f)),
        new(new(-1.909f, 3.529f), new(-2.38f, 1.26f), new(-4.77f, -0.17f), new(-4.76f, -0.18f)),
        // Right wing: body(-0.051, 0.681) + wing(1.31, 0.532) + Rp.
        new(new(1.689f, 3.513f), new(2.71f, 1.83f), new(6.40f, 0.72f), new(6.42f, 0.71f)),
        new(new(2.959f, 3.643f), new(1.36f, 2.65f), new(4.43f, 1.56f), new(5.65f, -1.28f)),
        new(new(3.229f, 5.183f), new(2.24f, 1.27f), new(5.24f, 0.68f), new(5.26f, 0.68f)),
        new(new(1.589f, 4.983f), new(1.41f, 1.59f), new(4.31f, 0.34f), new(4.31f, 0.36f)),
        new(new(0.819f, 4.683f), new(0.61f, 1.62f), new(4.10f, 2.24f), new(4.22f, 2.24f))
    ];

    internal static Task PlayEyeLaserAsync(
        Creature attacker,
        IReadOnlyList<Creature> targets) =>
        PlayEyeLaserAsync(
            attacker,
            targets,
            static () => Task.CompletedTask);

    internal static async Task PlayEyeLaserAsync(
        Creature attacker,
        IReadOnlyList<Creature> targets,
        Func<Task> onImpact)
    {
        ArgumentNullException.ThrowIfNull(onImpact);
        AttackVfxSnapshot snapshot = Capture(attacker, targets);
        LocalOggOneShotPlayer.Play(EyeLaserSfxPath, -2f);
        if (TestMode.IsOn
            || !TryCreateRoot(
                "PhilosophyTwilightEyeLaser",
                snapshot,
                out Node2D root))
        {
            await onImpact();
            return;
        }

        Texture2D? bulletTexture = LoadTexture(EyeBulletPath);
        Texture2D? lanternTexture = LoadTexture(EyeLanternPath);
        Texture2D? trailTexture = LoadTexture(EyeTrailPath);
        Texture2D? explosionTexture = LoadTexture(EyeExplosionPath);
        if (bulletTexture == null
            || lanternTexture == null
            || trailTexture == null
            || explosionTexture == null)
        {
            root.QueueFree();
            await onImpact();
            return;
        }

        var additive = new CanvasItemMaterial
        {
            BlendMode = CanvasItemMaterial.BlendModeEnum.Add
        };
        PhilosophyFloorLiberationBackgroundController.SetEndBirdFacing(
            snapshot.Direction);
        var eyeOrigins = new Vector2[
            PhilosophyFloorLiberationBackgroundController.EndBirdEyeCount];
        for (int index = 0; index < eyeOrigins.Length; index++)
        {
            if (!TryResolveEndBirdEyeOrigin(root, index, out eyeOrigins[index]))
            {
                QueueFree(root);
                await onImpact();
                return;
            }
        }

        Tween chargeTween = root.CreateTween();
        chargeTween.SetParallel();
        for (int index = 0; index < eyeOrigins.Length; index++)
        {
            Sprite2D lantern = CreateSprite(
                $"EyeLaserChargeLantern{index:00}",
                lanternTexture,
                eyeOrigins[index],
                new Vector2(0.10f, 0.10f),
                new Color(1f, 0.74f, 0.10f, 0f),
                additive,
                5);
            Sprite2D charge = CreateSprite(
                $"EyeLaserChargeCore{index:00}",
                bulletTexture,
                eyeOrigins[index],
                new Vector2(0.04f, 0.04f),
                new Color(1f, 0.92f, 0.22f, 0f),
                additive,
                6);
            root.AddChildSafely(lantern);
            root.AddChildSafely(charge);
            int eyeIndex = index;
            chargeTween.TweenMethod(
                Callable.From<float>(_ => UpdateEyeChargeOrigin(
                    root,
                    eyeIndex,
                    lantern,
                    charge)),
                0f,
                1f,
                1.10f);
            float chargeDelay = index * 0.025f;
            chargeTween.TweenProperty(
                lantern,
                "modulate:a",
                0.50f,
                0.16f)
                .SetDelay(chargeDelay);
            chargeTween.TweenProperty(
                lantern,
                "scale",
                new Vector2(0.28f, 0.28f),
                0.26f)
                .SetDelay(chargeDelay)
                .SetTrans(Tween.TransitionType.Back)
                .SetEase(Tween.EaseType.Out);
            chargeTween.TweenProperty(
                charge,
                "modulate:a",
                0.88f,
                0.14f)
                .SetDelay(chargeDelay);
            chargeTween.TweenProperty(
                charge,
                "scale",
                new Vector2(0.12f, 0.12f),
                0.18f)
                .SetDelay(chargeDelay)
                .SetTrans(Tween.TransitionType.Back)
                .SetEase(Tween.EaseType.Out);
            chargeTween.TweenProperty(
                lantern,
                "modulate:a",
                0f,
                0.12f)
                .SetDelay(0.98f);
            chargeTween.TweenProperty(
                charge,
                "modulate:a",
                0f,
                0.12f)
                .SetDelay(0.98f);
        }
        // BlackForest fires exactly one second after the first eye begins its
        // charge. Individual original eye paths retain the 25 ms cascade.
        if (!await Wait(root, 1.0))
        {
            QueueFree(root);
            await onImpact();
            return;
        }

        var curves = new EyeCurveVisual[eyeOrigins.Length];
        for (int index = 0; index < curves.Length; index++)
        {
            Vector2 start = TryResolveEndBirdEyeOrigin(
                root,
                index,
                out Vector2 liveOrigin)
                ? liveOrigin
                : eyeOrigins[index];
            int targetIndex = ResolveEyeCurveTargetIndex(
                index,
                snapshot.Targets.Count);
            EyeCurveTemplate template =
                EyeCurveTemplates[index % EyeCurveTemplates.Length];
            Vector2 target = ResolvePlannedTargetPoint(
                snapshot,
                root,
                targetIndex,
                560f);
            Vector2[] points = BuildEyeCurvePoints(
                template,
                start,
                target,
                snapshot.Direction);
            Sprite2D bullet = CreateSprite(
                $"EyeLaserProjectile{index:00}",
                bulletTexture,
                points[0],
                new Vector2(0.09f, 0.09f),
                new Color(1f, 0.88f, 0.12f, 0f),
                additive,
                9);
            root.AddChildSafely(bullet);
            Sprite2D[] trails = Enumerable.Range(0, EyeTrailSpriteCount)
                .Select(trailIndex =>
                {
                    Sprite2D segment = CreateSprite(
                        $"EyeLaserTrail{index:00}_{trailIndex:00}",
                        trailTexture,
                        points[0],
                        new Vector2(0.10f, 0.04f),
                        new Color(1f, 0.54f, 0.06f, 0f),
                        additive,
                        7);
                    root.AddChildSafely(segment);
                    return segment;
                })
                .ToArray();
            GpuParticles2D particles = CreateContinuousParticles(
                root,
                $"EyeLaserGpuTrail{index:00}",
                trailTexture,
                points[0],
                new Color(1f, 0.50f, 0.04f, 0.78f),
                additive,
                8,
                amount: 18,
                lifetime: 0.30,
                scaleMin: 0.025f,
                scaleMax: 0.065f);
            curves[index] = new EyeCurveVisual(
                bullet,
                trails,
                particles,
                template,
                targetIndex,
                start,
                points);
        }

        Task<Task>[] arrivals = curves
            .Select((curve, index) => PlayEyeCurveToImpactAsync(
                root,
                snapshot,
                curve,
                explosionTexture,
                additive,
                snapshot.Direction,
                index))
            .ToArray();
        Task[] particleLifetimes = await Task.WhenAll(arrivals);

        NGame.Instance?.ScreenShake(
            ShakeStrength.Medium,
            ShakeDuration.Short,
            snapshot.Direction < 0f ? 180f : 0f);
        try
        {
            await onImpact();
            await Task.WhenAll(particleLifetimes);
        }
        finally
        {
            QueueFree(root);
        }
    }

    internal static bool TryResolveEndBirdEyeOrigin(
        Node2D vfxRoot,
        int eyeIndex,
        out Vector2 position)
    {
        if (!PhilosophyFloorLiberationBackgroundController
                .TryGetEndBirdEyeGlobalPosition(
                    eyeIndex,
                    out Vector2 globalPosition))
        {
            position = default;
            return false;
        }

        position = vfxRoot.ToLocal(globalPosition);
        return true;
    }

    private static void UpdateEyeChargeOrigin(
        Node2D root,
        int eyeIndex,
        Sprite2D lantern,
        Sprite2D charge)
    {
        if (!TryResolveEndBirdEyeOrigin(root, eyeIndex, out Vector2 position))
        {
            return;
        }

        lantern.Position = position;
        charge.Position = position;
    }

    internal static int ResolveEyeCurveTargetIndex(
        int curveIndex,
        int targetCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(curveIndex);
        return targetCount <= 0 ? -1 : curveIndex % targetCount;
    }

    private static Vector2[] BuildEyeCurvePoints(
        EyeCurveTemplate template,
        Vector2 start,
        Vector2 target,
        float direction)
    {
        // Apocalypse Bird's source curves face left. Mirror their shape only
        // when the attacker faces right, then map Cp2-4 between the resolved
        // eye marker and the independently resolved multiplayer target.
        float mirror = direction < 0f ? 1f : -1f;
        Vector2 sourceEnd = UnityCurveVector(template.End, mirror);
        Vector2 destinationEnd = target - start;
        return
        [
            start,
            start + MapEyeCurveVector(
                UnityCurveVector(template.ControlTwo, mirror),
                sourceEnd,
                destinationEnd),
            start + MapEyeCurveVector(
                UnityCurveVector(template.ControlThree, mirror),
                sourceEnd,
                destinationEnd),
            target
        ];
    }

    private static Vector2 UnityCurveVector(Vector2 value, float mirror) =>
        new(value.X * mirror, -value.Y);

    private static Vector2 MapEyeCurveVector(
        Vector2 value,
        Vector2 sourceEnd,
        Vector2 destinationEnd)
    {
        float denominator = sourceEnd.LengthSquared();
        if (denominator <= 0.0001f)
        {
            return destinationEnd;
        }

        float real = destinationEnd.Dot(sourceEnd) / denominator;
        float imaginary =
            (destinationEnd.Y * sourceEnd.X
                - destinationEnd.X * sourceEnd.Y)
            / denominator;
        return new Vector2(
            real * value.X - imaginary * value.Y,
            imaginary * value.X + real * value.Y);
    }

    private static async Task<Task> PlayEyeCurveToImpactAsync(
        Node2D root,
        AttackVfxSnapshot snapshot,
        EyeCurveVisual curve,
        Texture2D explosionTexture,
        Material additive,
        float direction,
        int index)
    {
        // BlackForest starts each shot 25 ms apart and gives the projectile a
        // 35 ms flash lead followed by a 300 ms flight.
        if (!await Wait(root, 0.035 + index * 0.025))
        {
            return Task.CompletedTask;
        }
        curve.Bullet.Modulate = new Color(1f, 0.86f, 0.12f, 0.98f);
        curve.TrailParticles.Restart();
        curve.TrailParticles.Emitting = true;

        Tween flight = root.CreateTween();
        flight.SetParallel();
        flight.SetTrans(Tween.TransitionType.Expo);
        flight.SetEase(Tween.EaseType.In);
        flight.TweenMethod(
            Callable.From<float>(t => UpdateEyeProjectile(
                root,
                snapshot,
                curve,
                t)),
            0f,
            1f,
            0.30f);
        flight.TweenProperty(
            curve.Bullet,
            "scale",
            new Vector2(0.14f, 0.14f),
            0.30f);
        if (!await Wait(root, 0.30))
        {
            return Task.CompletedTask;
        }

        RefreshEyeCurveTarget(root, snapshot, curve);
        Vector2 end = curve.Points[3];
        curve.Bullet.Visible = false;
        curve.TrailParticles.Emitting = false;
        Sprite2D explosion = CreateSprite(
            $"EyeLaserExplosion{index:00}",
            explosionTexture,
            end,
            new Vector2(0.08f, 0.08f),
            new Color(1f, 0.86f, 0.18f),
            additive,
            10);
        root.AddChildSafely(explosion);

        Tween explode = root.CreateTween();
        explode.SetParallel();
        explode.TweenProperty(
            explosion,
            "scale",
            new Vector2(0.52f, 0.52f),
            0.14f)
            .SetTrans(Tween.TransitionType.Expo)
            .SetEase(Tween.EaseType.Out);
        explode.TweenProperty(
            explosion,
            "scale",
            new Vector2(0.82f, 0.82f),
            0.42f)
            .SetDelay(0.125f)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.Out);
        explode.TweenProperty(explosion, "modulate:a", 0f, 0.36f)
            .SetDelay(0.185f);
        foreach (Sprite2D trail in curve.Trails)
        {
            explode.TweenProperty(trail, "modulate:a", 0f, 0.08f)
                .SetDelay(0.02f);
        }

        GpuParticles2D impactParticles = CreateBurstParticles(
            root,
            $"EyeLaserGpuImpact{index:00}",
            explosionTexture,
            end,
            new Vector2(direction, -0.18f),
            new Color(1f, 0.68f, 0.08f, 0.9f),
            additive,
            11,
            amount: 10,
            lifetime: 0.34,
            speedMin: 75f,
            speedMax: 220f,
            scaleMin: 0.025f,
            scaleMax: 0.095f,
            spread: 72f,
            gravityY: 90f);
        return Task.WhenAll(
            Wait(root, 0.58),
            AwaitParticleTail(root, curve.TrailParticles),
            AwaitParticleTail(root, impactParticles));
    }

    private static void RefreshEyeCurveTarget(
        Node2D root,
        AttackVfxSnapshot snapshot,
        EyeCurveVisual curve)
    {
        Vector2 target = ResolvePlannedTargetPoint(
            snapshot,
            root,
            curve.TargetIndex,
            560f);
        Vector2[] livePoints = BuildEyeCurvePoints(
            curve.Template,
            curve.Start,
            target,
            snapshot.Direction);
        Array.Copy(livePoints, curve.Points, livePoints.Length);
    }

    private static void UpdateEyeProjectile(
        Node2D root,
        AttackVfxSnapshot snapshot,
        EyeCurveVisual curve,
        float t)
    {
        RefreshEyeCurveTarget(root, snapshot, curve);
        IReadOnlyList<Vector2> points = curve.Points;
        Vector2 position = CubicBezier(
            points[0],
            points[1],
            points[2],
            points[3],
            t);
        Vector2 next = CubicBezier(
            points[0],
            points[1],
            points[2],
            points[3],
            Math.Min(1f, t + 0.018f));
        Vector2 tangent = next - position;
        curve.Bullet.Position = position;
        curve.TrailParticles.Position = position;
        if (tangent.LengthSquared() > 0.001f)
        {
            curve.Bullet.Rotation = tangent.Angle() + Mathf.Pi * 0.5f;
        }

        for (int index = 0; index < curve.Trails.Count; index++)
        {
            float headT = t - index * 0.035f;
            float tailT = headT - 0.028f;
            Sprite2D trail = curve.Trails[index];
            if (tailT <= 0f || headT <= 0f)
            {
                trail.Modulate = new Color(1f, 0.58f, 0.08f, 0f);
                continue;
            }

            headT = Math.Clamp(headT, 0f, 1f);
            tailT = Math.Clamp(tailT, 0f, 1f);
            Vector2 head = CubicBezier(
                points[0],
                points[1],
                points[2],
                points[3],
                headT);
            Vector2 tail = CubicBezier(
                points[0],
                points[1],
                points[2],
                points[3],
                tailT);
            Vector2 segment = head - tail;
            trail.Position = (head + tail) * 0.5f;
            trail.Rotation = segment.Angle() + Mathf.Pi * 0.5f;
            trail.Scale = new Vector2(
                0.20f,
                Math.Max(0.10f, (segment.Length() + 80f) / 250f));
            trail.Modulate = new Color(
                1f,
                0.58f,
                0.08f,
                0.70f * (1f - index / (float)curve.Trails.Count));
        }
    }

    private sealed record EyeCurveTemplate(
        Vector2 Pivot,
        Vector2 ControlTwo,
        Vector2 ControlThree,
        Vector2 End);

    private sealed record EyeCurveVisual(
        Sprite2D Bullet,
        IReadOnlyList<Sprite2D> Trails,
        GpuParticles2D TrailParticles,
        EyeCurveTemplate Template,
        int TargetIndex,
        Vector2 Start,
        Vector2[] Points);
}
