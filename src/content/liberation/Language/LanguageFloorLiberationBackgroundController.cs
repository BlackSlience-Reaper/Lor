using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.infra.helpers;

namespace LibraryOfRuina.content.liberation.Language;

internal static class LanguageFloorLiberationBackgroundController
{
    public const string NormalTexturePath =
        LanguageFloorAssets.Background1;

    public const string RageTexturePath =
        LanguageFloorAssets.Background2;

    public const string PhaseThreeTexturePath =
        LanguageFloorAssets.Background3;

    public const string PhaseFourTexturePath =
        LanguageFloorAssets.NosferatuBackground;

    public const string PhaseFiveTexturePath =
        LanguageFloorAssets.Background5;

    public static string GetPhaseBackgroundTexturePath(int phase) =>
        phase >= 5
            ? PhaseFiveTexturePath
            : phase == 4
            ? PhaseFourTexturePath
            : phase == 3
            ? PhaseThreeTexturePath
            : phase == 2
                ? RageTexturePath
                : NormalTexturePath;

    public static void SetRageBackground(bool isRage)
    {
        SetBackground(isRage ? RageTexturePath : NormalTexturePath);
    }

    public static void SetPhaseBackground(int phase)
    {
        SetBackground(GetPhaseBackgroundTexturePath(phase));
    }

    public static void SetPhaseThreeBackground()
    {
        SetPhaseBackground(3);
    }

    // Called by the encounter right before a phase spawn; a failed node lookup must not skip it.
    public static TextureRect? GetCurrentBackgroundImage() =>
        PresentationGuard.Get(FindCurrentBackgroundImage, "LanguageFloorLiberation background lookup");

    private static TextureRect? FindCurrentBackgroundImage() =>
        CombatBackgroundImage.Find("LittleRedMercenaryBackgroundImage");

    // 与其他楼层不同，这里换图也走带保护的查找。
    private static void SetBackground(string texturePath) =>
        CombatBackgroundImage.SetTexture(GetCurrentBackgroundImage(), texturePath);
}
