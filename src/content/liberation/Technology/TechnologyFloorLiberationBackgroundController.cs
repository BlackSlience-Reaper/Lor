using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.infra.helpers;

namespace LibraryOfRuina.content.liberation.Technology;

internal static class TechnologyFloorLiberationBackgroundController
{
    public const string PhaseOneTexturePath = TechnologyFloorAssets.Background1;
    public const string PhaseTwoTexturePath = TechnologyFloorAssets.Background2;
    public const string PhaseThreeTexturePath = TechnologyFloorAssets.Phase3Background;
    public const string PhaseFourTexturePath = TechnologyFloorAssets.Phase4Background;
    public const string PhaseFiveTexturePath = TechnologyFloorAssets.Phase5Background;

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
