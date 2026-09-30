namespace LibraryOfRuina.content.abnormalities.DeadButterfly;

// 本实体引用的 res:// 资源路径；tools/check_res_paths.py 按仓库文件核对（大小写敏感）。
// 保持 const：调用点编译期内联成同一个字面量，何时、怎样加载仍由调用点决定。
// 别处实体持有的路径引用对方的常量，不重复写值。
internal static class DeadButterflyAssets
{
    internal const string DeadButterflyAttackSfx = "res://audio/sfx/dead_butterfly/dead_butterfly_attack.ogg";
    internal const string DeadButterflyDodgeSfx = "res://audio/sfx/dead_butterfly/dead_butterfly_dodge.ogg";
    internal const string FuneralBackground = "res://images/backgrounds/dead_butterfly/funeral_background.png";
    internal const string DeadButterflyAttackTexture = "res://images/monsters/dead_butterfly/attack.png";
    internal const string DeadButterflyHitTexture = "res://images/monsters/dead_butterfly/hit.png";
    internal const string DeadButterflyIdleTexture = "res://images/monsters/dead_butterfly/idle.png";
}
