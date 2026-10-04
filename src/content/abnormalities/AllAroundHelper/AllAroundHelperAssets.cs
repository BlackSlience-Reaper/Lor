namespace LibraryOfRuina.content.abnormalities.AllAroundHelper;

// 本实体引用的 res:// 资源路径；tools/check_res_paths.py 按仓库文件核对（大小写敏感）。
// 保持 const：调用点编译期内联成同一个字面量，何时、怎样加载仍由调用点决定。
// 别处实体持有的路径引用对方的常量，不重复写值。
internal static class AllAroundHelperAssets
{
    internal const string AllAroundHelperAttackSfx = "res://audio/sfx/all_around_helper/all_around_helper_attack.ogg";
    internal const string AllAroundHelperMonsterPrefix = "res://images/monsters/all_around_helper";
    internal const string AllAroundHelperTexture = "res://images/monsters/all_around_helper.webp";
    internal const string AllAroundHelperAttackTexture = "res://images/monsters/all_around_helper_attack.webp";
    internal const string AllAroundHelperHitTexture = "res://images/monsters/all_around_helper_hit.webp";
    // Spine 身体（tools/spine_from_sprite 生成）：以原始文件打进 PCK（export_presets 的 include_filter），运行时按路径加载。
    // 图集第一页直接引用上面的待机贴图，第二页是从攻击贴图抠出的刀光圈。
    internal const string AllAroundHelperSpineAtlas = "res://images/monsters/all_around_helper.atlas";
    internal const string AllAroundHelperSpineSkeleton = "res://images/monsters/all_around_helper.spine-json";
    internal const string AllAroundHelperSpineFxTexture = "res://images/monsters/all_around_helper_spine_fx.png";
}
