using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.infra.helpers;

namespace LibraryOfRuina.content.liberation.Technology;

internal static class TechnologyFloorLiberationBackgroundController
{
    public const string PhaseOneTexturePath = "res://images/backgrounds/technology_floor_liberation_encounter/background_1.webp";
    public const string PhaseTwoTexturePath = "res://images/backgrounds/technology_floor_liberation_encounter/background_2.webp";
    public const string PhaseThreeTexturePath = "res://images/backgrounds/technology_floor_liberation_encounter/phase3_background.png";
    public const string PhaseFourTexturePath = "res://images/backgrounds/technology_floor_liberation_encounter/phase4_background.png";
    public const string PhaseFiveTexturePath = "res://images/backgrounds/technology_floor_liberation_encounter/phase5_background.png";

    public static string GetPhaseBackgroundTexturePath(int phase) =>
        phase switch
        {
            5 => PhaseFiveTexturePath,
            4 => PhaseFourTexturePath,
            3 => PhaseThreeTexturePath,
            2 => PhaseTwoTexturePath,
            _ => PhaseOneTexturePath
        };

    // Called by the encounter right before a phase spawn; a failed node lookup must not skip it.
    public static TextureRect? GetCurrentBackgroundImage() =>
        PresentationGuard.Get(FindBackgroundImage, "TechnologyFloorLiberation background lookup");

    public static void SetPhaseBackground(int phase) =>
        CombatBackgroundImage.SetTexture(FindBackgroundImage(), GetPhaseBackgroundTexturePath(phase));

    private static TextureRect? FindBackgroundImage() =>
        CombatBackgroundImage.Find("TechnologyFloorLiberationBackgroundImage");
}
