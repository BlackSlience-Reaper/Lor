using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.infra.helpers;

namespace LibraryOfRuina.content.liberation.Literature;

internal static class LiteratureFloorLiberationBackgroundController
{
    public const string PhaseOneTexturePath =
        LiteratureFloorAssets.CreatureMapLatitiaCompositeBackground;

    public const string PhaseTwoTexturePath =
        LiteratureFloorAssets.SpiderBudStrongBackground;

    public const string PhaseThreeTexturePath =
        LiteratureFloorAssets.RedShoesBackground;

    public const string PhaseFourTexturePath =
        LiteratureFloorAssets.TodaysExpressionBackground;

    public const string PhaseFiveTexturePath =
        LiteratureFloorAssets.BlackSwanBackground;

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
