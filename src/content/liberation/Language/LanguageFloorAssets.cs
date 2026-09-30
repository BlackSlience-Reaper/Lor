using LibraryOfRuina.content.abnormalities.Nosferatu;

namespace LibraryOfRuina.content.liberation.Language;

// 本实体引用的 res:// 资源路径；tools/check_res_paths.py 按仓库文件核对（大小写敏感）。
// 保持 const：调用点编译期内联成同一个字面量，何时、怎样加载仍由调用点决定。
// 别处实体持有的路径引用对方的常量，不重复写值。
internal static class LanguageFloorAssets
{
    internal const string RolandLiberationPhase1Bgm = "res://audio/bgm/language_floor_liberation/roland_liberation_phase_1.ogg";
    internal const string RolandLiberationPhase2Bgm = "res://audio/bgm/language_floor_liberation/roland_liberation_phase_2.ogg";
    internal const string RolandLiberationPhase3Bgm = "res://audio/bgm/language_floor_liberation/roland_liberation_phase_3.ogg";
    internal const string CobaltScarSfxRoot = "res://audio/sfx/language_floor_liberation/cobalt_scar/";
    internal const string LiberationMimicrySfxRoot = "res://audio/sfx/language_floor_liberation/mimicry/";
    internal const string LittleRedAttackSfx = "res://audio/sfx/little_red_mercenary/little_red_attack.ogg";
    internal const string LittleRedFireSfx = "res://audio/sfx/little_red_mercenary/little_red_fire.ogg";
    internal const string LittleRedRageSfx = "res://audio/sfx/little_red_mercenary/little_red_rage.ogg";
    internal const string LittleRedUnrelievedSfx = "res://audio/sfx/little_red_mercenary/little_red_unrelieved.ogg";
    internal const string WolfBiteSfx = "res://audio/sfx/little_red_mercenary/wolf_bite.ogg";
    internal const string WolfHowlSfx = "res://audio/sfx/little_red_mercenary/wolf_howl.ogg";
    internal const string Background1 = "res://images/backgrounds/language_floor_liberation_encounter/background_1.png";
    internal const string Background2 = "res://images/backgrounds/language_floor_liberation_encounter/background_2.png";
    internal const string Background3 = "res://images/backgrounds/language_floor_liberation_encounter/background_3.png";
    internal const string Background5 = "res://images/backgrounds/language_floor_liberation_encounter/background_5.png";
    internal const string NosferatuBackground = "res://images/backgrounds/nosferatu_elite/nosferatu_background.png";
    internal const string LiberationEncounterMapIconPrefix = "res://images/map/placeholder/language_floor_liberation_encounter_icon";
    internal const string LanguageFloorLiberationMonsterRoot = "res://images/monsters/language_floor_liberation/";
    internal const string LiberationDipsiaMonsterRoot = "res://images/monsters/language_floor_liberation/dipsia/";
    internal const string LiberationMimicryMonsterRoot = "res://images/monsters/language_floor_liberation/mimicry/";
    internal const string SmilingFaceMonsterRoot = "res://images/monsters/language_floor_liberation/smiling_face/";
    internal const string FearCardTexture = "res://images/packed/card_portraits/status/language_floor_fear_card.png";
    internal const string ArtFloorGreenPassivePowerIcon = "res://images/powers/art_floor_green_passive_power.png";
    internal const string AngerGaugePowerIcon = "res://images/powers/language_floor_anger_gauge_power.png";
    internal const string DeathTrackerPowerIcon = "res://images/powers/language_floor_death_tracker_power.png";
    internal const string DestinedBigBadWolfPassivePowerIcon = "res://images/powers/language_floor_destined_big_bad_wolf_passive_power.png";
    internal const string ExhaustionPassivePowerIcon = "res://images/powers/language_floor_exhaustion_passive_power.png";
    internal const string HideInDarknessPassivePowerIcon = "res://images/powers/language_floor_hide_in_darkness_passive_power.png";
    internal const string HuntMarkPowerIcon = "res://images/powers/language_floor_hunt_mark_power.png";
    internal const string MeltingCorpseRotPowerIcon = "res://images/powers/language_floor_melting_corpse_rot_power.png";
    internal const string MimicryMimicPowerIcon = "res://images/powers/language_floor_mimicry_mimic_power.png";
    internal const string PunishEvilPassivePowerIcon = "res://images/powers/language_floor_punish_evil_passive_power.png";
    internal const string RagePowerIcon = "res://images/powers/language_floor_rage_power.png";
    internal const string RipOpenClawPassivePowerIcon = "res://images/powers/language_floor_rip_open_claw_passive_power.png";
    internal const string ScarPowerIcon = "res://images/powers/language_floor_scar_power.png";
    internal const string ShadowAmbushPassivePowerIcon = "res://images/powers/language_floor_shadow_ambush_passive_power.png";
    internal const string ShadowWolfPowerIcon = "res://images/powers/language_floor_shadow_wolf_power.png";
    internal const string SmilingFaceFindCorpsesPowerIcon = "res://images/powers/language_floor_smiling_face_find_corpses_power.png";
    internal const string SmilingFaceFormOneSplitPowerIcon = "res://images/powers/language_floor_smiling_face_form_one_split_power.png";
    internal const string SmilingFaceFormThreeSplitPowerIcon = "res://images/powers/language_floor_smiling_face_form_three_split_power.png";
    internal const string SmilingFaceFusionPowerIcon = "res://images/powers/language_floor_smiling_face_fusion_power.png";
    internal const string SmilingFaceScreamPowerIcon = "res://images/powers/language_floor_smiling_face_scream_power.png";
    internal const string SmilingFaceSplitAndFusionPowerIcon = "res://images/powers/language_floor_smiling_face_split_and_fusion_power.png";
    internal const string SmilingFaceVomitPowerIcon = "res://images/powers/language_floor_smiling_face_vomit_power.png";
    internal const string UnrelievedAngerPowerIcon = "res://images/powers/language_floor_unrelieved_anger_power.png";
    internal const string WolfHowlPassivePowerIcon = "res://images/powers/language_floor_wolf_howl_passive_power.png";
    internal const string LiberationEncounterRunHistoryIcon = "res://images/ui/run_history/language_floor_liberation_encounter.png";
    internal const string LiberationEncounterOutlineRunHistoryIcon = "res://images/ui/run_history/language_floor_liberation_encounter_outline.png";
    internal const string LiberationEncounterBackgroundScene = "res://scenes/backgrounds/language_floor_liberation_encounter/language_floor_liberation_encounter_background.tscn";
    internal const string LiberationEncounterScene = "res://scenes/encounters/language_floor_liberation_encounter.tscn";
    internal const string NosferatuSfxRoot = NosferatuAssets.NosferatuSfxRoot;
    internal const string NosferatuBloodPowerIcon = NosferatuAssets.BloodPowerIcon;
    internal const string NosferatuFlowingBloodPowerIcon = NosferatuAssets.FlowingBloodPowerIcon;
    internal const string NosferatuHydrophobiaPassivePowerIcon = NosferatuAssets.HydrophobiaPassivePowerIcon;
    internal const string NosferatuTransformPowerIcon = NosferatuAssets.TransformPowerIcon;
}
