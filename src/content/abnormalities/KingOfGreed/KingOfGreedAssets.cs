namespace LibraryOfRuina.content.abnormalities.KingOfGreed;

// 本实体引用的 res:// 资源路径；tools/check_res_paths.py 按仓库文件核对（大小写敏感）。
// 保持 const：调用点编译期内联成同一个字面量，何时、怎样加载仍由调用点决定。
// 别处实体持有的路径引用对方的常量，不重复写值。
internal static class KingOfGreedAssets
{
    internal const string KingOfGreedSfxRoot = "res://audio/sfx/king_of_greed/";
    internal const string PlaceholderBossMapIconPrefix = "res://images/map/placeholder/king_of_greed_boss_icon";
    internal const string KingOfGreedMonsterRoot = "res://images/monsters/king_of_greed/";
    internal const string FlickeringDesirePowerIcon = "res://images/powers/library_of_ruina_flickering_desire_power.png";
    internal const string GluttonyPowerIcon = "res://images/powers/library_of_ruina_gluttony_power.png";
    internal const string GoldenAmberPowerIcon = "res://images/powers/library_of_ruina_golden_amber_power.png";
    internal const string PassivePowerIcon = "res://images/powers/library_of_ruina_king_of_greed_passive_power.png";
    internal const string MomentaryHappinessPowerIcon = "res://images/powers/library_of_ruina_momentary_happiness_power.png";
    internal const string SelfIntoxicationPowerIcon = "res://images/powers/library_of_ruina_self_intoxication_power.png";
    internal const string ShiningHappinessPowerIcon = "res://images/powers/library_of_ruina_shining_happiness_power.png";
    internal const string KingOfGreedScene = "res://scenes/creature_visuals/king_of_greed.tscn";
}
