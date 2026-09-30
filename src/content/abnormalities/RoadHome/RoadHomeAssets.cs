namespace LibraryOfRuina.content.abnormalities.RoadHome;

// 本实体引用的 res:// 资源路径；tools/check_res_paths.py 按仓库文件核对（大小写敏感）。
// 保持 const：调用点编译期内联成同一个字面量，何时、怎样加载仍由调用点决定。
// 别处实体持有的路径引用对方的常量，不重复写值。
internal static class RoadHomeAssets
{
    internal const string RoadHomeSfxRoot = "res://audio/sfx/road_home/";
    internal const string EliteBackground = "res://images/backgrounds/road_home_elite/background.png";
    internal const string RoadHomeMonsterRoot = "res://images/monsters/road_home/";
    internal const string ScaredyCatMonsterRoot = "res://images/monsters/scaredy_cat/";
    internal const string CompanionRoadChoiceCardTexture = "res://images/packed/card_portraits/colorless/road_home_companion_road_choice_card.png";
    internal const string CourageChoiceCardTexture = "res://images/packed/card_portraits/colorless/road_home_courage_choice_card.png";
    internal const string HomeChoiceCardTexture = "res://images/packed/card_portraits/colorless/road_home_home_choice_card.png";
    internal const string BadWizardPassivePowerIcon = "res://images/powers/road_home_bad_wizard_passive_power.png";
    internal const string FriendPassivePowerIcon = "res://images/powers/road_home_friend_passive_power.png";
    internal const string HouseProtectionPowerIcon = "res://images/powers/road_home_house_protection_power.png";
    internal const string ScaredyCatCompanionCowardPowerIcon = "res://images/powers/scaredy_cat_companion_coward_power.png";
    internal const string ScaredyCatCouragePowerIcon = "res://images/powers/scaredy_cat_courage_power.png";
    internal const string ScaredyCatCowardPowerIcon = "res://images/powers/scaredy_cat_coward_power.png";
    internal const string PageRelicTexture = "res://images/relics/road_home_page_relic.png";
    internal const string EliteBg00ABackgroundScene = "res://scenes/backgrounds/road_home_elite/layers/road_home_elite_bg_00_a.tscn";
    internal const string EliteBackgroundScene = "res://scenes/backgrounds/road_home_elite/road_home_elite_background.tscn";
    internal const string RoadHomeEliteEncounterScene = "res://scenes/encounters/road_home_elite.tscn";
}
