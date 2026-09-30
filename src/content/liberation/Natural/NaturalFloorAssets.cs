using LibraryOfRuina.content.abnormalities.DespairKnight;
using LibraryOfRuina.content.abnormalities.WrathServant;
using LibraryOfRuina.content.liberation.History;
using LibraryOfRuina.framework.assets;

namespace LibraryOfRuina.content.liberation.Natural;

// 本实体引用的 res:// 资源路径；tools/check_res_paths.py 按仓库文件核对（大小写敏感）。
// 保持 const：调用点编译期内联成同一个字面量，何时、怎样加载仍由调用点决定。
// 别处实体持有的路径引用对方的常量，不重复写值。
internal static class NaturalFloorAssets
{
    // 黄金狂与贪欲的能力图标：后面拼能力名和 "_power.png"。
    internal const string PowerIconPrefix = "res://images/powers/library_of_ruina_";

    internal const string BlindRageSfxRoot = "res://audio/sfx/natural_floor_liberation/blind_rage/";
    internal const string LiberationDespairSfxRoot = "res://audio/sfx/natural_floor_liberation/despair/";
    internal const string LoveAndHatredSfxRoot = "res://audio/sfx/natural_floor_liberation/love_and_hatred/";
    internal const string NihilFilterSfx = "res://audio/sfx/natural_floor_nihil/Nihil_Filter.ogg";
    internal const string KingOfGreedBackground = "res://images/backgrounds/king_of_greed/king_of_greed_background.png";
    internal const string LoveAndHatredHumanBackground = "res://images/backgrounds/natural_floor_liberation_encounter/love_and_hatred_human.png";
    internal const string LoveAndHatredSnakeBackground = "res://images/backgrounds/natural_floor_liberation_encounter/love_and_hatred_snake.png";
    internal const string LiberationEncounterNihilBackground = "res://images/backgrounds/natural_floor_liberation_encounter/nihil.png";
    internal const string LiberationEncounterMapIconPrefix = "res://images/map/placeholder/natural_floor_liberation_encounter_icon";
    internal const string HermitStaffAttackTexture = "res://images/monsters/hermit_staff/attack.png";
    internal const string HermitStaffHitTexture = "res://images/monsters/hermit_staff/hit.png";
    internal const string HermitStaffIdleTexture = "res://images/monsters/hermit_staff/idle.png";
    internal const string BlindRageHitTexture = "res://images/monsters/natural_floor_liberation/blind_rage/hit.png";
    internal const string BlindRageIdleTexture = "res://images/monsters/natural_floor_liberation/blind_rage/idle.png";
    internal const string BlindRageS1Texture = "res://images/monsters/natural_floor_liberation/blind_rage/s1.png";
    internal const string BlindRageS2Texture = "res://images/monsters/natural_floor_liberation/blind_rage/s2.png";
    internal const string BlindRageS3Texture = "res://images/monsters/natural_floor_liberation/blind_rage/s3.png";
    internal const string BlindRageSlashTexture = "res://images/monsters/natural_floor_liberation/blind_rage/slash.png";
    internal const string BlindRageStrikeTexture = "res://images/monsters/natural_floor_liberation/blind_rage/strike.png";
    internal const string BlindRageThrustTexture = "res://images/monsters/natural_floor_liberation/blind_rage/thrust.png";
    internal const string GreenStemHermitMentalTexture = "res://images/monsters/natural_floor_liberation/green_stem_hermit/mental.png";
    internal const string LoveAndHatredMonsterRoot = "res://images/monsters/natural_floor_liberation/love_and_hatred/";
    internal const string NihilEmptinessTexture = "res://images/packed/card_portraits/colorless/nihil_emptiness.png";
    internal const string HappinessShardTexture = "res://images/packed/card_portraits/status/happiness_shard.png";
    internal const string BadGuyPowerIcon = "res://images/powers/natural_floor_bad_guy_power.png";
    internal const string NaturalFloorNihilIcon = "res://images/powers/natural_floor_nihil.png";
    internal const string NihilHatredIcon = "res://images/powers/natural_floor_nihil_hatred.png";
    internal const string LiberationEncounterRunHistoryIcon = "res://images/ui/run_history/natural_floor_liberation_encounter.png";
    internal const string LiberationEncounterOutlineRunHistoryIcon = "res://images/ui/run_history/natural_floor_liberation_encounter_outline.png";
    internal const string BlindRageBossScene = "res://scenes/creature_visuals/natural_floor_blind_rage_boss.tscn";
    internal const string BlindRageBossAnimationsResource = "res://scenes/creature_visuals/natural_floor_blind_rage_boss_animations.tres";
    internal const string ForgottenSwordScene = "res://scenes/creature_visuals/natural_floor_forgotten_sword.tscn";
    internal const string GoldRushBossScene = "res://scenes/creature_visuals/natural_floor_gold_rush_boss.tscn";
    internal const string GreenStemHermitScene = "res://scenes/creature_visuals/natural_floor_green_stem_hermit.tscn";
    internal const string GreenStemHermitAnimationsResource = "res://scenes/creature_visuals/natural_floor_green_stem_hermit_animations.tres";
    internal const string HermitStaffScene = "res://scenes/creature_visuals/natural_floor_hermit_staff.tscn";
    internal const string HermitStaffAnimationsResource = "res://scenes/creature_visuals/natural_floor_hermit_staff_animations.tres";
    internal const string LoveAndHatredScenePrefix = "res://scenes/creature_visuals/natural_floor_love_and_hatred_";
    internal const string LoveAndHatredBossScene = "res://scenes/creature_visuals/natural_floor_love_and_hatred_boss.tscn";
    internal const string NaturalFloorNihilScenePrefix = "res://scenes/creature_visuals/natural_floor_nihil_";
    internal const string ShiningHappinessScene = "res://scenes/creature_visuals/natural_floor_shining_happiness.tscn";
    internal const string TearEdgeBossScene = "res://scenes/creature_visuals/natural_floor_tear_edge_boss.tscn";
    internal const string NihilAttackScene = "res://scenes/vfx/natural_floor_nihil_attack.tscn";
    internal const string NihilTransitionScene = "res://scenes/vfx/natural_floor_nihil_transition.tscn";
    internal const string LoveAndHatredInversionVideo = "res://videos/natural_floor_love_and_hatred_inversion.ogv";
    internal const string DespairKnightBackground = DespairKnightAssets.DespairKnightBackground;
    internal const string WrathServantStrongBackground = WrathServantAssets.StrongBackground;
    internal const string GreenStemHermitGroundTexture = WrathServantAssets.GreenStemHermitGroundTexture;
    internal const string GreenStemHermitHitTexture = WrathServantAssets.GreenStemHermitHitTexture;
    internal const string GreenStemHermitIdleTexture = WrathServantAssets.GreenStemHermitIdleTexture;
    internal const string GreenStemHermitReachTexture = WrathServantAssets.GreenStemHermitReachTexture;
    internal const string GreenStemHermitThrustTexture = WrathServantAssets.GreenStemHermitThrustTexture;
    internal const string ForgottenKnightSwordTeardropPowerIcon = DespairKnightAssets.ForgottenKnightSwordTeardropPowerIcon;
    internal const string HistoryFloorCorrosionPowerIcon = HistoryFloorAssets.CorrosionPowerIcon;
    internal const string LibraryPassiveGreenIcon = SharedAssets.LibraryPassiveGreenIcon;
}
