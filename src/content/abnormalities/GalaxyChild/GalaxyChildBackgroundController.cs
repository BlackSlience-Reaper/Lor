using Godot;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.visuals;

namespace LibraryOfRuina.content.abnormalities.GalaxyChild;

internal static class GalaxyChildBackgroundController
{
    public const string BackgroundTexturePath = GalaxyChildAssets.GalaxyChildBackground;
    public const string NormalFilterTexturePath = GalaxyChildAssets.FilterNormalBackground;
    public const string FakeDeathFilterTexturePath = GalaxyChildAssets.FilterFakeDeathBackground;
    public const string CryLoopPath = GalaxyChildAssets.CryLoopSfx;

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

    private static void SetFilterTexture(string texturePath) =>
        CombatBackgroundImage.SetTexture(CombatBackgroundImage.Find("GalaxyChildFilterImage"), texturePath);
}
