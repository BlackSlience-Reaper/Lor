namespace LibraryOfRuina.content.abnormalities.SmilingBodies;

// 本实体引用的 res:// 资源路径；tools/check_res_paths.py 按仓库文件核对（大小写敏感）。
// 保持 const：调用点编译期内联成同一个字面量，何时、怎样加载仍由调用点决定。
// 别处实体持有的路径引用对方的常量，不重复写值。
internal static class SmilingBodiesAssets
{
    internal const string SmilingBodiesSfxRoot = "res://audio/sfx/smiling_bodies/";
    internal const string StrongBackground = "res://images/backgrounds/smiling_bodies_strong/background.png";
    internal const string SmilingBodiesMonsterRoot = "res://images/monsters/smiling_bodies/";
    internal const string DissolvingCorpsesPowerIcon = "res://images/powers/smiling_bodies_dissolving_corpses_power.png";
    internal const string FindCorpsesPowerIcon = "res://images/powers/smiling_bodies_find_corpses_power.png";
    internal const string FusionPowerIcon = "res://images/powers/smiling_bodies_fusion_power.png";
    internal const string ScreamPowerIcon = "res://images/powers/smiling_bodies_scream_power.png";
    internal const string SplitAndFusionPowerIcon = "res://images/powers/smiling_bodies_split_and_fusion_power.png";
    internal const string SplitPowerIcon = "res://images/powers/smiling_bodies_split_power.png";
    internal const string VomitPowerIcon = "res://images/powers/smiling_bodies_vomit_power.png";
    internal const string SmilingBodiesScene = "res://scenes/creature_visuals/smiling_bodies.tscn";
}
