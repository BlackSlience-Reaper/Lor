using Godot;
using LibraryOfRuina.framework.audio;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace LibraryOfRuina.backgrounds.GalaxyChild;

internal static class GalaxyChildBackgroundController
{
    public const string BackgroundTexturePath = "res://images/backgrounds/galaxy_child/background.png";
    public const string NormalFilterTexturePath = "res://images/backgrounds/galaxy_child/filter_normal.png";
    public const string FakeDeathFilterTexturePath = "res://images/backgrounds/galaxy_child/filter_fake_death.png";
    public const string CryLoopPath = "res://audio/sfx/galaxy_child/cry_loop.ogg";

    private const string CryLoopSlot = "GalaxyChildCryLoop";

    public static IEnumerable<string> AssetPaths =>
    [
        BackgroundTexturePath,
        NormalFilterTexturePath,
        FakeDeathFilterTexturePath,
        CryLoopPath
    ];

    public static void SetFakeDeathMode(bool enabled)
    {
        SetFilterTexture(enabled ? FakeDeathFilterTexturePath : NormalFilterTexturePath);

        if (enabled)
        {
            LocalOggOneShotPlayer.PlayExclusive(CryLoopSlot, CryLoopPath, -3f);
        }
        else
        {
            LocalOggOneShotPlayer.StopExclusive(CryLoopSlot);
        }
    }

    public static void Reset()
    {
        SetFakeDeathMode(false);
    }

    private static void SetFilterTexture(string texturePath)
    {
        TextureRect? image = FindFilterImage();
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

    private static TextureRect? FindFilterImage()
    {
        NCombatBackground? background = NCombatRoom.Instance?.Background;
        if (background == null)
        {
            return null;
        }

        return background.GetNodeOrNull<TextureRect>("%GalaxyChildFilterImage")
            ?? background.FindChild("GalaxyChildFilterImage", recursive: true, owned: false) as TextureRect;
    }
}
