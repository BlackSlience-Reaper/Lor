using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace LibraryOfRuina.content.liberation.Natural;

internal static class NaturalFloorDespairVideoController
{
    internal static readonly string[] AssetPaths = Enumerable.Range(1, 3)
        .Select(i => $"res://videos/natural_floor_tear_edge_stab_{i}.ogv").ToArray();
    private static Task? _active;
    private static Control? _root;
    private static VideoStreamPlayer? _player;
    private static TaskCompletionSource? _completion;

    internal static async Task PlayAsync(int count, Action onImpact)
    {
        while (_active is { IsCompleted: false } active)
        {
            await active;
        }

        Task playback = PlayCoreAsync(Math.Clamp(count, 1, 3), onImpact);
        _active = playback;
        try
        {
            await playback;
        }
        finally
        {
            if (ReferenceEquals(_active, playback))
            {
                _active = null;
            }
        }
    }

    private static async Task PlayCoreAsync(int segment, Action onImpact)
    {
        Control? host = NRun.Instance?.GlobalUi?.AboveTopBarVfxContainer ?? NCombatRoom.Instance?.CombatVfxContainer;
        string path = AssetPaths[segment - 1];
        if (host?.GetTree() is not { } tree || !ResourceLoader.Exists(path))
        {
            Log.Warn("[NaturalFloorDespair] Cannot play insertion video " + path);
            onImpact();
            return;
        }
        var root = new Control { Name = "NaturalFloorDespairVideo", MouseFilter = Control.MouseFilterEnum.Stop };
        var background = new ColorRect { Color = Colors.Black, MouseFilter = Control.MouseFilterEnum.Ignore };
        var frame = new AspectRatioContainer
        {
            Ratio = 16f / 9f,
            StretchMode = AspectRatioContainer.StretchModeEnum.Fit,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        var player = new VideoStreamPlayer
        {
            Stream = ResourceLoader.Load<VideoStream>(path) ?? new VideoStreamTheora { File = path },
            Expand = true,
            Loop = false,
            VolumeDb = -80f,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        root.AddChild(background);
        root.AddChild(frame);
        frame.AddChild(player);
        host.AddChildSafely(root);
        foreach (Control control in new Control[] { root, background, frame, player })
        {
            control.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        }

        _root = root;
        _player = player;
        var completion = new TaskCompletionSource();
        _completion = completion;
        player.Finished += () => completion.TrySetResult();
        root.TreeExiting += () => completion.TrySetResult();
        tree.CreateTimer(4).Timeout += () => completion.TrySetResult();
        try
        {
            root.MoveToFront();
            player.Play();
            SceneTreeTimer impact = tree.CreateTimer(segment == 3 ? 0.9 : 0.8);
            var marker = new TaskCompletionSource();
            impact.Timeout += () => marker.TrySetResult();
            if (await Task.WhenAny(marker.Task, completion.Task) == marker.Task)
            {
                onImpact();
            }

            await completion.Task;
        }
        finally
        {
            if (ReferenceEquals(_root, root))
            {
                Stop();
            }
        }
    }

    internal static void Stop()
    {
        TaskCompletionSource? completion = _completion;
        VideoStreamPlayer? player = _player;
        Control? root = _root;
        _completion = null;
        _root = null;
        _player = null;
        if (GodotObject.IsInstanceValid(player))
        {
            player!.Stop();
        }

        if (GodotObject.IsInstanceValid(root))
        {
            root!.QueueFreeSafely();
        }

        // Await continuations may start the next video immediately; clear this one first.
        completion?.TrySetResult();
    }
}
