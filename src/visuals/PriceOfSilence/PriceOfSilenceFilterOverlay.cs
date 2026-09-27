using Godot;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes;

namespace LibraryOfRuina.visuals.PriceOfSilence;

internal static class PriceOfSilenceFilterOverlay
{
    public static void FlashFor(Player? player)
    {
        if (player != null && LocalContext.IsMe(player))
        {
            Flash();
        }
    }

    public static void Flash()
    {
        Texture2D? texture = ResourceLoader.Load<Texture2D>(
            monsters.PriceOfSilence.PriceOfSilence.FilterTexturePath);
        if (texture == null)
        {
            Log.Error("[PriceOfSilenceFilterOverlay] Unable to load filter texture: " + monsters.PriceOfSilence.PriceOfSilence.FilterTexturePath);
            return;
        }

        Node? host = NRun.Instance?.GlobalUi ?? (Node?)NGame.Instance;
        if (host == null)
        {
            return;
        }

        var layer = new CanvasLayer
        {
            Name = "PriceOfSilenceFilterOverlay",
            Layer = 220
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
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize
        };

        layer.AddChild(rect);
        host.AddChildSafely(layer);

        SceneTreeTimer? timer = layer.GetTree()?.CreateTimer(1.1);
        if (timer != null)
        {
            timer.Timeout += () =>
            {
                if (GodotObject.IsInstanceValid(layer))
                {
                    layer.QueueFree();
                }
            };
        }
    }
}
