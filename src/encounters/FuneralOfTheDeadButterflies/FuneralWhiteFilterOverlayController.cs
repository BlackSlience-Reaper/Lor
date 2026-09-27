using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace LibraryOfRuina.encounters.FuneralOfTheDeadButterflies;

internal static class FuneralWhiteFilterOverlayController
{
    private const string OverlayNodeName = "FuneralWhiteFilterOverlay";

    public static void PlayOverlay()
    {
        NCombatRoom? room = NCombatRoom.Instance;
        Control? container = room?.CombatVfxContainer;
        if (container == null)
        {
            return;
        }

        Texture2D? texture = ResourceLoader.Load<Texture2D>(
            monsters.FuneralOfTheDeadButterflies.FuneralOfTheDeadButterflies.WhiteFilterTexturePath);
        if (texture == null)
        {
            return;
        }

        var overlay = new TextureRect
        {
            Name = OverlayNodeName,
            LayoutMode = 1,
            AnchorsPreset = (int)Control.LayoutPreset.FullRect,
            AnchorRight = 1f,
            AnchorBottom = 1f,
            GrowHorizontal = Control.GrowDirection.Both,
            GrowVertical = Control.GrowDirection.Both,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Texture = texture,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            Modulate = new Color(1f, 1f, 1f, 0f)
        };

        container.AddChildSafely(overlay);
        overlay.MoveToFront();

        Tween tween = overlay.CreateTween();
        tween.TweenProperty(overlay, "modulate:a", 0.95f, 0.27f);
        tween.TweenInterval(1.05f);
        tween.TweenProperty(overlay, "modulate:a", 0f, 0.57f);
        tween.Finished += () =>
        {
            if (GodotObject.IsInstanceValid(overlay))
            {
                overlay.QueueFree();
            }
        };
    }
}
