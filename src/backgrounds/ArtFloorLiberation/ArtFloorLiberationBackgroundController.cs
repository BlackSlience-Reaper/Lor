using Godot;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace LibraryOfRuina.backgrounds.ArtFloorLiberation;

internal static class ArtFloorLiberationBackgroundController
{
    public const string PhaseOneTexturePath = "res://images/backgrounds/art_floor_liberation_encounter/background.png";
    public const string PhaseTwoTexturePath = "res://images/backgrounds/art_floor_liberation_encounter/beyond_fragment_background.png";
    public const string PhaseThreeTexturePath = "res://images/backgrounds/galaxy_child/background.png";
    public const string PhaseFourTexturePath = "res://images/backgrounds/spiny_bus_weak/background.png";
    public const string PhaseFiveTexturePath = "res://images/backgrounds/art_floor_liberation_encounter/nostalgic_scent_background.png";
    public const string GalaxyFilterNormalTexturePath = "res://images/backgrounds/galaxy_child/filter_normal.png";
    public const string GalaxyFilterFakeDeathTexturePath = "res://images/backgrounds/galaxy_child/filter_fake_death.png";
    public const string BackgroundTexturePath = PhaseOneTexturePath;
    public const string BackgroundScenePath = "res://scenes/backgrounds/art_floor_liberation_encounter/art_floor_liberation_encounter_background.tscn";

    private static int _currentPhase = 1;

    public static string GetPhaseBackgroundTexturePath(int phase) =>
        phase >= 6
            ? PhaseOneTexturePath
            : phase >= 5
            ? PhaseFiveTexturePath
            : phase >= 4
                ? PhaseFourTexturePath
                : phase >= 3
                ? PhaseThreeTexturePath
                : phase >= 2
                    ? PhaseTwoTexturePath
                    : PhaseOneTexturePath;

    public static TextureRect? GetCurrentBackgroundImage() =>
        FindBackgroundImage();

    public static void SetPhaseBackground(int phase)
    {
        _currentPhase = phase;
        TextureRect? backgroundImage = FindBackgroundImage();
        if (backgroundImage == null)
        {
            return;
        }

        string texturePath = GetPhaseBackgroundTexturePath(phase);
        Texture2D? texture = ResourceLoader.Load<Texture2D>(texturePath);
        if (texture != null)
        {
            backgroundImage.Texture = texture;
        }

        if (FindGalaxyFilterImage() is { } filterImage)
        {
            filterImage.Visible = phase == 3;
            if (phase == 3 && filterImage.Texture == null)
            {
                filterImage.Texture = ResourceLoader.Load<Texture2D>(
                    GalaxyFilterNormalTexturePath);
            }
        }
    }

    public static void SetGalaxyCryingMode(bool crying)
    {
        TextureRect? filterImage = FindGalaxyFilterImage();
        if (filterImage == null)
        {
            return;
        }

        string texturePath = crying ? GalaxyFilterFakeDeathTexturePath : GalaxyFilterNormalTexturePath;
        Texture2D? texture = ResourceLoader.Load<Texture2D>(texturePath);
        if (texture != null)
        {
            filterImage.Texture = texture;
        }

        filterImage.Visible = _currentPhase == 3;
    }

    private static TextureRect? FindBackgroundImage()
    {
        NCombatBackground? background = NCombatRoom.Instance?.Background;
        if (background == null)
        {
            return null;
        }

        return background.GetNodeOrNull<TextureRect>("%ArtFloorLiberationBackgroundImage")
            ?? background.FindChild("ArtFloorLiberationBackgroundImage", recursive: true, owned: false) as TextureRect;
    }

    private static TextureRect? FindGalaxyFilterImage()
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
