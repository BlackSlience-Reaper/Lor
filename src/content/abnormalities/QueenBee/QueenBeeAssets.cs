namespace LibraryOfRuina.content.abnormalities.QueenBee;

// 本实体引用的 res:// 资源路径；tools/check_res_paths.py 按仓库文件核对（大小写敏感）。
// 保持 const：调用点编译期内联成同一个字面量，何时、怎样加载仍由调用点决定。
// 别处实体持有的路径引用对方的常量，不重复写值。
internal static class QueenBeeAssets
{
    internal const string QueenBuffSfx = "res://audio/sfx/queen_bee/queen_buff.ogg";
    internal const string QueenEvasionSfx = "res://audio/sfx/queen_bee/queen_evasion.ogg";
    internal const string QueenSpawnSfx = "res://audio/sfx/queen_bee/queen_spawn.ogg";
    internal const string QueenSporeSfx = "res://audio/sfx/queen_bee/queen_spore.ogg";
    internal const string QueenBeeMonsterRoot = "res://images/monsters/queen_bee/";
    internal const string EliteBg00ABackgroundScene = "res://scenes/backgrounds/queen_bee_elite/layers/queen_bee_elite_bg_00_a.tscn";
    internal const string QueenBeeEliteEncounterScene = "res://scenes/encounters/queen_bee_elite.tscn";
}
