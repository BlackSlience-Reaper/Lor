using System;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.audio;
using LibraryOfRuina.monsters.PhilosophyFloorLiberation;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;

namespace LibraryOfRuina.backgrounds.PhilosophyFloorLiberation;

/// <summary>
/// Runtime bridge for the Apocalypse Bird composite rig and its three eggs.
/// The scene stays data-only; gameplay calls these methods after the combat
/// background layer has been instantiated.
/// </summary>
internal static class PhilosophyFloorLiberationBackgroundController
{
    internal const int EndBirdEyeCount = 16;

    private const string EggBreakSfxPath =
        "res://audio/sfx/philosophy_floor_liberation/egg_break.ogg";

    // BlackForest/Scenes/end_bird.tscn source transforms. EYES is a sibling
    // of Visuals, so each marker is first converted into the source Visuals'
    // local space, then mapped through this encounter's live Visuals node.
    // This keeps the projectile origins attached when the background layer,
    // FacingRoot, or the End Bird rig is moved, mirrored, or scaled.
    private static readonly Vector2 EndBirdSourceVisualsPosition =
        new(-512f, -780f);
    private const float EndBirdSourceVisualsScale = 0.92f;
    private static readonly Vector2 EndBirdSourceEyesPosition =
        new(-179f, -220f);
    private static readonly Vector2[] EndBirdSourceEyePositions =
    [
        new(-453f, -462f),
        new(592f, -768f),
        new(297f, -430f),
        new(-240f, -430f),
        new(-504f, -780f),
        new(-84f, -681f),
        new(280f, -719f),
        new(127f, -664f),
        new(-224f, -721f),
        new(540f, -455f),
        new(-398f, -264f),
        new(477f, -259f),
        new(-759f, -738f),
        new(778f, -523f),
        new(875f, -727f),
        new(-905f, -765f)
    ];

    private static AnimationPlayer? _slamPlayer;
    private static Action? _slamDisconnectFinishedHandler;
    private static TaskCompletionSource? _slamCompletion;

    /// <summary>
    /// Synchronizes normal/broken egg sprites and pulses the currently active
    /// surviving egg. Use <paramref name="flash"/> for a stronger switch cue.
    /// </summary>
    internal static void SetEggState(
        PhilosophyFloorTwilightEgg activeEgg,
        int aliveEggMask,
        bool flash = false)
    {
        foreach (PhilosophyFloorTwilightEgg egg in EggOrder)
        {
            Node2D? root = FindEggRoot(egg);
            if (root == null)
            {
                continue;
            }

            bool alive = (aliveEggMask & EggBit(egg)) != 0;
            AnimationPlayer? player =
                root.GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
            string animationName = !alive
                ? "broken_static"
                : egg == activeEgg
                    ? flash ? "active_flash" : "active"
                    : "inactive";
            if (player != null && player.HasAnimation(animationName))
            {
                player.ClearQueue();
                player.Stop();
                player.Play(animationName);
                player.Advance(0d);
                if (animationName == "active_flash"
                    && player.HasAnimation("active"))
                {
                    player.Queue("active");
                }
                continue;
            }

            ApplyEggFallbackState(root, alive, alive && egg == activeEgg);
        }
    }

    /// <summary>
    /// Replaces one egg with its original broken composite and plays the local
    /// break flash. The caller remains responsible for updating boss state.
    /// </summary>
    internal static async Task PlayEggBreak(PhilosophyFloorTwilightEgg egg)
    {
        LocalOggOneShotPlayer.Play(EggBreakSfxPath, -1.5f);
        Node2D? root = FindEggRoot(egg);
        if (root == null)
        {
            return;
        }

        NGame.Instance?.ScreenShake(
            ShakeStrength.Medium,
            ShakeDuration.Short);

        AnimationPlayer? player =
            root.GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
        if (player == null || !player.HasAnimation("broken"))
        {
            ApplyEggFallbackState(root, alive: false, active: false);
            return;
        }

        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        void OnTreeExit() => completion.TrySetResult();
        void OnFinished(StringName animationName)
        {
            if (animationName != "broken")
            {
                return;
            }

            // The finally block below disconnects this handler; disconnecting
            // here as well makes Godot report a nonexistent connection.
            if (GodotObject.IsInstanceValid(player)
                && player.HasAnimation("broken_static"))
            {
                player.Play("broken_static");
                player.Advance(0d);
            }
            completion.TrySetResult();
        }

        player.AnimationFinished += OnFinished;
        player.TreeExiting += OnTreeExit;
        try
        {
            player.ClearQueue();
            player.Stop();
            player.Play("broken");
            player.Advance(0d);
            await completion.Task;
        }
        finally
        {
            if (GodotObject.IsInstanceValid(player))
            {
                player.AnimationFinished -= OnFinished;
                player.TreeExiting -= OnTreeExit;
            }
        }
    }

    /// <summary>
    /// Resolves one of Apocalypse Bird's 16 yellow-eye markers through the
    /// currently instantiated background rig. The index is zero-based and
    /// follows BlackForest's eye1..eye16 ordering.
    /// </summary>
    internal static bool TryGetEndBirdEyeGlobalPosition(
        int eyeIndex,
        out Vector2 position)
    {
        if (eyeIndex < 0 || eyeIndex >= EndBirdEyeCount)
        {
            position = default;
            return false;
        }

        Node2D? endBird = FindBackgroundNode<Node2D>("PhilosophyEndBird");
        Node2D? visuals =
            endBird?.GetNodeOrNull<Node2D>("FacingRoot/Visuals");
        if (visuals == null
            || !GodotObject.IsInstanceValid(visuals)
            || !visuals.IsInsideTree())
        {
            position = default;
            return false;
        }

        Vector2 sourceEndBirdPoint =
            EndBirdSourceEyesPosition + EndBirdSourceEyePositions[eyeIndex];
        Vector2 sourceVisualsPoint =
            (sourceEndBirdPoint - EndBirdSourceVisualsPosition)
            / EndBirdSourceVisualsScale;
        position = visuals.ToGlobal(sourceVisualsPoint);
        return true;
    }

    /// <summary>
    /// Mirrors the composite End Bird with the same facing convention used by
    /// the attacker VFX: negative attacks left, positive attacks right.
    /// </summary>
    internal static void SetEndBirdFacing(float facingDirection)
    {
        Node2D? facingRoot = FindBackgroundNode<Node2D>("FacingRoot");
        if (facingRoot == null)
        {
            return;
        }

        facingRoot.Scale = new Vector2(
            facingDirection < 0f ? 1f : -1f,
            1f);
    }

    /// <summary>
    /// Recreates the original left-arm wind-up/downstroke. Horizontal motion
    /// and rotation follow the attacker's facing direction.
    /// </summary>
    internal static async Task PlayLeftArmSlam(float facingDirection)
    {
        AnimationPlayer? player = FindBackgroundNode<AnimationPlayer>(
            "PhilosophyEndBirdAnimationPlayer");
        Node2D? facingRoot = FindBackgroundNode<Node2D>("FacingRoot");
        if (player == null
            || facingRoot == null
            || !player.HasAnimation("slam"))
        {
            return;
        }

        InterruptSlamAnimation();
        float direction = facingDirection < 0f ? -1f : 1f;
        SetEndBirdFacing(direction);

        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        bool finishedHandlerConnected = false;
        void OnTreeExit() => completion.TrySetResult();

        // The finish callback, an interrupting slam and the finally block can
        // all reach this; Godot reports an error when a connection is
        // disconnected twice, so only the first call disconnects.
        void DisconnectFinishedHandler()
        {
            if (!finishedHandlerConnected)
            {
                return;
            }

            finishedHandlerConnected = false;
            if (GodotObject.IsInstanceValid(player))
            {
                player.AnimationFinished -= OnFinished;
            }
        }

        void OnFinished(StringName animationName)
        {
            if (animationName != "slam")
            {
                return;
            }

            DisconnectFinishedHandler();
            if (GodotObject.IsInstanceValid(player)
                && player.HasAnimation("idle"))
            {
                player.Play("idle");
                player.Advance(0d);
            }
            completion.TrySetResult();
        }

        _slamPlayer = player;
        _slamDisconnectFinishedHandler = DisconnectFinishedHandler;
        _slamCompletion = completion;
        player.AnimationFinished += OnFinished;
        finishedHandlerConnected = true;
        player.TreeExiting += OnTreeExit;
        try
        {
            player.ClearQueue();
            player.Stop();
            player.Play("slam");
            player.Advance(0d);
            await completion.Task;
        }
        finally
        {
            DisconnectFinishedHandler();
            if (GodotObject.IsInstanceValid(player))
            {
                player.TreeExiting -= OnTreeExit;
            }
            if (ReferenceEquals(_slamCompletion, completion))
            {
                _slamPlayer = null;
                _slamDisconnectFinishedHandler = null;
                _slamCompletion = null;
            }
        }
    }

    private static readonly PhilosophyFloorTwilightEgg[] EggOrder =
    [
        PhilosophyFloorTwilightEgg.BigEyes,
        PhilosophyFloorTwilightEgg.SmallBeak,
        PhilosophyFloorTwilightEgg.LongArms
    ];

    private static int EggBit(PhilosophyFloorTwilightEgg egg) => egg switch
    {
        PhilosophyFloorTwilightEgg.BigEyes => 0b001,
        PhilosophyFloorTwilightEgg.SmallBeak => 0b010,
        PhilosophyFloorTwilightEgg.LongArms => 0b100,
        _ => 0
    };

    private static string? EggNodeName(
        PhilosophyFloorTwilightEgg egg) => egg switch
    {
        PhilosophyFloorTwilightEgg.BigEyes => "BigEyes",
        PhilosophyFloorTwilightEgg.SmallBeak => "SmallBeak",
        PhilosophyFloorTwilightEgg.LongArms => "LongArms",
        _ => null
    };

    private static Node2D? FindEggRoot(PhilosophyFloorTwilightEgg egg)
    {
        string? nodeName = EggNodeName(egg);
        return nodeName == null
            ? null
            : FindBackgroundNode<Node2D>(nodeName);
    }

    private static T? FindBackgroundNode<T>(string nodeName)
        where T : Node
    {
        NCombatBackground? background = NCombatRoom.Instance?.Background;
        if (background == null)
        {
            return null;
        }

        return background.GetNodeOrNull<T>($"%{nodeName}")
            ?? background.FindChild(
                nodeName,
                recursive: true,
                owned: false) as T;
    }

    private static void ApplyEggFallbackState(
        Node2D root,
        bool alive,
        bool active)
    {
        if (root.GetNodeOrNull<Sprite2D>("Normal") is { } normal)
        {
            normal.Visible = alive;
            normal.Scale = Vector2.One;
            normal.Modulate = Colors.White;
        }
        if (root.GetNodeOrNull<Sprite2D>("Broken") is { } broken)
        {
            broken.Visible = !alive;
            broken.Scale = Vector2.One;
            broken.Modulate = Colors.White;
        }
        if (root.GetNodeOrNull<Sprite2D>("Glow") is { } glow)
        {
            glow.Visible = alive && active;
            glow.Scale = new Vector2(1.12f, 1.12f);
            glow.Modulate = new Color(1f, 0.94f, 0.38f, 0.62f);
        }
    }

    private static void InterruptSlamAnimation()
    {
        if (_slamPlayer != null
            && GodotObject.IsInstanceValid(_slamPlayer))
        {
            _slamDisconnectFinishedHandler?.Invoke();
            _slamPlayer.ClearQueue();
            _slamPlayer.Stop();
        }
        _slamCompletion?.TrySetResult();
        _slamPlayer = null;
        _slamDisconnectFinishedHandler = null;
        _slamCompletion = null;
    }
}
