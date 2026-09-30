using LibraryOfRuina.content.abnormalities.Leticia;
using LibraryOfRuina.content.abnormalities.SpiderBud;
using LibraryOfRuina.content.liberation.History;
using LibraryOfRuina.framework.assets;

namespace LibraryOfRuina.content.liberation.Literature;

// 本实体引用的 res:// 资源路径；tools/check_res_paths.py 按仓库文件核对（大小写敏感）。
// 保持 const：调用点编译期内联成同一个字面量，何时、怎样加载仍由调用点决定。
// 别处实体持有的路径引用对方的常量，不重复写值。
internal static class LiteratureFloorAssets
{
    internal const string BlackSwanSfxRoot = "res://audio/sfx/literature_floor_liberation/black_swan/";
    internal const string LiberationBloodlustSfxRoot = "res://audio/sfx/literature_floor_liberation/bloodlust/";
    internal const string LaetitiaStrongAttackSfx = "res://audio/sfx/literature_floor_liberation/laetitia_strong_attack.ogg";
    internal const string LaetitiaStrongChargeSfx = "res://audio/sfx/literature_floor_liberation/laetitia_strong_charge.ogg";
    internal const string RedEyesSfxRoot = "res://audio/sfx/literature_floor_liberation/red_eyes/";
    internal const string TodaysExpressionSfxRoot = "res://audio/sfx/literature_floor_liberation/todays_expression/";
    internal const string BlackSwanBackground = "res://images/backgrounds/literature_floor_liberation_encounter/black_swan_background.png";
    internal const string CreatureMapLatitiaCompositeBackground = "res://images/backgrounds/literature_floor_liberation_encounter/creature_map_latitia_composite.png";
    internal const string TodaysExpressionBackground = "res://images/backgrounds/literature_floor_liberation_encounter/todays_expression_background.png";
    internal const string RedShoesBackground = "res://images/backgrounds/red_shoes_strong/red_shoes_background.png";
    internal const string LiberationEncounterMapIconPrefix = "res://images/map/placeholder/literature_floor_liberation_encounter_icon";
    internal const string BlackSwanMonsterRoot = "res://images/monsters/literature_floor_liberation/black_swan/";
    internal const string BlackSwanBrothersMonsterRoot = "res://images/monsters/literature_floor_liberation/black_swan_brothers/";
    internal const string Brother1IdleTexture = "res://images/monsters/literature_floor_liberation/black_swan_brothers/brother_1_idle.png";
    internal const string Brother2IdleTexture = "res://images/monsters/literature_floor_liberation/black_swan_brothers/brother_2_idle.png";
    internal const string Brother3IdleTexture = "res://images/monsters/literature_floor_liberation/black_swan_brothers/brother_3_idle.png";
    internal const string Brother4IdleTexture = "res://images/monsters/literature_floor_liberation/black_swan_brothers/brother_4_idle.png";
    internal const string Brother5IdleTexture = "res://images/monsters/literature_floor_liberation/black_swan_brothers/brother_5_idle.png";
    internal const string Brother6IdleTexture = "res://images/monsters/literature_floor_liberation/black_swan_brothers/brother_6_idle.png";
    internal const string LiberationBloodlustMonsterRoot = "res://images/monsters/literature_floor_liberation/bloodlust/";
    internal const string EnhancedSmallSpiderMonsterRoot = "res://images/monsters/literature_floor_liberation/enhanced_small_spider/";
    internal const string RedEyesMonsterRoot = "res://images/monsters/literature_floor_liberation/red_eyes/";
    internal const string TodaysExpressionMonsterRoot = "res://images/monsters/literature_floor_liberation/todays_expression/";
    internal const string BlackSwanVanishingFamilyPowerIcon = "res://images/powers/literature_floor_black_swan_vanishing_family_power.png";
    internal const string CocoonBindPowerIcon = "res://images/powers/literature_floor_cocoon_bind_power.png";
    internal const string DeepWoundPowerIcon = "res://images/powers/literature_floor_deep_wound_power.png";
    internal const string LiberationEncounterRunHistoryIcon = "res://images/ui/run_history/literature_floor_liberation_encounter.png";
    internal const string LiberationEncounterOutlineRunHistoryIcon = "res://images/ui/run_history/literature_floor_liberation_encounter_outline.png";
    internal const string Face1Texture = "res://images/vfx/literature_floor_liberation/todays_expression/face_1.png";
    internal const string Face2Texture = "res://images/vfx/literature_floor_liberation/todays_expression/face_2.png";
    internal const string Face3Texture = "res://images/vfx/literature_floor_liberation/todays_expression/face_3.png";
    internal const string Face4Texture = "res://images/vfx/literature_floor_liberation/todays_expression/face_4.png";
    internal const string Face5Texture = "res://images/vfx/literature_floor_liberation/todays_expression/face_5.png";
    internal const string LiberationEncounterBg00ABackgroundScene = "res://scenes/backgrounds/literature_floor_liberation_encounter/layers/literature_floor_liberation_encounter_bg_00_a.tscn";
    internal const string LiberationEncounterBackgroundScene = "res://scenes/backgrounds/literature_floor_liberation_encounter/literature_floor_liberation_encounter_background.tscn";
    internal const string BlackSwanBossScene = "res://scenes/creature_visuals/literature_floor_black_swan_boss.tscn";
    internal const string BlackSwanBrotherScene = "res://scenes/creature_visuals/literature_floor_black_swan_brother.tscn";
    internal const string BloodlustBossScene = "res://scenes/creature_visuals/literature_floor_bloodlust_boss.tscn";
    internal const string EnhancedSmallSpiderScene = "res://scenes/creature_visuals/literature_floor_enhanced_small_spider.tscn";
    internal const string LaetitiaBossScene = "res://scenes/creature_visuals/literature_floor_laetitia_boss.tscn";
    internal const string LittleWitchFriendScene = "res://scenes/creature_visuals/literature_floor_little_witch_friend.tscn";
    internal const string RedEyesBossScene = "res://scenes/creature_visuals/literature_floor_red_eyes_boss.tscn";
    internal const string SurpriseGiftBoxScene = "res://scenes/creature_visuals/literature_floor_surprise_gift_box.tscn";
    internal const string TodaysExpressionBossScene = "res://scenes/creature_visuals/literature_floor_todays_expression_boss.tscn";
    internal const string LiberationBlackSwanEncounterScene = "res://scenes/encounters/literature_floor_liberation_black_swan_encounter.tscn";
    internal const string LiberationBloodlustEncounterScene = "res://scenes/encounters/literature_floor_liberation_bloodlust_encounter.tscn";
    internal const string LiberationEncounterScene = "res://scenes/encounters/literature_floor_liberation_encounter.tscn";
    internal const string LiberationRedEyesEncounterScene = "res://scenes/encounters/literature_floor_liberation_red_eyes_encounter.tscn";
    internal const string LiberationTodaysExpressionEncounterScene = "res://scenes/encounters/literature_floor_liberation_todays_expression_encounter.tscn";
    internal const string FriendHitSfx = LeticiaAssets.FriendHitSfx;
    internal const string FriendPierceSfx = LeticiaAssets.FriendPierceSfx;
    internal const string GiftCloseSfx = LeticiaAssets.GiftCloseSfx;
    internal const string GiftOpenSfx = LeticiaAssets.GiftOpenSfx;
    internal const string LeticiaAttackSfx = LeticiaAssets.LeticiaAttackSfx;
    internal const string LeticiaGuardSfx = LeticiaAssets.LeticiaGuardSfx;
    internal const string SpiderBudStrongBackground = SpiderBudAssets.StrongBackground;
    internal const string HistoryFloorCorrosionPowerIcon = HistoryFloorAssets.CorrosionPowerIcon;
    internal const string LibraryPassiveGreenIcon = SharedAssets.LibraryPassiveGreenIcon;
    internal const string SpiderBudUntargetablePowerIcon = SharedAssets.SpiderBudUntargetablePowerIcon;
}
