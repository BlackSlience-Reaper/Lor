using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using LibraryOfRuina.framework.visuals;

namespace LibraryOfRuina.content.abnormalities.FairyFestival;

internal static class FairyFestivalBackgroundController
{
    private const string NormalTexturePath = "res://images/backgrounds/fairy_festival_strong/background_1.png";
    private const string StarvedTexturePath = "res://images/backgrounds/fairy_festival_strong/background_2.png";
    private const string OverlayTexturePath = "res://images/vfx/fairy_festival_predation_overlay.png";

    public static void SetStarvedBackground(bool isStarved) =>
        CombatBackgroundImage.SetTexture(
            CombatBackgroundImage.Find("FairyFestivalBackgroundImage"),
            isStarved ? StarvedTexturePath : NormalTexturePath);

    public static void PlayPredationOverlay()
    {
        NCombatRoom? room = NCombatRoom.Instance;
        Control? container = room?.CombatVfxContainer;
        if (container == null)
        {
            return;
        }

        Texture2D? texture = ResourceLoader.Load<Texture2D>(OverlayTexturePath);
        if (texture == null)
        {
            return;
        }

        var overlay = new TextureRect
        {
            Name = "FairyFestivalPredationOverlay",
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
        tween.TweenProperty(overlay, "modulate:a", 0.9f, 0.18f);
        tween.TweenInterval(0.90f);
        tween.TweenProperty(overlay, "modulate:a", 0f, 0.38f);
        tween.Finished += () =>
        {
            if (GodotObject.IsInstanceValid(overlay))
            {
                overlay.QueueFree();
            }
        };
    }
}
