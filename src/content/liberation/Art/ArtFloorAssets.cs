using LibraryOfRuina.content.abnormalities.GalaxyChild;

namespace LibraryOfRuina.content.liberation.Art;

// 本实体引用的 res:// 资源路径；tools/check_res_paths.py 按仓库文件核对（大小写敏感）。
// 保持 const：调用点编译期内联成同一个字面量，何时、怎样加载仍由调用点决定。
// 别处实体持有的路径引用对方的常量，不重复写值。
internal static class ArtFloorAssets
{
    internal const string NostalgicScentSfxRoot = "res://audio/sfx/art_floor/nostalgic_scent/";
    internal const string LiberationEncounterBackground = "res://images/backgrounds/art_floor_liberation_encounter/background.png";
    internal const string BeyondFragmentBackground = "res://images/backgrounds/art_floor_liberation_encounter/beyond_fragment_background.png";
    internal const string NostalgicScentBackground = "res://images/backgrounds/art_floor_liberation_encounter/nostalgic_scent_background.png";
    internal const string SpinyBusWeakBackground = "res://images/backgrounds/spiny_bus_weak/background.png";
    internal const string LiberationEncounterMapIconPrefix = "res://images/map/placeholder/art_floor_liberation_encounter_icon";
    internal const string ArtFloorDacapoMonsterRoot = "res://images/monsters/art_floor/dacapo/";
    internal const string DacapoPerformersMonsterRoot = "res://images/monsters/art_floor/dacapo_performers/";
    internal const string Performer1IdleTexture = "res://images/monsters/art_floor/dacapo_performers/performer_1_idle.png";
    internal const string FirstPerformerTexture = "res://images/monsters/art_floor/first_performer.png";
    internal const string LittleGalaxyMonsterRoot = "res://images/monsters/art_floor/little_galaxy/";
    internal const string NostalgicScentMonsterRoot = "res://images/monsters/art_floor/nostalgic_scent/";
    internal const string ArtFloorPleasureMonsterRoot = "res://images/monsters/art_floor/pleasure/";
    internal const string BeyondFragmentMonsterRoot = "res://images/monsters/beyond_fragment/";
    internal const string EverRepeatingPerformanceTexture = "res://images/packed/card_portraits/colorless/ever_repeating_performance.png";
    internal const string LiberationEncounterRunHistoryIcon = "res://images/ui/run_history/art_floor_liberation_encounter.png";
    internal const string LiberationEncounterOutlineRunHistoryIcon = "res://images/ui/run_history/art_floor_liberation_encounter_outline.png";
    internal const string LiberationEncounterBackgroundScene = "res://scenes/backgrounds/art_floor_liberation_encounter/art_floor_liberation_encounter_background.tscn";
    internal const string GalaxyChildBackground = GalaxyChildAssets.GalaxyChildBackground;
    internal const string FilterFakeDeathBackground = GalaxyChildAssets.FilterFakeDeathBackground;
    internal const string FilterNormalBackground = GalaxyChildAssets.FilterNormalBackground;
}
