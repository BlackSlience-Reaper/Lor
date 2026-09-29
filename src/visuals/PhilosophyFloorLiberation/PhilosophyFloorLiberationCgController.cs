using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.monsters.PhilosophyFloorLiberation;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.TestSupport;

namespace LibraryOfRuina.visuals.PhilosophyFloorLiberation;

/// <summary>
/// Godot conversion of Library of Ruina's BinahFinalBattle_ImageFilter.
/// The original 1920x1080 Canvas layers are kept in their exact order and
/// its five-second Unity Animator alpha curve is represented by a Godot
/// AnimationPlayer resource.
/// </summary>
internal static class PhilosophyFloorLiberationCgController
{
    internal const string ScenePath =
        "res://scenes/vfx/philosophy_floor_liberation_cg.tscn";
    internal const string AnimationLibraryPath =
        "res://scenes/vfx/philosophy_floor_liberation_cg_animations.tres";

    private const string ImageRoot =
        "res://images/vfx/philosophy_floor_liberation/cg/";
    private const string AudioRoot =
        "res://audio/sfx/philosophy_floor_liberation/cg/";
    private const string IntroTexturePath = ImageRoot + "boss_bird_appear.png";
    private const string BigEyesTexturePath = ImageRoot + "big_bird_dead.png";
    private const string SmallBeakTexturePath = ImageRoot + "small_bird_dead.png";
    private const string LongArmsTexturePath = ImageRoot + "long_bird_dead.png";
    private const string VictoryTexturePath = ImageRoot + "boss_bird_dead.png";
    private const string BirthAudioPath = AudioRoot + "boss_bird_birth.ogg";
    private const string StoryAudioPath = AudioRoot + "boss_bird_story_filter.ogg";
    private const string VictoryAudioPath = AudioRoot + "boss_bird_story_filter_dead.ogg";
    private const string IntroTextKey =
        "PHILOSOPHY_FLOOR_LIBERATION_CG_INTRO.text";
    private const string BigEyesTextKey =
        "PHILOSOPHY_FLOOR_LIBERATION_CG_BIG_EYES_BREAK.text";
    private const string SmallBeakTextKey =
        "PHILOSOPHY_FLOOR_LIBERATION_CG_SMALL_BEAK_BREAK.text";
    private const string LongArmsTextKey =
        "PHILOSOPHY_FLOOR_LIBERATION_CG_LONG_ARMS_BREAK.text";
    private const string VictoryTextKey =
        "PHILOSOPHY_FLOOR_LIBERATION_CG_VICTORY.text";
    private const double PlaybackTimeoutSeconds = 5.75;
    internal const float IntroAudioLinearScale = 0.48f;
    private static readonly float IntroAudioVolumeDb =
        Mathf.LinearToDb(IntroAudioLinearScale);

    internal static readonly IReadOnlyList<string> AssetPaths =
    [
        ScenePath,
        AnimationLibraryPath,
        ImageRoot + "background.png",
        ImageRoot + "foreground.png",
        ImageRoot + "upper_frame.png",
        IntroTexturePath,
        BigEyesTexturePath,
        SmallBeakTexturePath,
        LongArmsTexturePath,
        VictoryTexturePath,
        BirthAudioPath,
        StoryAudioPath,
        VictoryAudioPath
    ];

    private static Task? _activePlayback;
    private static Control? _activeRoot;
    private static TaskCompletionSource? _activeCompletion;
    private static int _playbackGeneration;

    internal static Task PlayIntroAsync() =>
        PlayAsync(new CgSpec(
            "Intro",
            IntroTexturePath,
            IntroTextKey,
            BirthAudioPath,
            IntroAudioVolumeDb));

    internal static Task PlayEggBreakAsync(
        PhilosophyFloorTwilightEgg egg) => egg switch
    {
        PhilosophyFloorTwilightEgg.BigEyes => PlayAsync(new CgSpec(
            "BigEyesBreak",
            BigEyesTexturePath,
            BigEyesTextKey,
            StoryAudioPath)),
        PhilosophyFloorTwilightEgg.SmallBeak => PlayAsync(new CgSpec(
            "SmallBeakBreak",
            SmallBeakTexturePath,
            SmallBeakTextKey,
            StoryAudioPath)),
        PhilosophyFloorTwilightEgg.LongArms => PlayAsync(new CgSpec(
            "LongArmsBreak",
            LongArmsTexturePath,
            LongArmsTextKey,
            StoryAudioPath)),
        _ => Task.CompletedTask
    };

    internal static Task PlayVictoryAsync() =>
        PlayAsync(new CgSpec(
            "Victory",
            VictoryTexturePath,
            VictoryTextKey,
            VictoryAudioPath));

    internal static void ResetPresentation()
    {
        _playbackGeneration++;
        _activeCompletion?.TrySetResult();
        if (_activeRoot != null
            && GodotObject.IsInstanceValid(_activeRoot))
        {
            _activeRoot.QueueFreeSafely();
        }

        _activeRoot = null;
        _activeCompletion = null;
    }

    private static async Task PlayAsync(CgSpec spec)
    {
        int generation = _playbackGeneration;
        Task previousPlayback = _activePlayback ?? Task.CompletedTask;
        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        // Reserve this position before awaiting: simultaneous callers each
        // wait for their predecessor rather than sharing the same predecessor.
        _activePlayback = completion.Task;
        try
        {
            await previousPlayback;
            if (generation != _playbackGeneration)
            {
                return;
            }

            await PlayCoreAsync(spec, generation);
        }
        finally
        {
            completion.TrySetResult();
            if (ReferenceEquals(_activePlayback, completion.Task))
            {
                _activePlayback = null;
            }
        }
    }

    private static async Task PlayCoreAsync(CgSpec spec, int generation)
    {
        if (TestMode.IsOn)
        {
            return;
        }

        Control? host = ResolveHost();
        if (host == null)
        {
            Log.Warn("[PhilosophyFloorLiberationCg] No UI host for "
                     + spec.Name + ".");
            return;
        }

        PackedScene? scene = ResourceLoader.Load<PackedScene>(
            ScenePath);
        Texture2D? texture = ResourceLoader.Load<Texture2D>(
            spec.TexturePath);
        if (scene == null || texture == null)
        {
            Log.Error("[PhilosophyFloorLiberationCg] Missing scene or texture for "
                      + spec.Name + ".");
            return;
        }

        Control root = scene.Instantiate<Control>();
        root.Name = "PhilosophyFloorLiberationCg_" + spec.Name;
        TextureRect? storyImage =
            root.GetNodeOrNull<TextureRect>("%StoryImage");
        Label? storyText = root.GetNodeOrNull<Label>("%StoryText");
        AnimationPlayer? animationPlayer =
            root.GetNodeOrNull<AnimationPlayer>("%AnimationPlayer");
        if (storyImage == null
            || storyText == null
            || animationPlayer == null
            || !animationPlayer.HasAnimation("play"))
        {
            Log.Error("[PhilosophyFloorLiberationCg] Invalid overlay scene for "
                      + spec.Name + ".");
            root.QueueFreeSafely();
            return;
        }

        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _activeRoot = root;
        _activeCompletion = completion;

        void CompleteForAnimation(StringName animationName)
        {
            if (animationName == "play")
            {
                completion.TrySetResult();
            }
        }

        void CompleteForTreeExit() => completion.TrySetResult();

        animationPlayer.AnimationFinished += CompleteForAnimation;
        root.TreeExiting += CompleteForTreeExit;
        try
        {
            storyImage.Texture = texture;
            storyText.Text = new LocString("encounters", spec.TextKey)
                .GetFormattedText();
            host.AddChildSafely(root);
            SceneTree? tree = host.GetTree();
            if (tree == null)
            {
                return;
            }

            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            if (generation != _playbackGeneration
                || !GodotObject.IsInstanceValid(root)
                || !root.IsInsideTree()
                || root.IsQueuedForDeletion())
            {
                return;
            }

            root.MoveToFront();
            if (spec.AudioVolumeDb is float volumeDb)
            {
                LocalOggOneShotPlayer.Play(spec.AudioPath, volumeDb);
            }
            else
            {
                LocalOggOneShotPlayer.Play(spec.AudioPath);
            }
            animationPlayer.Play("play");
            animationPlayer.Advance(0d);
            _ = TaskHelper.RunSafely(CompleteAfterTimeout(tree, completion));
            await completion.Task;
        }
        finally
        {
            if (GodotObject.IsInstanceValid(animationPlayer))
            {
                animationPlayer.AnimationFinished -= CompleteForAnimation;
                animationPlayer.Stop();
            }
            if (GodotObject.IsInstanceValid(root))
            {
                root.TreeExiting -= CompleteForTreeExit;
                root.QueueFreeSafely();
            }
            if (ReferenceEquals(_activeRoot, root))
            {
                _activeRoot = null;
                _activeCompletion = null;
            }
        }
    }

    private static async Task CompleteAfterTimeout(
        SceneTree tree,
        TaskCompletionSource completion)
    {
        SceneTreeTimer timer = tree.CreateTimer(PlaybackTimeoutSeconds);
        await tree.ToSignal(timer, SceneTreeTimer.SignalName.Timeout);
        completion.TrySetResult();
    }

    private static Control? ResolveHost() =>
        NRun.Instance?.GlobalUi?.AboveTopBarVfxContainer
        ?? NCombatRoom.Instance?.CombatVfxContainer;

    private sealed record CgSpec(
        string Name,
        string TexturePath,
        string TextKey,
        string AudioPath,
        float? AudioVolumeDb = null);
}
