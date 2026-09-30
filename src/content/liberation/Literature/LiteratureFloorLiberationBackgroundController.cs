using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.infra.helpers;

namespace LibraryOfRuina.content.liberation.Literature;

internal static class LiteratureFloorLiberationBackgroundController
{
    public const string PhaseOneTexturePath =
        "res://images/backgrounds/literature_floor_liberation_encounter/creature_map_latitia_composite.png";

    public const string PhaseTwoTexturePath =
        "res://images/backgrounds/spider_bud_strong/spider_bud_strong_background.png";

    public const string PhaseThreeTexturePath =
        "res://images/backgrounds/red_shoes_strong/red_shoes_background.png";

    public const string PhaseFourTexturePath =
        "res://images/backgrounds/literature_floor_liberation_encounter/todays_expression_background.png";

    public const string PhaseFiveTexturePath =
        "res://images/backgrounds/literature_floor_liberation_encounter/black_swan_background.png";

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
        PresentationGuard.Get(FindBackgroundImage, "LiteratureFloorLiberation background lookup");

    public static void SetPhaseBackground(int phase) =>
        CombatBackgroundImage.SetTexture(FindBackgroundImage(), GetPhaseBackgroundTexturePath(phase));

    private static TextureRect? FindBackgroundImage() =>
        CombatBackgroundImage.Find("LiteratureFloorLiberationBackgroundImage");
}
