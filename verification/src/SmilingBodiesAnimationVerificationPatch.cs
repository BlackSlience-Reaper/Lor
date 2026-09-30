using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using LibraryOfRuina.content.abnormalities.SmilingBodies;
using LibraryOfRuina.patches.visuals;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using Environment = System.Environment;

namespace LibraryOfRuinaVerification;

internal static class SmilingBodiesAnimationVerificationPatch
{
    private const string VerifyArg =
        "lor-verify-smiling-bodies-animation";
    private const string LogPrefix =
        "[LibraryOfRuina.SmilingBodiesAnimation.Verify] ";

    private static bool _started;

    internal static void Start()
    {
        if (_started || !HasVerifyArg())
        {
            return;
        }

        _started = true;
        Callable.From(() => TaskHelper.RunSafely(RunAsync())).CallDeferred();
    }

    private static bool HasVerifyArg() =>
        CommandLineHelper.HasArg(VerifyArg)
        || Environment.GetCommandLineArgs().Any(arg =>
            string.Equals(
                arg.TrimStart('-'),
                VerifyArg,
                StringComparison.OrdinalIgnoreCase));

    private static async Task RunAsync()
    {
        try
        {
            VerifyTemplateAndLibraries();
            await VerifyRuntimeRoutingAndIdleRestore();
            Log.Info(LogPrefix + "SMILING_BODIES_TSCN_ANIMATION_OK");
            NGame.Instance?.GetTree().Quit();
        }
        catch (Exception exception)
        {
            Log.Error(
                LogPrefix
                + "SMILING_BODIES_TSCN_ANIMATION_FAILED: "
                + exception);
            NGame.Instance?.GetTree().Quit(1);
        }
    }

    private static void VerifyTemplateAndLibraries()
    {
        PackedScene scene = ResourceLoader.Load<PackedScene>(
                SmilingBodiesCreatureVisuals.ScenePath)
            ?? throw new InvalidOperationException(
                "Could not load Smiling Bodies visual scene.");
        Node2D root = scene.Instantiate<Node2D>();
        try
        {
            Require(
                root.GetScript().VariantType == Variant.Type.Nil,
                "The Smiling Bodies template root must remain scriptless.");
            Require(
                root.Position.IsEqualApprox(Vector2.Zero)
                && root.Scale.IsEqualApprox(Vector2.One)
                && Mathf.IsZeroApprox(root.Rotation)
                && Mathf.IsZeroApprox(root.Skew),
                "The template root must remain identity; edit MotionRoot.");

            Node2D motionRoot = RequireNode<Node2D>(root, "MotionRoot");
            Sprite2D visuals = RequireNode<Sprite2D>(
                root,
                "MotionRoot/Visuals");
            Sprite2D attackVisuals = RequireNode<Sprite2D>(
                root,
                "MotionRoot/AttackVisuals");
            AnimationPlayer player = RequireNode<AnimationPlayer>(
                root,
                "AnimationPlayer");
            Control bounds = RequireNode<Control>(root, "Bounds");
            Marker2D center = RequireNode<Marker2D>(root, "CenterPos");
            Marker2D intent = RequireNode<Marker2D>(root, "IntentPos");
            Marker2D talk = RequireNode<Marker2D>(root, "TalkPos");

            foreach (Node node in new Node[]
                     {
                         motionRoot,
                         visuals,
                         attackVisuals,
                         player,
                         bounds,
                         center,
                         intent,
                         talk
                     })
            {
                Require(
                    node.UniqueNameInOwner,
                    $"Node '{node.Name}' lost unique_name_in_owner=true.");
            }

            Require(
                visuals.Position.IsEqualApprox(new Vector2(0f, -1f))
                && visuals.Scale.IsEqualApprox(new Vector2(0.72f, 0.72f)),
                "Visuals no longer preserve the 0.72 baseline layout.");
            Require(
                Mathf.IsEqualApprox(bounds.OffsetLeft, -230f)
                && Mathf.IsEqualApprox(bounds.OffsetTop, -339f)
                && Mathf.IsEqualApprox(bounds.OffsetRight, 230f)
                && Mathf.IsEqualApprox(bounds.OffsetBottom, 51f),
                "Smiling Bodies Bounds changed.");
            Require(
                center.Position.IsEqualApprox(new Vector2(0f, -34f))
                && intent.Position.IsEqualApprox(new Vector2(0f, -339f))
                && talk.Position.IsEqualApprox(new Vector2(0f, -224f)),
                "Smiling Bodies combat anchors changed.");
            Require(
                string.Equals(
                    player.Autoplay,
                    "phase_2/Idle",
                    StringComparison.Ordinal),
                "F6/editor autoplay must remain phase_2/Idle.");

            string[] expectedLibraries =
                SmilingBodiesAnimationContract.Phases
                    .Select(SmilingBodiesAnimationContract.LibraryForPhase)
                    .ToArray();
            RequireSameSet(
                "animation libraries",
                expectedLibraries,
                player.GetAnimationLibraryList()
                    .Select(static name => name.ToString()));

            foreach (SmilingBodiesPhase phase
                     in SmilingBodiesAnimationContract.Phases)
            {
                VerifyPhaseLibrary(player, phase);
            }
        }
        finally
        {
            root.Free();
        }
    }

    private static void VerifyPhaseLibrary(
        AnimationPlayer player,
        SmilingBodiesPhase phase)
    {
        string libraryName =
            SmilingBodiesAnimationContract.LibraryForPhase(phase);
        AnimationLibrary library = player.GetAnimationLibrary(libraryName)
            ?? throw new InvalidOperationException(
                $"Missing animation library '{libraryName}'.");
        Require(
            library.ResourcePath.EndsWith(
                $"smiling_bodies_{libraryName}_animations.tres",
                StringComparison.Ordinal),
            $"Animation library '{libraryName}' is not an external resource.");

        IReadOnlyList<string> expected =
            SmilingBodiesAnimationContract.AnimationsForPhase(phase);
        RequireSameSet(
            libraryName + " animations",
            expected,
            library.GetAnimationList()
                .Select(static name => name.ToString()));

        foreach (string triggerName in expected)
        {
            string qualified = SmilingBodiesCreatureVisuals
                .ResolveAnimationName(phase, triggerName);
            Require(
                string.Equals(
                    qualified,
                    $"{libraryName}/{triggerName}",
                    StringComparison.Ordinal),
                $"Phase route mismatch for {phase}/{triggerName}.");
            Require(
                player.HasAnimation(qualified),
                $"AnimationPlayer cannot resolve '{qualified}'.");

            Animation animation = library.GetAnimation(triggerName)
                ?? throw new InvalidOperationException(
                    $"Missing animation '{qualified}'.");
            if (triggerName == "Idle")
            {
                Require(
                    animation.LoopMode == Animation.LoopModeEnum.Linear,
                    $"'{qualified}' must loop.");
                VerifyTrackMode(
                    animation,
                    "MotionRoot/Visuals:texture",
                    Animation.UpdateMode.Discrete,
                    qualified);
                VerifyTrackMode(
                    animation,
                    "MotionRoot/Visuals:position",
                    Animation.UpdateMode.Continuous,
                    qualified);
                continue;
            }

            Require(
                animation.LoopMode == Animation.LoopModeEnum.None,
                $"'{qualified}' must not loop.");
            Require(
                Mathf.IsEqualApprox(
                    animation.Length,
                    SmilingBodiesAnimationContract.DurationForTrigger(
                        triggerName)),
                $"'{qualified}' length no longer matches the C# settlement "
                + "constant.");
            VerifyActionCompletionState(animation, qualified);
        }
    }

    private static void VerifyActionCompletionState(
        Animation animation,
        string qualifiedName)
    {
        int idleVisibleTrack = FindTrack(
            animation,
            "MotionRoot/Visuals:visible");
        int attackVisibleTrack = FindTrack(
            animation,
            "MotionRoot/AttackVisuals:visible");
        int idleLastKey = animation.TrackGetKeyCount(idleVisibleTrack) - 1;
        int attackLastKey = animation.TrackGetKeyCount(attackVisibleTrack) - 1;

        Require(
            idleLastKey >= 1
            && attackLastKey >= 1
            && animation.TrackGetKeyValue(idleVisibleTrack, idleLastKey)
                .AsBool()
            && !animation.TrackGetKeyValue(
                    attackVisibleTrack,
                    attackLastKey)
                .AsBool(),
            $"'{qualifiedName}' does not restore idle visibility at its end.");
        Require(
            Mathf.IsEqualApprox(
                (float)animation.TrackGetKeyTime(
                    idleVisibleTrack,
                    idleLastKey),
                animation.Length)
            && Mathf.IsEqualApprox(
                (float)animation.TrackGetKeyTime(
                    attackVisibleTrack,
                    attackLastKey),
                animation.Length),
            $"'{qualifiedName}' visibility restore is not on the final frame.");

        VerifyTrackMode(
            animation,
            "MotionRoot/AttackVisuals:texture",
            Animation.UpdateMode.Discrete,
            qualifiedName);
        VerifyTrackMode(
            animation,
            "MotionRoot/AttackVisuals:position",
            Animation.UpdateMode.Continuous,
            qualifiedName);
        VerifyTrackMode(
            animation,
            "MotionRoot/AttackVisuals:scale",
            Animation.UpdateMode.Continuous,
            qualifiedName);
    }

    private static void VerifyTrackMode(
        Animation animation,
        string path,
        Animation.UpdateMode expectedMode,
        string qualifiedName)
    {
        int track = FindTrack(animation, path);
        Require(
            animation.ValueTrackGetUpdateMode(track) == expectedMode,
            $"'{qualifiedName}' track '{path}' has the wrong update mode.");
    }

    private static int FindTrack(Animation animation, string path)
    {
        for (int track = 0; track < animation.GetTrackCount(); track++)
        {
            if (string.Equals(
                    animation.TrackGetPath(track).ToString(),
                    path,
                    StringComparison.Ordinal))
            {
                return track;
            }
        }

        throw new InvalidOperationException(
            $"Animation '{animation.ResourceName}' is missing track '{path}'.");
    }

    private static async Task VerifyRuntimeRoutingAndIdleRestore()
    {
        NGame game = NGame.Instance
            ?? throw new InvalidOperationException("NGame.Instance is null.");
        var host = new Node2D
        {
            Name = "SmilingBodiesAnimationVerificationHost"
        };
        var visuals = (SmilingBodiesCreatureVisuals)
            WrappedMonsterVisualFactory
                .CreateSceneBackedVisuals<SmilingBodiesCreatureVisuals>(
                    "SMILING_BODIES_VERIFY",
                    SmilingBodiesCreatureVisuals.ScenePath);
        host.AddChild(visuals);
        game.AddChild(host);

        try
        {
            await WaitFrame();
            const SmilingBodiesPhase fallbackPhase =
                SmilingBodiesPhase.Second;
            string idle = SmilingBodiesCreatureVisuals.ResolveAnimationName(
                fallbackPhase,
                "Idle");
            Require(
                string.Equals(
                    visuals.CurrentAnimationName,
                    idle,
                    StringComparison.Ordinal),
                "Scene host did not start in the fallback phase Idle.");

            Require(
                visuals.TryPlayTrigger("Absorb"),
                "Scene host rejected phase_2/Absorb.");
            Require(
                visuals.TryPlayTrigger("Hit"),
                "Scene host rejected an interrupting phase_2/Hit.");
            Require(
                string.Equals(
                    visuals.CurrentAnimationName,
                    SmilingBodiesCreatureVisuals.ResolveAnimationName(
                        fallbackPhase,
                        "Hit"),
                    StringComparison.Ordinal),
                "The interrupting Hit animation did not replace Absorb.");
            visuals.AnimationPlayer.Advance(
                SmilingBodiesAnimationContract.HitDurationSeconds + 0.02d);
            await WaitFrame();
            Require(
                string.Equals(
                    visuals.CurrentAnimationName,
                    idle,
                    StringComparison.Ordinal),
                "Interrupted action completion did not return to phase_2/Idle.");

            Require(
                visuals.TryPlayTrigger("Scream"),
                "Scene host rejected phase_2/Scream.");
            visuals.AnimationPlayer.Advance(
                SmilingBodiesAnimationContract.ActionDurationSeconds + 0.02d);
            await WaitFrame();
            Require(
                string.Equals(
                    visuals.CurrentAnimationName,
                    idle,
                    StringComparison.Ordinal),
                "Completed action did not return to phase_2/Idle.");
        }
        finally
        {
            host.QueueFree();
            await WaitFrame();
        }
    }

    private static T RequireNode<T>(Node root, string path)
        where T : Node
    {
        return root.GetNodeOrNull<T>(path)
            ?? throw new InvalidOperationException(
                $"Smiling Bodies template is missing '{path}' ({typeof(T).Name}).");
    }

    private static void RequireSameSet(
        string label,
        IEnumerable<string> expected,
        IEnumerable<string> actual)
    {
        string[] missing = expected
            .Except(actual, StringComparer.Ordinal)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();
        string[] unexpected = actual
            .Except(expected, StringComparer.Ordinal)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();
        Require(
            missing.Length == 0 && unexpected.Length == 0,
            $"{label} mismatch. Missing=[{string.Join(", ", missing)}], "
            + $"Unexpected=[{string.Join(", ", unexpected)}].");
    }

    private static async Task WaitFrame()
    {
        SceneTree tree = NGame.Instance?.GetTree()
            ?? (SceneTree)Engine.GetMainLoop();
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
