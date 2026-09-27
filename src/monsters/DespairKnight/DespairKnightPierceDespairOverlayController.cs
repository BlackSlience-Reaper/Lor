using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace LibraryOfRuina.monsters.DespairKnight;

internal static class DespairKnightPierceDespairOverlayController
{
    private const string Root = "res://images/vfx/despair_knight_pierce_despair";
    private const float AspectRatio = 564f / 316f;
    private const double FallbackFrameSeconds = 0.10;

    private static readonly IReadOnlyList<IReadOnlyList<double>> SegmentFrameDurations =
    [
        [0.70, 0.10, 0.70],
        [0.70, 0.10, 0.80],
        [0.80, 0.10, 0.90]
    ];

    internal static readonly string[] AssetPaths =
    [
        FramePath(1, 0),
        FramePath(1, 1),
        FramePath(1, 2),
        FramePath(2, 0),
        FramePath(2, 1),
        FramePath(2, 2),
        FramePath(3, 0),
        FramePath(3, 1),
        FramePath(3, 2)
    ];

    private static Task? _activePlayback;

    public static async Task PlayAsync(int stabbedSwordCount)
    {
        int segment = Math.Clamp(stabbedSwordCount, 1, ForgottenKnightSword.RequiredSwordCount);
        if (_activePlayback is { IsCompleted: false })
        {
            await _activePlayback;
        }

        Task playback = PlayCoreAsync(segment);
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

    private static async Task PlayCoreAsync(int segment)
    {
        Control? host = ResolveHost();
        if (host == null)
        {
            Log.Warn("[DespairKnightPierceDespairOverlay] No UI host available; skipping overlay.");
            return;
        }

        Texture2D[] frames = LoadFrames(segment).ToArray();
        if (frames.Length == 0)
        {
            Log.Warn("[DespairKnightPierceDespairOverlay] No frames available for segment " + segment + ".");
            return;
        }

        Control root = CreateRoot();
        TextureRect frame = CreateFrame(frames[0]);
        root.AddChild(CreateBackdrop());
        root.AddChild(CreateFrameContainer(frame));

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

        try
        {
            IReadOnlyList<double> frameDurations = GetFrameDurations(segment);
            for (int i = 0; i < frames.Length; i++)
            {
                if (!GodotObject.IsInstanceValid(frame))
                {
                    return;
                }

                frame.Texture = frames[i];
                double duration = i < frameDurations.Count
                    ? frameDurations[i]
                    : FallbackFrameSeconds;
                SceneTreeTimer timer = tree.CreateTimer(Math.Max(FallbackFrameSeconds, duration));
                await root.ToSignal(timer, SceneTreeTimer.SignalName.Timeout);
            }
        }
        finally
        {
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

    private static IEnumerable<Texture2D> LoadFrames(int segment)
    {
        for (int i = 0; i < GetFrameDurations(segment).Count; i++)
        {
            string path = FramePath(segment, i);
            Texture2D? texture = ResourceLoader.Load<Texture2D>(path);
            if (texture == null)
            {
                Log.Warn("[DespairKnightPierceDespairOverlay] Missing frame: " + path);
                continue;
            }

            yield return texture;
        }
    }

    private static Control CreateRoot()
    {
        var root = new Control
        {
            Name = "DespairKnightPierceDespairOverlay",
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

    private static AspectRatioContainer CreateFrameContainer(TextureRect frame)
    {
        var container = new AspectRatioContainer
        {
            Name = "FrameContainer",
            Ratio = AspectRatio,
            StretchMode = AspectRatioContainer.StretchModeEnum.Cover,
            ClipContents = true,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        SetFullRect(container);
        container.AddChild(frame);
        return container;
    }

    private static TextureRect CreateFrame(Texture2D texture)
    {
        var frame = new TextureRect
        {
            Name = "Frame",
            Texture = texture,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        SetFullRect(frame);
        return frame;
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

    private static string FramePath(int segment, int frame) =>
        $"{Root}/segment_{segment}/frame_{frame:00}.png";

    private static IReadOnlyList<double> GetFrameDurations(int segment)
    {
        int index = Math.Clamp(segment, 1, ForgottenKnightSword.RequiredSwordCount) - 1;
        return SegmentFrameDurations[index];
    }
}
