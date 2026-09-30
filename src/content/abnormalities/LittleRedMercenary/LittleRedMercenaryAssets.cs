namespace LibraryOfRuina.content.abnormalities.LittleRedMercenary;

// 本实体引用的 res:// 资源路径；tools/check_res_paths.py 按仓库文件核对（大小写敏感）。
// 保持 const：调用点编译期内联成同一个字面量，何时、怎样加载仍由调用点决定。
// 别处实体持有的路径引用对方的常量，不重复写值。
internal static class LittleRedMercenaryAssets
{
    internal const string LittleRedMercenarySfxRoot = "res://audio/sfx/little_red_mercenary/";
    internal const string Background1 = "res://images/backgrounds/little_red_mercenary_elite/background_1.png";
    internal const string Background2 = "res://images/backgrounds/little_red_mercenary_elite/background_2.png";
    internal const string LittleRedMercenaryMonsterRoot = "res://images/monsters/little_red_mercenary/";
    internal const string WolfInHerNightmaresScene = "res://scenes/creature_visuals/wolf_in_her_nightmares.tscn";
}
