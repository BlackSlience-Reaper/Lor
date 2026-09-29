using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.framework.audio;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;
using MegaCrit.Sts2.Core.TestSupport;
using static LibraryOfRuina.framework.visuals.common.VfxPrimitives;

namespace LibraryOfRuina.visuals.PhilosophyFloorLiberation;

// Judgment 与 TiltedScale 的天平。Judgment 落在攻击方向上的固定点；TiltedScale 按 TiltedScaleVisualScale 缩小，
// 挂在跟随目标节点的锚点上，每个目标一架，只有第一架播音效并触发伤害回调。
internal static partial class PhilosophyFloorLiberationVfx
{
    internal const float TiltedScaleVisualScale = 0.3f;

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
}
