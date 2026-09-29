using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.audio;
using LibraryOfRuina.backgrounds.PhilosophyFloorLiberation;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;
using MegaCrit.Sts2.Core.TestSupport;
using static LibraryOfRuina.visuals.common.VfxPrimitives;

namespace LibraryOfRuina.visuals.PhilosophyFloorLiberation;

/// <summary>
/// Godot-native recreation of the original Apocalypse Bird attack effects.
/// Static impacts capture their anchors before the first await. Eye projectiles
/// instead launch from the live background End Bird eye markers and resolve
/// each planned player's scene node throughout flight.
/// </summary>
internal static class PhilosophyFloorLiberationVfx
{
    internal const float TiltedScaleVisualScale = 0.3f;

    private const string VfxRoot =
        "res://images/vfx/philosophy_floor_liberation/";
    private const string SfxRoot =
        "res://audio/sfx/philosophy_floor_liberation/";
    private const string PowerIconRoot = "res://images/powers/";

    private const string EyeBulletPath = VfxRoot + "EyeBullet1.png";
    private const string EyeLanternPath = VfxRoot + "EyeLantern.png";
    private const string EyeTrailPath = VfxRoot + "EyeBulletTrail.png";
    private const string EyeExplosionPath = VfxRoot + "EyeBulletExplosion.png";
    private const string JusticeLinePath = VfxRoot + "Justice1.png";
    private const string JusticeWeightPath = VfxRoot + "Justice2.png";
    private const string JusticeStarPath = VfxRoot + "Justice3.png";
    private const string JusticeRingPath = VfxRoot + "Justice4.png";
    private const string MeleeBurstPath = VfxRoot + "MeleeEffect2.png";
    private const string MeleeWavePath = VfxRoot + "MeleeEffect3.png";
    private const string SurveillanceEyesPath = VfxRoot + "BigBird1.png";

    private const string EyeLaserSfxPath = SfxRoot + "eye_laser.ogg";
    private const string JudgmentSfxPath = SfxRoot + "judgement.ogg";
    private const string PeaceSlamSfxPath = SfxRoot + "peace_slam.ogg";
    private const string PunishmentSfxPath = SfxRoot + "punishment.ogg";
    private const string SurveillanceSfxPath = SfxRoot + "surveillance.ogg";
    private const string EggBreakSfxPath = SfxRoot + "egg_break.ogg";

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

    internal static readonly IReadOnlyList<string> AssetPaths =
    [
        EyeBulletPath,
        EyeLanternPath,
        EyeTrailPath,
        EyeExplosionPath,
        JusticeLinePath,
        JusticeWeightPath,
        JusticeStarPath,
        JusticeRingPath,
        MeleeBurstPath,
        MeleeWavePath,
        SurveillanceEyesPath,
        EyeLaserSfxPath,
        JudgmentSfxPath,
        PeaceSlamSfxPath,
        PunishmentSfxPath,
        SurveillanceSfxPath,
        EggBreakSfxPath
    ];

    internal static readonly IReadOnlyList<string> PowerIconPaths =
    [
        PowerIconRoot + "philosophy_floor_twilight_black_monster_power.png",
        PowerIconRoot + "philosophy_floor_twilight_three_birds_power.png",
        PowerIconRoot + "philosophy_floor_twilight_broken_egg_power.png",
        PowerIconRoot + "philosophy_floor_twilight_sin_power.png",
        PowerIconRoot + "philosophy_floor_twilight_fear_power.png",
        // Canonical power ids come from Peace75/50/25 class names. The
        // underscored PEACE_75/50/25 ids are localization-only legacy keys.
        PowerIconRoot + "philosophy_floor_twilight_peace75_power.png",
        PowerIconRoot + "philosophy_floor_twilight_peace50_power.png",
        PowerIconRoot + "philosophy_floor_twilight_peace25_power.png"
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

    internal static Task PlayJudgmentAsync(
        Creature attacker,
        IReadOnlyList<Creature> targets) =>
        PlayJudgmentAsync(
            attacker,
            targets,
            static () => Task.CompletedTask);

    internal static Task PlayJudgmentAsync(
        Creature attacker,
        IReadOnlyList<Creature> targets,
        Func<Task> onImpact) =>
        PlayJusticeScaleAsync(
            attacker,
            targets,
            onImpact,
            trackedTarget: null,
            visualScale: 1f,
            rootName: "PhilosophyTwilightJudgment");

    internal static async Task PlayTiltedScaleAsync(
        Creature attacker,
        IReadOnlyList<Creature> targets,
        Func<Task> onImpact)
    {
        ArgumentNullException.ThrowIfNull(onImpact);
        Creature[] trackedTargets = targets
            .Where(static target => target.IsAlive)
            .Distinct()
            .ToArray();
        if (trackedTargets.Length == 0)
        {
            await onImpact();
            return;
        }

        Task[] visuals = trackedTargets
            .Select((target, index) => PlayJusticeScaleAsync(
                attacker,
                [target],
                index == 0
                    ? onImpact
                    : static () => Task.CompletedTask,
                target,
                visualScale: TiltedScaleVisualScale,
                rootName: $"PhilosophyTwilightTiltedScale{index}",
                playSfx: index == 0))
            .ToArray();
        await Task.WhenAll(visuals);
    }

    internal static Task PlayTiltedScaleAsync(
        Creature attacker,
        Creature target,
        Func<Task> onImpact) =>
        PlayJusticeScaleAsync(
            attacker,
            [target],
            onImpact,
            target,
            visualScale: TiltedScaleVisualScale,
            rootName: "PhilosophyTwilightTiltedScale",
            playSfx: true);

    private static async Task PlayJusticeScaleAsync(
        Creature attacker,
        IReadOnlyList<Creature> targets,
        Func<Task> onImpact,
        Creature? trackedTarget,
        float visualScale,
        string rootName,
        bool playSfx = true)
    {
        ArgumentNullException.ThrowIfNull(onImpact);
        AttackVfxSnapshot snapshot = Capture(attacker, targets);
        if (playSfx)
        {
            LocalOggOneShotPlayer.Play(JudgmentSfxPath, -2f);
        }
        if (TestMode.IsOn
            || !TryCreateRoot(rootName, snapshot, out Node2D root))
        {
            await onImpact();
            return;
        }

        Texture2D? lineTexture = LoadTexture(JusticeLinePath);
        Texture2D? weightTexture = LoadTexture(JusticeWeightPath);
        Texture2D? starTexture = LoadTexture(JusticeStarPath);
        Texture2D? ringTexture = LoadTexture(JusticeRingPath);
        if (lineTexture == null
            || weightTexture == null
            || starTexture == null
            || ringTexture == null)
        {
            root.QueueFree();
            await onImpact();
            return;
        }

        var additive = new CanvasItemMaterial
        {
            BlendMode = CanvasItemMaterial.BlendModeEnum.Add
        };
        Vector2 impact = trackedTarget == null
            ? ResolveImpactPoint(snapshot, root, 520f)
            : ResolvePlannedTargetPoint(snapshot, root, 0, 520f);
        float visualFacing = snapshot.Direction < 0f ? 1f : -1f;
        Node2D? targetAnchor = null;
        Node2D balanceParent = root;
        Vector2 balancePosition = impact + new Vector2(0f, -132f);
        if (trackedTarget != null)
        {
            targetAnchor = new Node2D
            {
                Name = "TiltedScaleTargetAnchor",
                Position = impact
            };
            root.AddChildSafely(targetAnchor);
            StartJusticeTargetFollow(root, targetAnchor, trackedTarget);
            balanceParent = targetAnchor;
            balancePosition = new Vector2(0f, -132f * visualScale);
        }

        var balance = new Node2D
        {
            Name = "JudgmentScale",
            Position = balancePosition,
            Scale = new Vector2(
                visualFacing * 1.05f * visualScale,
                1.05f * visualScale)
        };
        balanceParent.AddChildSafely(balance);

        Sprite2D line = CreateSprite(
            "ScaleBeam",
            lineTexture,
            Vector2.Zero,
            Vector2.One * 4.35f,
            new Color(1f, 1f, 1f, 0f),
            additive,
            4);
        Sprite2D leftWeight = CreateSprite(
            "ScaleLeftWeight",
            weightTexture,
            new Vector2(-216f, 7f),
            Vector2.One * 3.15f,
            new Color(1f, 1f, 1f, 0f),
            additive,
            5);
        Sprite2D rightWeight = CreateSprite(
            "ScaleRightWeight",
            weightTexture,
            new Vector2(216f, 7f),
            Vector2.One * 3.15f,
            new Color(1f, 1f, 1f, 0f),
            additive,
            5);
        leftWeight.Centered = false;
        leftWeight.Offset = new Vector2(-16.5f, 0f);
        rightWeight.Centered = false;
        rightWeight.Offset = new Vector2(-16.5f, 0f);
        Sprite2D star = CreateSprite(
            "ScaleJudgmentStar",
            starTexture,
            new Vector2(0f, -7f),
            Vector2.One * 0.36f,
            new Color(1f, 0.94f, 0.62f, 0f),
            additive,
            20);
        Sprite2D ring = CreateSprite(
            "ScaleJudgmentRing",
            ringTexture,
            Vector2.Zero,
            Vector2.One * 1.75f,
            new Color(1f, 0.92f, 0.55f, 0f),
            additive,
            8);
        ring.Vframes = 5;
        ring.Frame = 0;
        balance.AddChildSafely(line);
        balance.AddChildSafely(leftWeight);
        balance.AddChildSafely(rightWeight);
        balance.AddChildSafely(star);
        balance.AddChildSafely(ring);

        Tween reveal = balance.CreateTween();
        reveal.SetParallel();
        reveal.TweenProperty(
            balance,
            "scale",
            new Vector2(
                visualFacing * 1.38f * visualScale,
                1.38f * visualScale),
            0.32f)
            .SetTrans(Tween.TransitionType.Back)
            .SetEase(Tween.EaseType.Out);
        reveal.TweenProperty(line, "modulate:a", 1f, 0.22f);
        reveal.TweenProperty(leftWeight, "modulate:a", 0.95f, 0.22f)
            .SetDelay(0.06f);
        reveal.TweenProperty(rightWeight, "modulate:a", 0.95f, 0.22f)
            .SetDelay(0.06f);
        reveal.TweenProperty(star, "modulate:a", 0.95f, 0.26f)
            .SetDelay(0.10f);
        reveal.TweenProperty(star, "scale", Vector2.One * 0.48f, 0.70f)
            .SetDelay(0.10f)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.InOut);
        reveal.TweenProperty(
            balance,
            "rotation",
            Mathf.DegToRad(-2.5f) * visualFacing,
            0.70f)
            .SetDelay(0.28f)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.InOut);
        reveal.TweenProperty(
            balance,
            "rotation",
            Mathf.DegToRad(2f) * visualFacing,
            0.42f)
            .SetDelay(1.0f)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.InOut);
        if (!await Wait(root, 1.50))
        {
            QueueFree(root);
            await onImpact();
            return;
        }

        Tween swing = balance.CreateTween();
        swing.SetParallel();
        swing.TweenProperty(
            balance,
            "rotation",
            Mathf.DegToRad(18f) * visualFacing,
            0.24f)
            .SetTrans(Tween.TransitionType.Expo)
            .SetEase(Tween.EaseType.In);
        swing.TweenProperty(
            balance,
            "position",
            balance.Position
                + new Vector2(12f * visualFacing, 12f) * visualScale,
            0.24f)
            .SetTrans(Tween.TransitionType.Expo)
            .SetEase(Tween.EaseType.In);
        swing.TweenProperty(
            leftWeight,
            "position",
            new Vector2(-216f, -8f),
            0.24f)
            .SetTrans(Tween.TransitionType.Expo)
            .SetEase(Tween.EaseType.In);
        swing.TweenProperty(
            rightWeight,
            "position",
            new Vector2(216f, 24f),
            0.24f)
            .SetTrans(Tween.TransitionType.Expo)
            .SetEase(Tween.EaseType.In);
        swing.TweenProperty(
            leftWeight,
            "rotation",
            Mathf.DegToRad(-30f),
            0.16f)
            .SetTrans(Tween.TransitionType.Expo)
            .SetEase(Tween.EaseType.Out);
        swing.TweenProperty(
            rightWeight,
            "rotation",
            Mathf.DegToRad(-6f),
            0.16f)
            .SetTrans(Tween.TransitionType.Expo)
            .SetEase(Tween.EaseType.Out);
        swing.TweenMethod(
            Callable.From<float>(frame =>
                ring.Frame = Mathf.Clamp((int)Mathf.Floor(frame), 0, 4)),
            0f,
            4.99f,
            0.50f);
        swing.TweenProperty(ring, "modulate:a", 1f, 0.05f);
        swing.TweenProperty(ring, "scale", Vector2.One * 3.65f, 0.40f)
            .SetTrans(Tween.TransitionType.Expo)
            .SetEase(Tween.EaseType.Out);
        swing.TweenProperty(ring, "modulate:a", 0f, 0.52f)
            .SetDelay(0.36f);

        NGame.Instance?.ScreenShake(
            ShakeStrength.Medium,
            ShakeDuration.Short);
        Tween finish = balance.CreateTween();
        finish.SetParallel();
        finish.TweenProperty(balance, "modulate:a", 0f, 0.36f)
            .SetDelay(1.14f);
        finish.TweenProperty(
            balance,
            "scale",
            new Vector2(
                visualFacing * 1.06f * visualScale,
                1.06f * visualScale),
            0.34f)
            .SetDelay(1.16f)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.In);
        if (targetAnchor != null && trackedTarget != null)
        {
            UpdateJusticeTargetAnchor(root, targetAnchor, trackedTarget);
            impact = targetAnchor.Position;
        }
        GpuParticles2D judgmentParticles = CreateBurstParticles(
            root,
            "JudgmentGpuStars",
            starTexture,
            impact,
            new Vector2(snapshot.Direction, -0.25f),
            new Color(0.58f, 0.96f, 1f, 0.86f),
            additive,
            10,
            amount: 20,
            lifetime: 0.52,
            speedMin: 80f,
            speedMax: 260f,
            scaleMin: 0.025f * visualScale,
            scaleMax: 0.10f * visualScale,
            spread: 78f,
            gravityY: 52f);
        Task judgmentFx = Task.WhenAll(
            Wait(root, 1.50),
            AwaitParticleTail(root, judgmentParticles));
        try
        {
            await onImpact();
            await judgmentFx;
        }
        finally
        {
            QueueFree(root);
        }
    }

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

    internal static void PlayPunishmentSfx() =>
        LocalOggOneShotPlayer.Play(PunishmentSfxPath, -2f);

    internal static async Task PlaySurveillanceDarknessAsync(
        Creature attacker)
    {
        AttackVfxSnapshot snapshot = Capture(attacker, []);
        LocalOggOneShotPlayer.Play(SurveillanceSfxPath, -2f);
        if (TestMode.IsOn
            || !TryCreateRoot(
                "PhilosophyTwilightSurveillance",
                snapshot,
                out Node2D root))
        {
            return;
        }

        Texture2D? eyesTexture = LoadTexture(SurveillanceEyesPath);
        if (eyesTexture == null)
        {
            root.QueueFree();
            return;
        }

        Rect2 viewportRect = root.GetViewportRect();
        Vector2 topLeft = root.ToLocal(viewportRect.Position);
        Vector2 bottomRight = root.ToLocal(viewportRect.End);
        Vector2 overlaySize = new(
            Math.Abs(bottomRight.X - topLeft.X),
            Math.Abs(bottomRight.Y - topLeft.Y));
        var darkness = new ColorRect
        {
            Name = "SurveillanceDarkness",
            Position = topLeft,
            Size = overlaySize,
            Color = new Color(0f, 0f, 0f, 0f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 1
        };
        var eyes = new TextureRect
        {
            Name = "SurveillanceEyes",
            Position = topLeft,
            Size = overlaySize,
            Texture = eyesTexture,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            FlipH = snapshot.Direction > 0f,
            Modulate = new Color(1f, 0.38f, 0.08f, 0f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 2
        };
        root.AddChildSafely(darkness);
        root.AddChildSafely(eyes);

        Tween reveal = root.CreateTween();
        reveal.SetParallel();
        reveal.TweenProperty(
            darkness,
            "color",
            new Color(0f, 0f, 0f, 0.88f),
            0.20f);
        reveal.TweenProperty(eyes, "modulate:a", 0.82f, 0.26f);
        if (!await Wait(root, 0.48))
        {
            QueueFree(root);
            return;
        }
        Tween fade = root.CreateTween();
        fade.SetParallel();
        fade.TweenProperty(darkness, "color:a", 0f, 0.30f);
        fade.TweenProperty(eyes, "modulate:a", 0f, 0.30f);
        await Wait(root, 0.32);
        QueueFree(root);
    }

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

    private static AttackVfxSnapshot Capture(
        Creature attacker,
        IReadOnlyList<Creature> targets)
    {
        NCombatRoom? room = NCombatRoom.Instance;
        NCreature? attackerNode = room?.GetCreatureNode(attacker);
        Vector2 origin = attackerNode?.VfxSpawnPosition
            ?? attackerNode?.GlobalPosition
            ?? Vector2.Zero;
        AttackVfxTarget[] targetNodes = targets
            .Select(target => new AttackVfxTarget(
                target,
                TryResolveTargetNodePosition(target, out Vector2 position)
                    ? position
                    : null))
            .ToArray();
        float direction = attacker.Side == CombatSide.Enemy ? -1f : 1f;
        Vector2[] targetPositions = targetNodes
            .Where(static target => target.CapturedVfxPosition.HasValue)
            .Select(static target => target.CapturedVfxPosition!.Value)
            .ToArray();
        if (targetPositions.Length > 0)
        {
            float averageTargetX = targetPositions.Average(
                static point => point.X);
            float deltaX = averageTargetX - origin.X;
            if (Math.Abs(deltaX) > 1f)
            {
                direction = Mathf.Sign(deltaX);
            }
        }

        return new AttackVfxSnapshot(
            origin,
            direction,
            targetNodes);
    }

    /// <summary>
    /// Resolves the current combat-scene node for a planned target. STS2's
    /// projectile VFX convention uses the creature visuals' %CenterPos marker
    /// rather than a model-space estimate or a fixed screen coordinate.
    /// </summary>
    internal static bool TryResolveTargetNodePosition(
        Creature target,
        out Vector2 position)
    {
        NCreature? node = NCombatRoom.Instance?.GetCreatureNode(target);
        if (node == null
            || !GodotObject.IsInstanceValid(node)
            || !node.IsInsideTree())
        {
            position = default;
            return false;
        }

        position = node.VfxSpawnPosition;
        return true;
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

    private static void StartJusticeTargetFollow(
        Node2D root,
        Node2D anchor,
        Creature target)
    {
        UpdateJusticeTargetAnchor(root, anchor, target);
        Tween follow = root.CreateTween();
        follow.SetLoops();
        follow.TweenMethod(
            Callable.From<float>(_ =>
                UpdateJusticeTargetAnchor(root, anchor, target)),
            0f,
            1f,
            0.05f);
    }

    private static void UpdateJusticeTargetAnchor(
        Node2D root,
        Node2D anchor,
        Creature target)
    {
        if (TryResolveTargetNodePosition(target, out Vector2 globalPosition))
        {
            anchor.Position = root.ToLocal(globalPosition);
        }
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

    private static bool TryCreateRoot(
        string name,
        AttackVfxSnapshot snapshot,
        out Node2D root)
    {
        Control? host = NCombatRoom.Instance?.CombatVfxContainer;
        if (host == null)
        {
            root = null!;
            return false;
        }

        root = new Node2D
        {
            Name = name,
            ZIndex = 180
        };
        host.AddChildSafely(root);
        root.GlobalPosition = snapshot.AttackStart;
        return true;
    }

    private static Vector2 ResolveImpactPoint(
        AttackVfxSnapshot snapshot,
        Node2D root,
        float fallbackDistance)
    {
        Vector2[] capturedPositions = snapshot.Targets
            .Where(static target => target.CapturedVfxPosition.HasValue)
            .Select(static target => target.CapturedVfxPosition!.Value)
            .ToArray();
        Vector2 global = capturedPositions.Length == 0
            ? snapshot.AttackStart
                + new Vector2(snapshot.Direction * fallbackDistance, 40f)
            : capturedPositions.Aggregate(
                    Vector2.Zero,
                    static (sum, point) => sum + point)
                / capturedPositions.Length;
        Vector2 local = root.ToLocal(global);
        if (Mathf.Sign(local.X) != Mathf.Sign(snapshot.Direction)
            || Math.Abs(local.X) < 160f)
        {
            local.X = snapshot.Direction * fallbackDistance;
        }

        local.X = snapshot.Direction
            * Mathf.Clamp(Math.Abs(local.X), 300f, 760f);
        local.Y = Mathf.Clamp(local.Y, -220f, 260f);
        return local;
    }

    private static Vector2 ResolvePlannedTargetPoint(
        AttackVfxSnapshot snapshot,
        Node2D root,
        int targetIndex,
        float fallbackDistance)
    {
        if (targetIndex >= 0 && targetIndex < snapshot.Targets.Count)
        {
            AttackVfxTarget target = snapshot.Targets[targetIndex];
            if (TryResolveTargetNodePosition(
                    target.Creature,
                    out Vector2 livePosition))
            {
                return root.ToLocal(livePosition);
            }

            if (target.CapturedVfxPosition is { } capturedPosition)
            {
                return root.ToLocal(capturedPosition);
            }
        }

        return root.ToLocal(
            snapshot.AttackStart
            + new Vector2(snapshot.Direction * fallbackDistance, 40f));
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

    private sealed record AttackVfxTarget(
        Creature Creature,
        Vector2? CapturedVfxPosition);

    private sealed record AttackVfxSnapshot(
        Vector2 AttackStart,
        float Direction,
        IReadOnlyList<AttackVfxTarget> Targets);

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
