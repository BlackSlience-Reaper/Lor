namespace LibraryOfRuina.content.abnormalities.Nosferatu;

// 本实体引用的 res:// 资源路径；tools/check_res_paths.py 按仓库文件核对（大小写敏感）。
// 保持 const：调用点编译期内联成同一个字面量，何时、怎样加载仍由调用点决定。
// 别处实体持有的路径引用对方的常量，不重复写值。
internal static class NosferatuAssets
{
    internal const string NosferatuSfxRoot = "res://audio/sfx/nosferatu/";
    internal const string NosferatuMonsterRoot = "res://images/monsters/nosferatu/";
    internal const string BloodPowerIcon = "res://images/powers/nosferatu_blood_power.png";
    internal const string FlowingBloodPowerIcon = "res://images/powers/nosferatu_flowing_blood_power.png";
    internal const string HydrophobiaPassivePowerIcon = "res://images/powers/nosferatu_hydrophobia_passive_power.png";
    internal const string HydrophobiaPowerIcon = "res://images/powers/nosferatu_hydrophobia_power.png";
    internal const string TransformPowerIcon = "res://images/powers/nosferatu_transform_power.png";
}
