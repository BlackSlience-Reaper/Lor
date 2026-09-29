using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.framework.audio;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes;

namespace LibraryOfRuina.content.abnormalities.BigBird;

internal static class BigBirdFilterOverlay
{
    private const string CharmedLayerName = "BigBirdCharmedFilterOverlay";
    private const string RescueLayerName = "BigBirdRescueFilterOverlay";

    private static CanvasLayer? _charmedLayer;
    private static LocalOggLoopPlayer.LoopHandle? _charmedLoop;

    public static void RefreshForCombat(CombatStateLike? combatState)
    {
        bool shouldShow = ShouldShowCharmedOverlay(combatState);
        if (shouldShow)
        {
            EnsureCharmedOverlay();
        }
        else
        {
            StopCharmedOverlay();
        }
    }

    public static void Clear()
    {
        StopCharmedOverlay();
    }

    public static async Task PlayRescueSequence()
    {
        Node? host = NRun.Instance?.GlobalUi ?? (Node?)NGame.Instance;
        if (host == null)
        {
            return;
        }

        CanvasLayer? layer = CreateLayer(RescueLayerName, BigBird.RescueFilterFirstTexturePath, alpha: 0.82f);
        if (layer == null)
        {
            return;
        }

        try
        {
            host.AddChildSafely(layer);
            await Wait(layer, 2.0);
            if (!GodotObject.IsInstanceValid(layer) || !layer.IsInsideTree())
            {
                return;
            }

            SetLayerTexture(layer, BigBird.RescueFilterSecondTexturePath);
            await Wait(layer, 2.0);
        }
        finally
        {
            if (GodotObject.IsInstanceValid(layer))
            {
                layer.QueueFree();
            }
        }
    }

    private static bool ShouldShowCharmedOverlay(CombatStateLike? combatState)
    {
        if (combatState == null)
        {
            return false;
        }

        Player? me = LocalContext.GetMe(combatState.RunState);
        return me?.Creature.GetPower<BigBirdCharmedPower>() != null;
    }

    private static void EnsureCharmedOverlay()
    {
        if (_charmedLayer != null && GodotObject.IsInstanceValid(_charmedLayer))
        {
            return;
        }

        Node? host = NRun.Instance?.GlobalUi ?? (Node?)NGame.Instance;
        if (host == null)
        {
            return;
        }

        _charmedLayer = CreateLayer(CharmedLayerName, BigBird.CharmedFilterTexturePath, alpha: 0.28f);
        if (_charmedLayer == null)
        {
            return;
        }

        host.AddChildSafely(_charmedLayer);
        if (_charmedLayer.GetChild(0) is TextureRect rect)
        {
            Tween tween = rect.CreateTween();
            tween.SetLoops();
            tween.TweenProperty(rect, "modulate:a", 0.08f, 1.35f);
            tween.TweenProperty(rect, "modulate:a", 0.32f, 1.35f);
        }

        _charmedLoop = LocalOggLoopPlayer.StartLoop(BigBird.EyesLoopSfxPath, -11f);
    }

    private static void StopCharmedOverlay()
    {
        _charmedLoop?.Stop();
        _charmedLoop = null;

        if (_charmedLayer != null && GodotObject.IsInstanceValid(_charmedLayer))
        {
            _charmedLayer.QueueFree();
        }

        _charmedLayer = null;
    }

    private static CanvasLayer? CreateLayer(string name, string texturePath, float alpha)
    {
        Texture2D? texture = ResourceLoader.Load<Texture2D>(
            texturePath);
        if (texture == null)
        {
            Log.Error("[BigBirdFilterOverlay] Unable to load filter texture: " + texturePath);
            return null;
        }

        var layer = new CanvasLayer
        {
            Name = name,
            Layer = 225
        };

        var rect = new TextureRect
        {
            Name = "Filter",
            Texture = texture,
            LayoutMode = 1,
            AnchorsPreset = (int)Control.LayoutPreset.FullRect,
            AnchorRight = 1f,
            AnchorBottom = 1f,
            GrowHorizontal = Control.GrowDirection.Both,
            GrowVertical = Control.GrowDirection.Both,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            Modulate = new Color(1f, 1f, 1f, alpha)
        };

        layer.AddChild(rect);
        return layer;
    }

    private static void SetLayerTexture(CanvasLayer layer, string texturePath)
    {
        if (!GodotObject.IsInstanceValid(layer)
            || layer.GetChild(0) is not TextureRect rect)
        {
            return;
        }

        Texture2D? texture = ResourceLoader.Load<Texture2D>(
            texturePath);
        if (texture == null)
        {
            Log.Error("[BigBirdFilterOverlay] Unable to load filter texture: " + texturePath);
            return;
        }

        rect.Texture = texture;
    }

    private static Task Wait(Node node, double seconds)
    {
        if (!GodotObject.IsInstanceValid(node) || !node.IsInsideTree())
        {
            return Task.CompletedTask;
        }

        SceneTreeTimer? timer = node.GetTree()?.CreateTimer(seconds);
        if (timer == null)
        {
            return Task.CompletedTask;
        }

        var completion = new TaskCompletionSource();
        timer.Timeout += () => completion.TrySetResult();
        return completion.Task;
    }
}
