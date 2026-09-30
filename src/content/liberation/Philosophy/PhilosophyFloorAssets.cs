namespace LibraryOfRuina.content.liberation.Philosophy;

// 本实体引用的 res:// 资源路径；tools/check_res_paths.py 按仓库文件核对（大小写敏感）。
// 保持 const：调用点编译期内联成同一个字面量，何时、怎样加载仍由调用点决定。
// 别处实体持有的路径引用对方的常量，不重复写值。
internal static class PhilosophyFloorAssets
{
    internal const string PhilosophyFloorLiberationSfxRoot = "res://audio/sfx/philosophy_floor_liberation/";
    internal const string LiberationCgSfxRoot = "res://audio/sfx/philosophy_floor_liberation/cg/";
    internal const string EggBreakSfx = "res://audio/sfx/philosophy_floor_liberation/egg_break.ogg";
    internal const string LiberationEncounterMapIconPrefix = "res://images/map/placeholder/philosophy_floor_liberation_encounter_icon";
    internal const string TwilightDefaultTexture = "res://images/monsters/philosophy_floor_twilight/default.png";
    internal const string TwilightFTexture = "res://images/monsters/philosophy_floor_twilight/f.png";
    internal const string TwilightGuardTexture = "res://images/monsters/philosophy_floor_twilight/guard.png";
    internal const string TwilightHitTexture = "res://images/monsters/philosophy_floor_twilight/hit.png";
    internal const string TwilightJTexture = "res://images/monsters/philosophy_floor_twilight/j.png";
    internal const string TwilightS1Texture = "res://images/monsters/philosophy_floor_twilight/s1.png";
    internal const string TwilightS2Texture = "res://images/monsters/philosophy_floor_twilight/s2.png";
    internal const string TwilightS3Texture = "res://images/monsters/philosophy_floor_twilight/s3.png";
    internal const string TwilightS4Texture = "res://images/monsters/philosophy_floor_twilight/s4.png";
    internal const string TwilightS5Texture = "res://images/monsters/philosophy_floor_twilight/s5.png";
    internal const string TwilightZTexture = "res://images/monsters/philosophy_floor_twilight/z.png";
    internal const string ImagesPowersRoot = "res://images/powers/";
    internal const string LiberationEncounterRunHistoryIcon = "res://images/ui/run_history/philosophy_floor_liberation_encounter.png";
    internal const string LiberationEncounterOutlineRunHistoryIcon = "res://images/ui/run_history/philosophy_floor_liberation_encounter_outline.png";
    internal const string PhilosophyFloorLiberationVfxRoot = "res://images/vfx/philosophy_floor_liberation/";
    internal const string LiberationCgVfxRoot = "res://images/vfx/philosophy_floor_liberation/cg/";
    internal const string LiberationEncounterBg00ABackgroundScene = "res://scenes/backgrounds/philosophy_floor_liberation_encounter/layers/philosophy_floor_liberation_encounter_bg_00_a.tscn";
    internal const string LiberationEggAnimationsBackgroundScene = "res://scenes/backgrounds/philosophy_floor_liberation_encounter/philosophy_floor_liberation_egg_animations.tres";
    internal const string LiberationEncounterBackgroundScene = "res://scenes/backgrounds/philosophy_floor_liberation_encounter/philosophy_floor_liberation_encounter_background.tscn";
    internal const string LiberationEndBirdAnimationsBackgroundScene = "res://scenes/backgrounds/philosophy_floor_liberation_encounter/philosophy_floor_liberation_end_bird_animations.tres";
    internal const string TwilightAnimationsResource = "res://scenes/creature_visuals/philosophy_floor_twilight_animations.tres";
    internal const string LiberationEncounterScene = "res://scenes/encounters/philosophy_floor_liberation_encounter.tscn";
    internal const string LiberationCgScene = "res://scenes/vfx/philosophy_floor_liberation_cg.tscn";
    internal const string LiberationCgAnimationsResource = "res://scenes/vfx/philosophy_floor_liberation_cg_animations.tres";
}
