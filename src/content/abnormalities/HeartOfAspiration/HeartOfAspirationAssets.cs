namespace LibraryOfRuina.content.abnormalities.HeartOfAspiration;

// 本实体引用的 res:// 资源路径；tools/check_res_paths.py 按仓库文件核对（大小写敏感）。
// 保持 const：调用点编译期内联成同一个字面量，何时、怎样加载仍由调用点决定。
// 别处实体持有的路径引用对方的常量，不重复写值。
internal static class HeartOfAspirationAssets
{
    internal const string HeartAttackSfx = "res://audio/sfx/heart_of_aspiration/heart_attack.ogg";
    internal const string LungAttackSfx = "res://audio/sfx/heart_of_aspiration/lung_attack.ogg";
    internal const string HeartOfAspirationMonsterRoot = "res://images/monsters/heart_of_aspiration/";
    internal const string LungOfAspirationMonsterRoot = "res://images/monsters/lung_of_aspiration/";
    internal const string DesirePassivePowerIcon = "res://images/powers/heart_of_aspiration_desire_passive_power.png";
    internal const string LungOfAspirationDesirePassivePowerIcon = "res://images/powers/lung_of_aspiration_desire_passive_power.png";
}
