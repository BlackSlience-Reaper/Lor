using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace LibraryOfRuina.content.abnormalities.QueenOfHatred;

internal static class QueenOfHatredInversionVideoController
{
    internal const string VideoPath = QueenOfHatredAssets.QueenOfHatredInversionVideo;
    private const float VideoAspectRatio = 16f / 9f;
    private const double UnknownStreamFallbackSeconds = 10.0;
    private const double PlaybackFallbackPaddingSeconds = 0.5;

    private static Task? _activePlayback;

    public static async Task PlayAsync(string videoPath = VideoPath)
    {
        if (_activePlayback is { IsCompleted: false })
        {
            await _activePlayback;
            return;
        }

        Task playback = PlayCoreAsync(videoPath);
        _activePlayback = playback;
        try
        {
            await playback;
        }
        finally
        {
            if (ReferenceEquals(_activePlayback, playback))
            {
                _activePlayback = null;
            }
        }
    }

    private static async Task PlayCoreAsync(string videoPath)
    {
        Control? host = ResolveHost();
        if (host == null)
        {
            Log.Warn("[QueenOfHatredInversionVideo] No UI host available; skipping transform video.");
            return;
        }

        if (!ResourceLoader.Exists(videoPath) && !FileAccess.FileExists(videoPath))
        {
            Log.Warn("[QueenOfHatredInversionVideo] Video not found: " + videoPath);
            return;
        }

        VideoStream stream = ResourceLoader.Load<VideoStream>(videoPath)
            ?? new VideoStreamTheora { File = videoPath };

        Control root = CreateRoot();
        VideoStreamPlayer player = CreatePlayer(stream);
        root.AddChild(CreateBackdrop());
        root.AddChild(CreateVideoFrame(player));

        host.AddChildSafely(root);
        SceneTree? tree = host.GetTree();
        if (tree == null)
        {
            root.QueueFreeSafely();
            return;
        }

        await host.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        if (!GodotObject.IsInstanceValid(root) || root.GetParent() == null)
        {
            return;
        }

        root.MoveToFront();

        var completion = new TaskCompletionSource();
        player.Finished += () => completion.TrySetResult();

        player.Play();
        StartFallbackTimer(player, tree, completion);

        try
        {
            await completion.Task;
        }
        finally
        {
            if (GodotObject.IsInstanceValid(player))
            {
                player.Stop();
            }

            if (GodotObject.IsInstanceValid(root))
            {
                root.QueueFreeSafely();
            }
        }
    }

    private static Control? ResolveHost()
    {
        return NRun.Instance?.GlobalUi?.AboveTopBarVfxContainer
            ?? NCombatRoom.Instance?.CombatVfxContainer;
    }

    private static Control CreateRoot()
    {
        var root = new Control
        {
            Name = "QueenOfHatredInversionVideoOverlay",
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        SetFullRect(root);
        return root;
    }

    private static ColorRect CreateBackdrop()
    {
        var backdrop = new ColorRect
        {
            Name = "Backdrop",
            Color = Colors.Black,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        SetFullRect(backdrop);
        return backdrop;
    }

    private static AspectRatioContainer CreateVideoFrame(VideoStreamPlayer player)
    {
        var frame = new AspectRatioContainer
        {
            Name = "VideoFrame",
            Ratio = VideoAspectRatio,
            StretchMode = AspectRatioContainer.StretchModeEnum.Cover,
            ClipContents = true,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        SetFullRect(frame);
        frame.AddChild(player);
        return frame;
    }

    private static VideoStreamPlayer CreatePlayer(VideoStream stream)
    {
        var player = new VideoStreamPlayer
        {
            Name = "Player",
            Stream = stream,
            Expand = true,
            Loop = false,
            Bus = "Master",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        SetFullRect(player);
        return player;
    }

    private static void SetFullRect(Control control)
    {
        control.LayoutMode = 1;
        control.AnchorsPreset = (int)Control.LayoutPreset.FullRect;
        control.AnchorLeft = 0f;
        control.AnchorTop = 0f;
        control.AnchorRight = 1f;
        control.AnchorBottom = 1f;
        control.OffsetLeft = 0f;
        control.OffsetTop = 0f;
        control.OffsetRight = 0f;
        control.OffsetBottom = 0f;
        control.GrowHorizontal = Control.GrowDirection.Both;
        control.GrowVertical = Control.GrowDirection.Both;
    }

    private static void StartFallbackTimer(
        VideoStreamPlayer player,
        SceneTree tree,
        TaskCompletionSource completion)
    {
        double length = player.GetStreamLength();
        _ = TaskHelper.RunSafely(CompleteAfterLength(
            tree,
            completion,
            ResolveFallbackDelay(length)));
    }

    internal static double ResolveFallbackDelay(double streamLength) =>
        (streamLength > 0
            ? streamLength
            : UnknownStreamFallbackSeconds)
        + PlaybackFallbackPaddingSeconds;

    private static async Task CompleteAfterLength(
        SceneTree tree,
        TaskCompletionSource completion,
        double seconds)
    {
        SceneTreeTimer timer = tree.CreateTimer(seconds);
        await tree.ToSignal(timer, SceneTreeTimer.SignalName.Timeout);
        completion.TrySetResult();
    }
}
