namespace LibraryOfRuina.content.abnormalities.Leticia;

// 本实体引用的 res:// 资源路径；tools/check_res_paths.py 按仓库文件核对（大小写敏感）。
// 保持 const：调用点编译期内联成同一个字面量，何时、怎样加载仍由调用点决定。
// 别处实体持有的路径引用对方的常量，不重复写值。
internal static class LeticiaAssets
{
    internal const string FriendHitSfx = "res://audio/sfx/leticia/friend_hit.ogg";
    internal const string FriendPierceSfx = "res://audio/sfx/leticia/friend_pierce.ogg";
    internal const string FriendSpawnSfx = "res://audio/sfx/leticia/friend_spawn.ogg";
    internal const string GiftCloseSfx = "res://audio/sfx/leticia/gift_close.ogg";
    internal const string GiftOpenSfx = "res://audio/sfx/leticia/gift_open.ogg";
    internal const string LeticiaAttackSfx = "res://audio/sfx/leticia/leticia_attack.ogg";
    internal const string LeticiaGuardSfx = "res://audio/sfx/leticia/leticia_guard.ogg";
    internal const string LeticiaLeticiaMonsterPrefix = "res://images/monsters/leticia/leticia_";
    internal const string LittleWitchFriendMonsterPrefix = "res://images/monsters/leticia/little_witch_friend_";
    internal const string SurpriseGiftBoxMonsterPrefix = "res://images/monsters/leticia/surprise_gift_box_";
    internal const string Filter1Texture = "res://images/vfx/leticia_filter_1.png";
    internal const string Filter2Texture = "res://images/vfx/leticia_filter_2.png";
}
