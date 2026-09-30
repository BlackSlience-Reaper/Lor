namespace LibraryOfRuina.content.abnormalities.DespairKnight;

// 本实体引用的 res:// 资源路径；tools/check_res_paths.py 按仓库文件核对（大小写敏感）。
// 保持 const：调用点编译期内联成同一个字面量，何时、怎样加载仍由调用点决定。
// 别处实体持有的路径引用对方的常量，不重复写值。
internal static class DespairKnightAssets
{
    internal const string DespairKnightSfxRoot = "res://audio/sfx/despair_knight/";
    internal const string DespairKnightBackground = "res://images/backgrounds/despair_knight_strong/despair_knight_background.png";
    internal const string DespairKnightMonsterRoot = "res://images/monsters/despair_knight/";
    internal const string ForgottenKnightSwordMonsterRoot = "res://images/monsters/forgotten_knight_sword/";
    internal const string BrokenHeartPowerIcon = "res://images/powers/despair_knight_broken_heart_power.png";
    internal const string DespairPowerIcon = "res://images/powers/despair_knight_despair_power.png";
    internal const string ProtectionPowerIcon = "res://images/powers/despair_knight_protection_power.png";
    internal const string SorrowPowerIcon = "res://images/powers/despair_knight_sorrow_power.png";
    internal const string ForgottenKnightSwordFalseDeathPowerIcon = "res://images/powers/forgotten_knight_sword_false_death_power.png";
    internal const string ForgottenKnightSwordPierceDespairPowerIcon = "res://images/powers/forgotten_knight_sword_pierce_despair_power.png";
    internal const string ForgottenKnightSwordTeardropPowerIcon = "res://images/powers/forgotten_knight_sword_teardrop_power.png";
    internal const string PierceDespairVfxPrefix = "res://images/vfx/despair_knight_pierce_despair";
    internal const string StrongBackgroundScene = "res://scenes/backgrounds/despair_knight_strong/despair_knight_strong_background.tscn";
    internal const string StrongBg00ABackgroundScene = "res://scenes/backgrounds/despair_knight_strong/layers/despair_knight_strong_bg_00_a.tscn";
}
