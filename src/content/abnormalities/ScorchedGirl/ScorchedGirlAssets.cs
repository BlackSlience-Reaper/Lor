namespace LibraryOfRuina.content.abnormalities.ScorchedGirl;

// 本实体引用的 res:// 资源路径；tools/check_res_paths.py 按仓库文件核对（大小写敏感）。
// 保持 const：调用点编译期内联成同一个字面量，何时、怎样加载仍由调用点决定。
// 别处实体持有的路径引用对方的常量，不重复写值。
internal static class ScorchedGirlAssets
{
    internal const string FourthMatchFlameAttackSfx = "res://audio/sfx/scorched_girl/fourth_match_flame_attack.ogg";
    internal const string ScorchedGirlExplosionSfx = "res://audio/sfx/scorched_girl/scorched_girl_explosion.ogg";
    internal const string ScorchedGirlMonsterPrefix = "res://images/monsters/scorched_girl_monster";
}
