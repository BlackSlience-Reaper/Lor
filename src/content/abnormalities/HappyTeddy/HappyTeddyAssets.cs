namespace LibraryOfRuina.content.abnormalities.HappyTeddy;

// 本实体引用的 res:// 资源路径；tools/check_res_paths.py 按仓库文件核对（大小写敏感）。
// 保持 const：调用点编译期内联成同一个字面量，何时、怎样加载仍由调用点决定。
// 别处实体持有的路径引用对方的常量，不重复写值。
internal static class HappyTeddyAssets
{
    internal const string ScorchedGirlBattle1Bgm = "res://audio/bgm/scorched_girl/scorched_girl_battle_1.ogg";
    internal const string ScorchedGirlBattle2Bgm = "res://audio/bgm/scorched_girl/scorched_girl_battle_2.ogg";
    internal const string ScorchedGirlBattle3Bgm = "res://audio/bgm/scorched_girl/scorched_girl_battle_3.ogg";
    internal const string HappyTeddyEmbraceSfx = "res://audio/sfx/happy_teddy/happy_teddy_embrace.ogg";
    internal const string MusicBoxSfx = "res://audio/sfx/happy_teddy/happy_teddy_music_box.ogg";
    internal const string NormalAttackSfx = "res://audio/sfx/happy_teddy/happy_teddy_normal_attack.ogg";
    internal const string HappyTeddyMonsterPrefix = "res://images/monsters/happy_teddy";
    // Spine 身体（tools/spine_from_sprite 生成）：骨骼与图集以原始文件打进 PCK，运行时按路径加载
    internal const string HappyTeddySpineAtlas = "res://images/monsters/happy_teddy.atlas";
    internal const string HappyTeddySpineSkeleton = "res://images/monsters/happy_teddy.spine-json";
}
