namespace LibraryOfRuina.content.liberation.Religion;

internal static class ReligionFloorAssets
{
    internal const string EncounterScene = "res://scenes/encounters/religion_floor_liberation_encounter.tscn";
    internal const string BackgroundScene = "res://scenes/backgrounds/religion_floor_liberation_encounter/religion_floor_liberation_encounter_background.tscn";
    internal const string MapIcon = "res://images/map/placeholder/religion_floor_liberation_encounter_icon";
    internal const string FirstMusic = "res://audio/bgm/religion_floor_liberation/phase_1.ogg";
    internal const string SecondMusic = "res://audio/bgm/religion_floor_liberation/phase_2.ogg";
    internal const string SalvationMusic = "res://audio/bgm/religion_floor_liberation/salvation.ogg";
    internal const string PresentationScene = "res://scenes/vfx/religion_floor_salvation.tscn";
    internal const string ClockScene = "res://scenes/vfx/religion_floor_apostle_clock.tscn";
    internal const string ClockTickSfx = "res://audio/sfx/religion_floor_liberation/WhiteNight_Clock_Tick.ogg";
    internal const string ClockBellSfx = "res://audio/sfx/religion_floor_liberation/WhiteNight_Clock_Bell.ogg";
    internal const string ScytheSlashSfx = "res://audio/sfx/religion_floor_liberation/WhiteNight_Apostle_Vert1.ogg";
    internal const string ScytheStrikeSfx = "res://audio/sfx/religion_floor_liberation/WhiteNight_Apostle_Vert2.ogg";
    internal const string SpearAttackSfx = "res://audio/sfx/religion_floor_liberation/WhiteNight_Apostle_Spear.ogg";
    internal const string StaffAttackSfx = "res://audio/sfx/religion_floor_liberation/WhiteNight_Apostle_Wand.ogg";
    internal const string ParadiseChargeSfx = "res://audio/sfx/religion_floor_liberation/WhiteNight_Strong_Charge.ogg";
    internal const string ParadiseFireSfx = "res://audio/sfx/religion_floor_liberation/WhiteNight_Strong_Fire.ogg";

    internal static IEnumerable<string> SoundPaths =>
    [
        ClockTickSfx, ClockBellSfx, ScytheSlashSfx, ScytheStrikeSfx,
        SpearAttackSfx, StaffAttackSfx, ParadiseChargeSfx, ParadiseFireSfx
    ];

    internal static string CreatureScene(string name) =>
        $"res://scenes/creature_visuals/religion_floor_{name}.tscn";
}
