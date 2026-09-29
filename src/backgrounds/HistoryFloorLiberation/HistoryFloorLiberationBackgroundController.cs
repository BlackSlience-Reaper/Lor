using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using LibraryOfRuina.infra.helpers;

namespace LibraryOfRuina.backgrounds.HistoryFloorLiberation;

internal static class HistoryFloorLiberationBackgroundController
{
    public const string PhaseOneTexturePath = "res://images/backgrounds/history_floor_liberation_encounter/background_1.png";
    public const string ForgottenTexturePath = "res://images/backgrounds/history_floor_liberation_encounter/background_2.png";
    public const string FlutteringTexturePath = "res://images/backgrounds/history_floor_liberation_encounter/fluttering_background_1.png";
    public const string FlutteringStarvedTexturePath = "res://images/backgrounds/history_floor_liberation_encounter/fluttering_background_2.png";
    public const string FlutteringPredationOverlayTexturePath = "res://images/vfx/history_floor_fluttering_predation_overlay.png";
    public const string WaspTexturePath = "res://images/backgrounds/history_floor/wasp_background.png";
    public const string WaspBuffOverlayTexturePath = "res://images/vfx/wasp_buff_overlay.png";
    public const string WaspLoyaltyOverlayTexturePath = "res://images/vfx/wasp_loyalty_overlay.png";
    public const string EmeraldBoughTexturePath = "res://images/backgrounds/history_floor/emerald_bough_background.png";

    public static string GetPhaseBackgroundTexturePath(int phase) =>
        phase switch
        {
            2 => ForgottenTexturePath,
            3 => FlutteringTexturePath,
            4 => WaspTexturePath,
            5 => EmeraldBoughTexturePath,
            _ => PhaseOneTexturePath
        };

    // Called by the encounter right before a phase spawn; a failed node lookup must not skip it.
    public static TextureRect? GetCurrentBackgroundImage() =>
        PresentationGuard.Get(FindBackgroundImage, "HistoryFloorLiberation background lookup");

    public static void SetPhaseBackground(int phase)
    {
        string texturePath = GetPhaseBackgroundTexturePath(phase);
        TextureRect? image = FindBackgroundImage();
        if (image == null)
        {
            return;
        }

        Texture2D? texture = ResourceLoader.Load<Texture2D>(texturePath);
        if (texture != null)
        {
            image.Texture = texture;
        }
    }

    public static void PlayFlutteringPredationOverlay()
    {
        NCombatRoom? room = NCombatRoom.Instance;
        Control? container = room?.CombatVfxContainer;
        if (container == null)
        {
            return;
        }

        Texture2D? texture = ResourceLoader.Load<Texture2D>(FlutteringPredationOverlayTexturePath);
        if (texture == null)
        {
            return;
        }

        var overlay = new TextureRect
        {
            Name = "HistoryFloorFlutteringPredationOverlay",
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

    public static void PlayWaspBuffOverlay()
    {
        NCombatRoom? room = NCombatRoom.Instance;
        Control? container = room?.CombatVfxContainer;
        if (container == null)
        {
            return;
        }

        Texture2D? texture = ResourceLoader.Load<Texture2D>(WaspBuffOverlayTexturePath);
        if (texture == null)
        {
            return;
        }

        var overlay = new TextureRect
        {
            Name = "WaspBuffOverlay",
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

    public static void PlayWaspLoyaltyOverlay()
    {
        NCombatRoom? room = NCombatRoom.Instance;
        Control? container = room?.CombatVfxContainer;
        if (container == null)
        {
            return;
        }

        Texture2D? texture = ResourceLoader.Load<Texture2D>(WaspLoyaltyOverlayTexturePath);
        if (texture == null)
        {
            return;
        }

        var overlay = new TextureRect
        {
            Name = "WaspLoyaltyOverlay",
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

    private static TextureRect? FindBackgroundImage()
    {
        NCombatBackground? background = NCombatRoom.Instance?.Background;
        if (background == null)
        {
            return null;
        }

        return background.GetNodeOrNull<TextureRect>("%HistoryFloorLiberationBackgroundImage")
            ?? background.FindChild("HistoryFloorLiberationBackgroundImage", recursive: true, owned: false) as TextureRect;
    }
}
