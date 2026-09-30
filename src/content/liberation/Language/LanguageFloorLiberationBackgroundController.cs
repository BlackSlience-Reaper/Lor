using Godot;
using LibraryOfRuina.framework.visuals;
using LibraryOfRuina.infra.helpers;

namespace LibraryOfRuina.content.liberation.Language;

internal static class LanguageFloorLiberationBackgroundController
{
    public const string NormalTexturePath =
        "res://images/backgrounds/language_floor_liberation_encounter/background_1.png";

    public const string RageTexturePath =
        "res://images/backgrounds/language_floor_liberation_encounter/background_2.png";

    public const string PhaseThreeTexturePath =
        "res://images/backgrounds/language_floor_liberation_encounter/background_3.png";

    public const string PhaseFourTexturePath =
        "res://images/backgrounds/nosferatu_elite/nosferatu_background.png";

    public const string PhaseFiveTexturePath =
        "res://images/backgrounds/language_floor_liberation_encounter/background_5.png";

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
