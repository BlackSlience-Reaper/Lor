using System;
using System.Linq;
using Godot;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.combat;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace LibraryOfRuina.content.liberation.Philosophy;

/// <summary>
/// Godot-native recreation of the original Apocalypse Bird attack effects.
/// Static impacts capture their anchors before the first await. Eye projectiles
/// instead launch from the live background End Bird eye markers and resolve
/// each planned player's scene node throughout flight.
/// </summary>
internal static partial class PhilosophyFloorLiberationVfx
{
    private const string EyeBulletPath = PhilosophyFloorAssets.PhilosophyFloorLiberationVfxRoot + "EyeBullet1.png";
    private const string EyeLanternPath = PhilosophyFloorAssets.PhilosophyFloorLiberationVfxRoot + "EyeLantern.png";
    private const string EyeTrailPath = PhilosophyFloorAssets.PhilosophyFloorLiberationVfxRoot + "EyeBulletTrail.png";
    private const string EyeExplosionPath = PhilosophyFloorAssets.PhilosophyFloorLiberationVfxRoot + "EyeBulletExplosion.png";
    private const string JusticeLinePath = PhilosophyFloorAssets.PhilosophyFloorLiberationVfxRoot + "Justice1.png";
    private const string JusticeWeightPath = PhilosophyFloorAssets.PhilosophyFloorLiberationVfxRoot + "Justice2.png";
    private const string JusticeStarPath = PhilosophyFloorAssets.PhilosophyFloorLiberationVfxRoot + "Justice3.png";
    private const string JusticeRingPath = PhilosophyFloorAssets.PhilosophyFloorLiberationVfxRoot + "Justice4.png";
    private const string MeleeBurstPath = PhilosophyFloorAssets.PhilosophyFloorLiberationVfxRoot + "MeleeEffect2.png";
    private const string MeleeWavePath = PhilosophyFloorAssets.PhilosophyFloorLiberationVfxRoot + "MeleeEffect3.png";
    private const string SurveillanceEyesPath = PhilosophyFloorAssets.PhilosophyFloorLiberationVfxRoot + "BigBird1.png";

    private const string EyeLaserSfxPath = PhilosophyFloorAssets.PhilosophyFloorLiberationSfxRoot + "eye_laser.ogg";
    private const string JudgmentSfxPath = PhilosophyFloorAssets.PhilosophyFloorLiberationSfxRoot + "judgement.ogg";
    private const string PeaceSlamSfxPath = PhilosophyFloorAssets.PhilosophyFloorLiberationSfxRoot + "peace_slam.ogg";
    private const string PunishmentSfxPath = PhilosophyFloorAssets.PhilosophyFloorLiberationSfxRoot + "punishment.ogg";
    private const string SurveillanceSfxPath = PhilosophyFloorAssets.PhilosophyFloorLiberationSfxRoot + "surveillance.ogg";
    private const string EggBreakSfxPath = PhilosophyFloorAssets.PhilosophyFloorLiberationSfxRoot + "egg_break.ogg";

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
        PhilosophyFloorAssets.ImagesPowersRoot + "philosophy_floor_twilight_black_monster_power.png",
        PhilosophyFloorAssets.ImagesPowersRoot + "philosophy_floor_twilight_three_birds_power.png",
        PhilosophyFloorAssets.ImagesPowersRoot + "philosophy_floor_twilight_broken_egg_power.png",
        PhilosophyFloorAssets.ImagesPowersRoot + "philosophy_floor_twilight_sin_power.png",
        PhilosophyFloorAssets.ImagesPowersRoot + "philosophy_floor_twilight_fear_power.png",
        // Canonical power ids come from Peace75/50/25 class names. The
        // underscored PEACE_75/50/25 ids are localization-only legacy keys.
        PhilosophyFloorAssets.ImagesPowersRoot + "philosophy_floor_twilight_peace75_power.png",
        PhilosophyFloorAssets.ImagesPowersRoot + "philosophy_floor_twilight_peace50_power.png",
        PhilosophyFloorAssets.ImagesPowersRoot + "philosophy_floor_twilight_peace25_power.png"
    ];

    internal static void PlayPunishmentSfx() =>
        LocalOggOneShotPlayer.Play(PunishmentSfxPath, -2f);

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
        NCreature? node = CombatQueries.CreatureNodeOf(target);
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

    private sealed record AttackVfxTarget(
        Creature Creature,
        Vector2? CapturedVfxPosition);

    private sealed record AttackVfxSnapshot(
        Vector2 AttackStart,
        float Direction,
        IReadOnlyList<AttackVfxTarget> Targets);
}
