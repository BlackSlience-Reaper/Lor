using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using LibraryOfRuina.acts;
using LibraryOfRuina.encounters.KingOfGreed;
using LibraryOfRuina.patches;
using LibraryOfRuina.visuals.KingOfGreed;
using LibraryOfRuina.visuals.LittleRedMercenary;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Random;
using Environment = System.Environment;

namespace LibraryOfRuina.debug;

[HarmonyPatch(typeof(NMainMenu), nameof(NMainMenu._Ready))]
internal static class SceneBackedAbnormalityAnimationVerificationPatch
{
    private const string VerifyArg =
        "lor-verify-scene-backed-abnormalities";
    private const string LogPrefix =
        "[LibraryOfRuina.SceneBackedAbnormalities.Verify] ";

    private static bool _started;

    private static void Postfix()
    {
        if (_started || !HasVerifyArg())
        {
            return;
        }

        _started = true;
        Callable.From(
            () =>
            {
                _ = TaskHelper.RunSafely(RunAsync());
            }).CallDeferred();
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
            VerifyKingOfGreedTemplate();
            VerifyWolfTemplate();
            VerifyLibraryActBackgroundOffset();
            await VerifyRuntimeRouting();
            Log.Info(LogPrefix + "SCENE_BACKED_ABNORMALITIES_OK");
            NGame.Instance?.GetTree().Quit();
        }
        catch (Exception exception)
        {
            Log.Error(
                LogPrefix
                + "SCENE_BACKED_ABNORMALITIES_FAILED: "
                + exception);
            NGame.Instance?.GetTree().Quit(1);
        }
    }

    private static void VerifyKingOfGreedTemplate()
    {
        Node2D root = LoadTemplate(KingOfGreedCreatureVisuals.ScenePath);
        try
        {
            AnimationPlayer player = VerifyCommonTemplate(
                root,
                new Rect2(-160f, -410f, 320f, 422f),
                new Vector2(0f, -205f),
                new Vector2(0f, -445f),
                new Vector2(0f, -350f));
            Require(
                string.Equals(
                    player.Autoplay,
                    "magical_girl/Idle",
                    StringComparison.Ordinal),
                "King of Greed editor autoplay changed.");
            RequireSameSet(
                "King of Greed animation libraries",
                KingOfGreedAnimationContract.Libraries,
                player.GetAnimationLibraryList()
                    .Select(static name => name.ToString()));

            foreach (string libraryName
                     in KingOfGreedAnimationContract.Libraries)
            {
                VerifyLibrary(
                    player,
                    libraryName,
                    KingOfGreedAnimationContract.Animations,
                    KingOfGreedAnimationContract.DurationForTrigger,
                    $"king_of_greed_{libraryName}_animations.tres");
            }
        }
        finally
        {
            root.Free();
        }
    }

    private static void VerifyWolfTemplate()
    {
        Node2D root = LoadTemplate(
            WolfInHerNightmaresCreatureVisuals.ScenePath);
        try
        {
            AnimationPlayer player = VerifyCommonTemplate(
                root,
                new Rect2(-242f, -226f, 484f, 238f),
                new Vector2(-10f, -94f),
                new Vector2(-10f, -262f),
                new Vector2(-190f, -148f));
            Require(
                string.Equals(
                    player.Autoplay,
                    "wolf/Idle",
                    StringComparison.Ordinal),
                "Wolf in Her Nightmares editor autoplay changed.");
            RequireSameSet(
                "Wolf animation libraries",
                [WolfInHerNightmaresAnimationContract.Library],
                player.GetAnimationLibraryList()
                    .Select(static name => name.ToString()));
            VerifyLibrary(
                player,
                WolfInHerNightmaresAnimationContract.Library,
                WolfInHerNightmaresAnimationContract.Animations,
                WolfInHerNightmaresAnimationContract.DurationForTrigger,
                "wolf_in_her_nightmares_animations.tres");
        }
        finally
        {
            root.Free();
        }
    }

    private static Node2D LoadTemplate(string scenePath)
    {
        PackedScene scene = ResourceLoader.Load<PackedScene>(scenePath)
            ?? throw new InvalidOperationException(
                $"Could not load scene-backed visual '{scenePath}'.");
        Node2D root = scene.Instantiate<Node2D>();
        Require(
            root.GetScript().VariantType == Variant.Type.Nil,
            $"Template '{scenePath}' must remain scriptless.");
        Require(
            root.Position.IsEqualApprox(Vector2.Zero)
            && root.Scale.IsEqualApprox(Vector2.One)
            && Mathf.IsZeroApprox(root.Rotation)
            && Mathf.IsZeroApprox(root.Skew),
            $"Template '{scenePath}' root must remain identity.");
        return root;
    }

    private static AnimationPlayer VerifyCommonTemplate(
        Node2D root,
        Rect2 expectedBounds,
        Vector2 expectedCenter,
        Vector2 expectedIntent,
        Vector2 expectedTalk)
    {
        Node2D motionRoot = RequireNode<Node2D>(root, "MotionRoot");
        Sprite2D visuals = RequireNode<Sprite2D>(
            root,
            "MotionRoot/Visuals");
        Sprite2D attack = RequireNode<Sprite2D>(
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
                     attack,
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

        var actualBounds = new Rect2(
            bounds.OffsetLeft,
            bounds.OffsetTop,
            bounds.OffsetRight - bounds.OffsetLeft,
            bounds.OffsetBottom - bounds.OffsetTop);
        Require(
            actualBounds.IsEqualApprox(expectedBounds),
            $"Template '{root.Name}' Bounds changed.");
        Require(
            center.Position.IsEqualApprox(expectedCenter)
            && intent.Position.IsEqualApprox(expectedIntent)
            && talk.Position.IsEqualApprox(expectedTalk),
            $"Template '{root.Name}' combat anchors changed.");
        return player;
    }

    private static void VerifyLibrary(
        AnimationPlayer player,
        string libraryName,
        IReadOnlyList<string> expectedAnimations,
        Func<string, float> durationForTrigger,
        string expectedFileName)
    {
        AnimationLibrary library = player.GetAnimationLibrary(libraryName)
            ?? throw new InvalidOperationException(
                $"Missing animation library '{libraryName}'.");
        Require(
            library.ResourcePath.EndsWith(
                expectedFileName,
                StringComparison.Ordinal),
            $"Animation library '{libraryName}' must remain external.");
        RequireSameSet(
            $"{libraryName} animations",
            expectedAnimations,
            library.GetAnimationList()
                .Select(static name => name.ToString()));

        foreach (string triggerName in expectedAnimations)
        {
            Animation animation = library.GetAnimation(triggerName)
                ?? throw new InvalidOperationException(
                    $"Missing animation '{libraryName}/{triggerName}'.");
            if (triggerName == "Idle")
            {
                Require(
                    animation.LoopMode == Animation.LoopModeEnum.Linear,
                    $"'{libraryName}/Idle' must loop.");
                VerifyTrackMode(
                    animation,
                    "MotionRoot/Visuals:texture",
                    Animation.UpdateMode.Discrete);
                continue;
            }

            Require(
                animation.LoopMode == Animation.LoopModeEnum.None,
                $"'{libraryName}/{triggerName}' must not loop.");
            Require(
                Mathf.IsEqualApprox(
                    animation.Length,
                    durationForTrigger(triggerName)),
                $"'{libraryName}/{triggerName}' duration drifted.");
            VerifyActionCompletion(animation, libraryName, triggerName);
        }
    }

    private static void VerifyActionCompletion(
        Animation animation,
        string libraryName,
        string triggerName)
    {
        int idleVisible = FindTrack(
            animation,
            "MotionRoot/Visuals:visible");
        int attackVisible = FindTrack(
            animation,
            "MotionRoot/AttackVisuals:visible");
        int idleLast = animation.TrackGetKeyCount(idleVisible) - 1;
        int attackLast = animation.TrackGetKeyCount(attackVisible) - 1;
        string qualified = $"{libraryName}/{triggerName}";

        Require(
            animation.TrackGetKeyValue(idleVisible, idleLast).AsBool()
            && !animation.TrackGetKeyValue(attackVisible, attackLast)
                .AsBool(),
            $"'{qualified}' does not restore Idle visibility.");
        Require(
            Mathf.IsEqualApprox(
                (float)animation.TrackGetKeyTime(idleVisible, idleLast),
                animation.Length)
            && Mathf.IsEqualApprox(
                (float)animation.TrackGetKeyTime(
                    attackVisible,
                    attackLast),
                animation.Length),
            $"'{qualified}' visibility restore moved off its final frame.");
        VerifyTrackMode(
            animation,
            "MotionRoot/AttackVisuals:texture",
            Animation.UpdateMode.Discrete);
        VerifyTrackMode(
            animation,
            "MotionRoot/AttackVisuals:position",
            Animation.UpdateMode.Continuous);
        VerifyTrackMode(
            animation,
            "MotionRoot/AttackVisuals:scale",
            Animation.UpdateMode.Continuous);
    }

    private static void VerifyLibraryActBackgroundOffset()
    {
        KingOfGreedElite vanillaEncounter =
            (KingOfGreedElite)ModelDb.Encounter<KingOfGreedElite>()
                .ToMutable();
        KingOfGreedElite libraryEncounter =
            (KingOfGreedElite)ModelDb.Encounter<KingOfGreedElite>()
                .ToMutable();
        NCombatBackground vanilla = vanillaEncounter.CreateBackground(
            ModelDb.Act<Hive>(),
            new Rng(20260828u));
        NCombatBackground library = libraryEncounter.CreateBackground(
            ModelDb.Act<NetZech>(),
            new Rng(20260828u));
        try
        {
            Control vanillaLayer = RequireBackgroundLayer(vanilla);
            Control libraryLayer = RequireBackgroundLayer(library);
            Require(
                Mathf.IsEqualApprox(
                    libraryLayer.Position.Y,
                    vanillaLayer.Position.Y - 15f)
                && Mathf.IsEqualApprox(
                    libraryLayer.Position.X,
                    vanillaLayer.Position.X),
                "Library Act combat backgrounds are not shifted up 15px.");
            Require(
                libraryLayer.Scale.IsEqualApprox(vanillaLayer.Scale),
                "Library Act background offset changed responsive scaling.");
        }
        finally
        {
            vanilla.Free();
            library.Free();
        }
    }

    private static Control RequireBackgroundLayer(NCombatBackground background)
    {
        Control slot = background.GetNodeOrNull<Control>("Layer_00")
            ?? throw new InvalidOperationException(
                "Combat background is missing Layer_00.");
        return slot.GetChildOrNull<Control>(0)
            ?? throw new InvalidOperationException(
                "Combat background Layer_00 is empty.");
    }

    private static async Task VerifyRuntimeRouting()
    {
        NGame game = NGame.Instance
            ?? throw new InvalidOperationException("NGame.Instance is null.");
        var host = new Node2D
        {
            Name = "SceneBackedAbnormalityVerificationHost"
        };
        var king = (KingOfGreedCreatureVisuals)
            WrappedMonsterVisualFactory
                .CreateSceneBackedVisuals<KingOfGreedCreatureVisuals>(
                    "KING_OF_GREED_VERIFY",
                    KingOfGreedCreatureVisuals.ScenePath);
        var wolf = (WolfInHerNightmaresCreatureVisuals)
            WrappedMonsterVisualFactory
                .CreateSceneBackedVisuals<WolfInHerNightmaresCreatureVisuals>(
                    "WOLF_IN_HER_NIGHTMARES_VERIFY",
                    WolfInHerNightmaresCreatureVisuals.ScenePath);
        host.AddChild(king);
        host.AddChild(wolf);
        game.AddChild(host);

        try
        {
            await WaitFrame();
            Require(
                string.Equals(
                    king.CurrentAnimationName,
                    KingOfGreedCreatureVisuals.ResolveAnimationName(
                        kingForm: false,
                        "Idle"),
                    StringComparison.Ordinal),
                "King of Greed did not start in magical_girl/Idle.");
            Require(
                king.TryPlayTrigger("Attack")
                && string.Equals(
                    king.CurrentAnimationName,
                    KingOfGreedCreatureVisuals.ResolveAnimationName(
                        kingForm: false,
                        KingOfGreedAnimationContract.AttackStabTrigger),
                    StringComparison.Ordinal),
                "King of Greed first attack did not route to stab.");
            Require(
                king.TryPlayTrigger("Attack")
                && string.Equals(
                    king.CurrentAnimationName,
                    KingOfGreedCreatureVisuals.ResolveAnimationName(
                        kingForm: false,
                        KingOfGreedAnimationContract.AttackSlashTrigger),
                    StringComparison.Ordinal),
                "King of Greed second attack did not route to slash.");
            king.SetKingForm(true);
            Require(
                string.Equals(
                    king.CurrentAnimationName,
                    KingOfGreedCreatureVisuals.ResolveAnimationName(
                        kingForm: true,
                        "Idle"),
                    StringComparison.Ordinal),
                "King of Greed form switch did not route to king/Idle.");

            Require(
                wolf.TryPlayTrigger("Slash"),
                "Wolf scene rejected Slash.");
            Require(
                wolf.TryPlayTrigger("Hit"),
                "Wolf scene rejected an interrupting Hit.");
            Require(
                string.Equals(
                    wolf.CurrentAnimationName,
                    WolfInHerNightmaresCreatureVisuals
                        .ResolveAnimationName("Hit"),
                    StringComparison.Ordinal),
                "Wolf Hit did not interrupt Slash.");
            wolf.AnimationPlayer.Advance(
                WolfInHerNightmaresAnimationContract.HitDurationSeconds
                + 0.02d);
            await WaitFrame();
            Require(
                string.Equals(
                    wolf.CurrentAnimationName,
                    WolfInHerNightmaresCreatureVisuals
                        .ResolveAnimationName("Idle"),
                    StringComparison.Ordinal),
                "Wolf action completion did not return to Idle.");
        }
        finally
        {
            host.QueueFree();
            await WaitFrame();
        }
    }

    private static void VerifyTrackMode(
        Animation animation,
        string path,
        Animation.UpdateMode expected)
    {
        int track = FindTrack(animation, path);
        Require(
            animation.ValueTrackGetUpdateMode(track) == expected,
            $"Animation '{animation.ResourceName}' track '{path}' "
            + "has the wrong update mode.");
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
            $"Animation '{animation.ResourceName}' is missing '{path}'.");
    }

    private static T RequireNode<T>(Node root, string path)
        where T : Node =>
        root.GetNodeOrNull<T>(path)
        ?? throw new InvalidOperationException(
            $"Template '{root.Name}' is missing '{path}'.");

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
