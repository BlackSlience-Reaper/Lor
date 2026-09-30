using Godot;
using LibraryOfRuina.framework.audio;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace LibraryOfRuina.content.abnormalities.Ozma;

internal static class OzmaTrueJackFlashOverlay
{
    private const string OverlayNodeName = "OzmaTrueJackFlashOverlay";
    internal const string TexturePath = OzmaAssets.OblivionMeetingTexture;

    private static TextureRect? _activeOverlay;
    private static Tween? _activeTween;

    public static void Play()
    {
        LocalOggOneShotPlayer.Play(Ozma.TrueJackGetCardSfxPath, -1.5f);
        Control? container = NCombatRoom.Instance?.CombatVfxContainer;
        if (container == null)
        {
            return;
        }

        Texture2D? texture = ResourceLoader.Load<Texture2D>(TexturePath);
        if (texture == null)
        {
            return;
        }

        CleanupActiveOverlay();

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
            Modulate = Colors.White
        };

        container.AddChildSafely(overlay);
        overlay.MoveToFront();
        Tween tween = overlay.CreateTween();
        _activeOverlay = overlay;
        _activeTween = tween;
        tween.TweenProperty(overlay, "modulate:a", 0f, 1f);
        tween.Finished += () =>
        {
            if (ReferenceEquals(_activeOverlay, overlay))
            {
                CleanupActiveOverlay();
            }
        };
    }

    private static void CleanupActiveOverlay()
    {
        _activeTween?.Kill();
        _activeTween = null;

        if (GodotObject.IsInstanceValid(_activeOverlay))
        {
            _activeOverlay!.QueueFree();
        }

        _activeOverlay = null;
    }
}
