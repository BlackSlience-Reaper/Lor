namespace LibraryOfRuina.content.liberation.Social;

// 本实体引用的 res:// 资源路径；tools/check_res_paths.py 按仓库文件核对（大小写敏感）。
// 保持 const：调用点编译期内联成同一个字面量，何时、怎样加载仍由调用点决定。
// 别处实体持有的路径引用对方的常量，不重复写值。
internal static class SocialFloorAssets
{
    internal const string AttackBoomSfx = "res://audio/sfx/social_floor_liberation/attack_boom.ogg";
    internal const string AttackUpSfx = "res://audio/sfx/social_floor_liberation/attack_up.ogg";
    internal const string CardMagicSfx = "res://audio/sfx/social_floor_liberation/card_magic.ogg";
    internal const string ChangeMagicSfx = "res://audio/sfx/social_floor_liberation/change_magic.ogg";
    internal const string StrongAttackDownSfx = "res://audio/sfx/social_floor_liberation/strong_attack_down.ogg";
    internal const string StrongAttackFinishSfx = "res://audio/sfx/social_floor_liberation/strong_attack_finish.ogg";
    internal const string StrongAttackStartSfx = "res://audio/sfx/social_floor_liberation/strong_attack_start.ogg";
    internal const string LiberationEncounterMapIconPrefix = "res://images/map/placeholder/social_floor_liberation_encounter_icon";
    internal const string EmeraldCrystalDefaultTexture = "res://images/monsters/social_floor_liberation/emerald_crystal/default.png";
    internal const string AreaS2Texture = "res://images/monsters/social_floor_liberation/false_throne/area_s2.png";
    internal const string FalseThroneDamagedTexture = "res://images/monsters/social_floor_liberation/false_throne/damaged.png";
    internal const string FalseThroneDefaultTexture = "res://images/monsters/social_floor_liberation/false_throne/default.png";
    internal const string FalseThroneFireTexture = "res://images/monsters/social_floor_liberation/false_throne/fire.png";
    internal const string FireS1Texture = "res://images/monsters/social_floor_liberation/false_throne/fire_s1.png";
    internal const string FalseThroneGuardTexture = "res://images/monsters/social_floor_liberation/false_throne/guard.png";
    internal const string PolymorphS4Texture = "res://images/monsters/social_floor_liberation/false_throne/polymorph_s4.png";
    internal const string RageS3Texture = "res://images/monsters/social_floor_liberation/false_throne/rage_s3.png";
    internal const string ScowlingFaceDamagedTexture = "res://images/monsters/social_floor_liberation/scowling_face/damaged.png";
    internal const string ScowlingFaceDefaultTexture = "res://images/monsters/social_floor_liberation/scowling_face/default.png";
    internal const string ScowlingFaceHitTexture = "res://images/monsters/social_floor_liberation/scowling_face/hit.png";
    internal const string ScowlingFaceMoveTexture = "res://images/monsters/social_floor_liberation/scowling_face/move.png";
    internal const string LiberationEncounterRunHistoryIcon = "res://images/ui/run_history/social_floor_liberation_encounter.png";
    internal const string LiberationEncounterOutlineRunHistoryIcon = "res://images/ui/run_history/social_floor_liberation_encounter_outline.png";
    internal const string CrystalAreaEffectTexture = "res://images/vfx/social_floor_liberation/crystal_area_effect.png";
    internal const string CrystalAreaEmbeddedTexture = "res://images/vfx/social_floor_liberation/crystal_area_embedded.png";
    internal const string CrystalAreaFallTexture = "res://images/vfx/social_floor_liberation/crystal_area_fall.png";
    internal const string CrystalAreaShockwaveTexture = "res://images/vfx/social_floor_liberation/crystal_area_shockwave.png";
    internal const string FarHitCrystalRiseTexture = "res://images/vfx/social_floor_liberation/far_hit_crystal_rise.png";
    internal const string FarPenetrateHitTexture = "res://images/vfx/social_floor_liberation/far_penetrate_hit.png";
    internal const string LiberationTransformationTexture = "res://images/vfx/social_floor_liberation/transformation.png";
    internal const string TransformationMaskTexture = "res://images/vfx/social_floor_liberation/transformation_mask.png";
    internal const string TransformationMask2Texture = "res://images/vfx/social_floor_liberation/transformation_mask2.png";
    internal const string LiberationEncounterBg00ABackgroundScene = "res://scenes/backgrounds/social_floor_liberation_encounter/layers/social_floor_liberation_encounter_bg_00_a.tscn";
    internal const string LiberationEncounterBackgroundScene = "res://scenes/backgrounds/social_floor_liberation_encounter/social_floor_liberation_encounter_background.tscn";
    internal const string LiberationEncounterScene = "res://scenes/encounters/social_floor_liberation_encounter.tscn";
}
