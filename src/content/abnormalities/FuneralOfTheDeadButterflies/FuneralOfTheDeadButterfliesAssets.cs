using LibraryOfRuina.content.abnormalities.DeadButterfly;
using LibraryOfRuina.framework.assets;

namespace LibraryOfRuina.content.abnormalities.FuneralOfTheDeadButterflies;

// 本实体引用的 res:// 资源路径；tools/check_res_paths.py 按仓库文件核对（大小写敏感）。
// 保持 const：调用点编译期内联成同一个字面量，何时、怎样加载仍由调用点决定。
// 别处实体持有的路径引用对方的常量，不重复写值。
internal static class FuneralOfTheDeadButterfliesAssets
{
    internal const string FuneralAttackBlackSfx = "res://audio/sfx/funeral_of_the_dead_butterflies/funeral_attack_black.ogg";
    internal const string FuneralAttackWhiteSfx = "res://audio/sfx/funeral_of_the_dead_butterflies/funeral_attack_white.ogg";
    internal const string FuneralParrySfx = "res://audio/sfx/funeral_of_the_dead_butterflies/funeral_parry.ogg";
    internal const string FuneralStrongPrepareSfx = "res://audio/sfx/funeral_of_the_dead_butterflies/funeral_strong_prepare.ogg";
    internal const string FuneralStunSfx = "res://audio/sfx/funeral_of_the_dead_butterflies/funeral_stun.ogg";
    internal const string CoffinAttackTexture = "res://images/monsters/funeral_of_the_dead_butterflies/coffin_attack.png";
    internal const string CoffinPrepareTexture = "res://images/monsters/funeral_of_the_dead_butterflies/coffin_prepare.png";
    internal const string FireBlackTexture = "res://images/monsters/funeral_of_the_dead_butterflies/fire_black.png";
    internal const string FireWhiteTexture = "res://images/monsters/funeral_of_the_dead_butterflies/fire_white.png";
    internal const string FuneralOfTheDeadButterfliesHitTexture = "res://images/monsters/funeral_of_the_dead_butterflies/hit.png";
    internal const string FuneralOfTheDeadButterfliesIdleTexture = "res://images/monsters/funeral_of_the_dead_butterflies/idle.png";
    internal const string FuneralWhiteFilterOverlayTexture = "res://images/vfx/funeral_white_filter_overlay.png";
    internal const string LiteratureReceptionFloor1Bgm = SharedAssets.LiteratureReceptionFloor1Bgm;
    internal const string FuneralBackground = DeadButterflyAssets.FuneralBackground;
}
