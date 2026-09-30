using System;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.framework.audio;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using JudgementBirdMonster = LibraryOfRuina.content.abnormalities.JudgementBird.JudgementBird;

namespace LibraryOfRuina.content.abnormalities.JudgementBird;

internal static class JudgementBirdJudgementVideoController
{
    internal const string VideoPath =
        "res://videos/judgement_bird_judgement.ogv";

    internal const double HangCueSeconds = 0.56;
    internal const double ResolutionCueSeconds = 3.22;
    internal const double ExpectedDurationSeconds = 3.92;

    private const float VideoAspectRatio = 16f / 9f;
    private const double PlaybackFallbackPaddingSeconds = 0.5;

    internal static async Task PlayAsync(
        Func<Task> resolveJudgement)
    {
        ArgumentNullException.ThrowIfNull(resolveJudgement);

        Control? host = ResolveHost();
        if (host == null
            || (!ResourceLoader.Exists(VideoPath)
                && !FileAccess.FileExists(VideoPath)))
        {
            Log.Warn(
                "[JudgementBirdVideo] Video host or stream unavailable; "
                + "resolving judgement without overlay.");
            await resolveJudgement();
            return;
        }

        VideoStream stream = ResourceLoader.Load<VideoStream>(
                VideoPath)
            ?? new VideoStreamTheora { File = VideoPath };

        Control root = CreateRoot();
        VideoStreamPlayer player = CreatePlayer(stream);
        root.AddChild(CreateBackdrop());
        root.AddChild(CreateVideoFrame(player));
        host.AddChildSafely(root);

        SceneTree? tree = host.GetTree();
        if (tree == null)
        {
            root.QueueFreeSafely();
            await resolveJudgement();
            return;
        }

        await host.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        if (!GodotObject.IsInstanceValid(root)
            || root.GetParent() == null)
        {
            await resolveJudgement();
            return;
        }

        root.MoveToFront();
        var completion = new TaskCompletionSource();
        player.Finished += () => completion.TrySetResult();

        player.Play();
        LocalOggOneShotPlayer.Play(JudgementBirdMonster.OnSfxPath, -2f);
        StartFallbackTimer(player, tree, completion);

        try
        {
            await Wait(player, HangCueSeconds);
            LocalOggOneShotPlayer.Play(JudgementBirdMonster.HangSfxPath, -2f);

            await Wait(
                player,
                ResolutionCueSeconds - HangCueSeconds);
            LocalOggOneShotPlayer.Play(JudgementBirdMonster.DownSfxPath, -2f);
            await resolveJudgement();

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

    private static Control? ResolveHost() =>
        NRun.Instance?.GlobalUi?.AboveTopBarVfxContainer
        ?? NCombatRoom.Instance?.CombatVfxContainer;

    private static Control CreateRoot()
    {
        var root = new Control
        {
            Name = "JudgementBirdJudgementVideoOverlay",
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

    private static AspectRatioContainer CreateVideoFrame(
        VideoStreamPlayer player)
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

    private static async Task Wait(Node node, double seconds)
    {
        SceneTree? tree = node.GetTree();
        if (tree == null || seconds <= 0)
        {
            return;
        }

        SceneTreeTimer timer = tree.CreateTimer(seconds);
        await node.ToSignal(timer, SceneTreeTimer.SignalName.Timeout);
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
            : ExpectedDurationSeconds)
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
