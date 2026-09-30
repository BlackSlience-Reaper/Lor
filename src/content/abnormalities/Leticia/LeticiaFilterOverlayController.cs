using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace LibraryOfRuina.content.abnormalities.Leticia;

internal static class LeticiaFilterOverlayController
{
    internal const string FilterOneTexturePath = LeticiaAssets.Filter1Texture;
    internal const string FilterTwoTexturePath = LeticiaAssets.Filter2Texture;

    public static void PlayGiftOpenOverlay()
    {
        PlayOverlay(FilterOneTexturePath, "LeticiaGiftOpenOverlay", 0.82f, 0.18f, 0.62f, 0.34f);
    }

    public static void PlayGiftCloseOverlay()
    {
        PlayOverlay(FilterTwoTexturePath, "LeticiaGiftCloseOverlay", 0.88f, 0.16f, 0.52f, 0.32f);
    }

    private static void PlayOverlay(
        string texturePath,
        string nodeName,
        float peakAlpha,
        float fadeInSeconds,
        float holdSeconds,
        float fadeOutSeconds)
    {
        Control? container = NCombatRoom.Instance?.CombatVfxContainer;
        if (container == null)
        {
            return;
        }

        Texture2D? texture = ResourceLoader.Load<Texture2D>(texturePath);
        if (texture == null)
        {
            return;
        }

        var overlay = new TextureRect
        {
            Name = nodeName,
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
        tween.TweenProperty(overlay, "modulate:a", peakAlpha, fadeInSeconds);
        tween.TweenInterval(holdSeconds);
        tween.TweenProperty(overlay, "modulate:a", 0f, fadeOutSeconds);
        tween.Finished += () =>
        {
            if (GodotObject.IsInstanceValid(overlay))
            {
                overlay.QueueFree();
            }
        };
    }
}
