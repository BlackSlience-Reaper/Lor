namespace LibraryOfRuina.content.abnormalities.GalaxyChild;

// 本实体引用的 res:// 资源路径；tools/check_res_paths.py 按仓库文件核对（大小写敏感）。
// 保持 const：调用点编译期内联成同一个字面量，何时、怎样加载仍由调用点决定。
// 别处实体持有的路径引用对方的常量，不重复写值。
internal static class GalaxyChildAssets
{
    internal const string GalaxyChildAttackSfx = "res://audio/sfx/galaxy_child/attack.ogg";
    internal const string CryLoopSfx = "res://audio/sfx/galaxy_child/cry_loop.ogg";
    internal const string GalaxyChildHealSfx = "res://audio/sfx/galaxy_child/heal.ogg";
    internal const string GalaxyChildParrySfx = "res://audio/sfx/galaxy_child/parry.ogg";
    internal const string GalaxyChildBackground = "res://images/backgrounds/galaxy_child/background.png";
    internal const string FilterFakeDeathBackground = "res://images/backgrounds/galaxy_child/filter_fake_death.png";
    internal const string FilterNormalBackground = "res://images/backgrounds/galaxy_child/filter_normal.png";
    internal const string GalaxyFriendAttackTexture = "res://images/monsters/galaxy_friend/attack.png";
    internal const string GalaxyFriendHitTexture = "res://images/monsters/galaxy_friend/hit.png";
    internal const string GalaxyFriendIdleTexture = "res://images/monsters/galaxy_friend/idle.png";
    internal const string GalaxyFriendParryTexture = "res://images/monsters/galaxy_friend/parry.png";
}
