using LibraryOfRuina.content.abnormalities.AllAroundHelper;
using LibraryOfRuina.content.abnormalities.RedShoes;
using LibraryOfRuina.content.abnormalities.ScorchedGirl;
using LibraryOfRuina.framework.assets;

namespace LibraryOfRuina.content.liberation.History;

// 本实体引用的 res:// 资源路径；tools/check_res_paths.py 按仓库文件核对（大小写敏感）。
// 保持 const：调用点编译期内联成同一个字面量，何时、怎样加载仍由调用点决定。
// 别处实体持有的路径引用对方的常量，不重复写值。
internal static class HistoryFloorAssets
{
    internal const string AngelaLiberationPhase1Bgm = "res://audio/bgm/angela_liberation/angela_liberation_phase_1.ogg";
    internal const string AngelaLiberationPhase2Bgm = "res://audio/bgm/angela_liberation/angela_liberation_phase_2.ogg";
    internal const string AngelaLiberationPhase3Bgm = "res://audio/bgm/angela_liberation/angela_liberation_phase_3.ogg";
    internal const string HappyTeddyForgottenAttackSfx = "res://audio/sfx/happy_teddy/happy_teddy_forgotten_attack.ogg";
    internal const string HappyTeddyForgottenEmbraceSfx = "res://audio/sfx/happy_teddy/happy_teddy_forgotten_embrace.ogg";
    internal const string HappyTeddyForgottenParrySfx = "res://audio/sfx/happy_teddy/happy_teddy_forgotten_parry.ogg";
    internal const string BossAttackSfx = "res://audio/sfx/history_floor/fluttering/boss_attack.ogg";
    internal const string BossChangeSfx = "res://audio/sfx/history_floor/fluttering/boss_change.ogg";
    internal const string BossDevourSfx = "res://audio/sfx/history_floor/fluttering/boss_devour.ogg";
    internal const string MassAttackSfx = "res://audio/sfx/history_floor/fluttering/mass_attack.ogg";
    internal const string FlutteringSpecialSfx = "res://audio/sfx/history_floor/fluttering/special.ogg";
    internal const string SporeApplySfx = "res://audio/sfx/history_floor/wasp/spore_apply.ogg";
    internal const string WaspAttackBuffSfx = "res://audio/sfx/history_floor/wasp/wasp_attack_buff.ogg";
    internal const string WorkerAttackSlashSfx = "res://audio/sfx/history_floor/wasp/worker_attack_slash.ogg";
    internal const string WorkerAttackThrustSfx = "res://audio/sfx/history_floor/wasp/worker_attack_thrust.ogg";
    internal const string WorkerDodgeSfx = "res://audio/sfx/history_floor/wasp/worker_dodge.ogg";
    internal const string WorkerSpawnSfx = "res://audio/sfx/history_floor/wasp/worker_spawn.ogg";
    internal const string EmeraldBoughBackground = "res://images/backgrounds/history_floor/emerald_bough_background.png";
    internal const string WaspBackground = "res://images/backgrounds/history_floor/wasp_background.png";
    internal const string Background1 = "res://images/backgrounds/history_floor_liberation_encounter/background_1.png";
    internal const string Background2 = "res://images/backgrounds/history_floor_liberation_encounter/background_2.png";
    internal const string FlutteringBackground1 = "res://images/backgrounds/history_floor_liberation_encounter/fluttering_background_1.png";
    internal const string FlutteringBackground2 = "res://images/backgrounds/history_floor_liberation_encounter/fluttering_background_2.png";
    internal const string LiberationEncounterMapIconPrefix = "res://images/map/placeholder/history_floor_liberation_encounter_icon";
    internal const string HappyTeddyTexture = "res://images/monsters/happy_teddy.webp";
    internal const string HappyTeddyAttack1Texture = "res://images/monsters/happy_teddy_attack_1.webp";
    internal const string HappyTeddyAttack2Texture = "res://images/monsters/happy_teddy_attack_2.webp";
    internal const string HappyTeddyHitTexture = "res://images/monsters/happy_teddy_hit.webp";
    internal const string HistoryFloorMonsterRoot = "res://images/monsters/history_floor/";
    internal const string EmeraldBoughMonsterRoot = "res://images/monsters/history_floor/emerald_bough/";
    internal const string EndLightTexture = "res://images/monsters/history_floor/end_light.png";
    internal const string EndLightAttackTexture = "res://images/monsters/history_floor/end_light_attack.png";
    internal const string EndLightCastTexture = "res://images/monsters/history_floor/end_light_cast.png";
    internal const string EndLightHitTexture = "res://images/monsters/history_floor/end_light_hit.png";
    internal const string HistoryFloorFlutteringMonsterRoot = "res://images/monsters/history_floor/fluttering/";
    internal const string ForgottenAttackSlashTexture = "res://images/monsters/history_floor/forgotten_attack_slash.png";
    internal const string ForgottenAttackStrikeTexture = "res://images/monsters/history_floor/forgotten_attack_strike.png";
    internal const string ForgottenHitTexture = "res://images/monsters/history_floor/forgotten_hit.png";
    internal const string ForgottenIdleTexture = "res://images/monsters/history_floor/forgotten_idle.png";
    internal const string ForgottenSpecialTexture = "res://images/monsters/history_floor/forgotten_special.png";
    internal const string LastMatchTexture = "res://images/monsters/history_floor/last_match.webp";
    internal const string LastMatchAttackTexture = "res://images/monsters/history_floor/last_match_attack.png";
    internal const string LastMatchCastTexture = "res://images/monsters/history_floor/last_match_cast.png";
    internal const string LastMatchHitTexture = "res://images/monsters/history_floor/last_match_hit.png";
    internal const string HistoryFloorWaspMonsterRoot = "res://images/monsters/history_floor/wasp/";
    internal const string ScorchedGirlMonsterTexture = "res://images/monsters/scorched_girl_monster.png";
    internal const string ScorchedGirlMonsterAttackTexture = "res://images/monsters/scorched_girl_monster_attack.webp";
    internal const string ScorchedGirlMonsterHitTexture = "res://images/monsters/scorched_girl_monster_hit.webp";
    internal const string CorrosionPowerIcon = "res://images/powers/history_floor_corrosion_power.png";
    internal const string LiberationEncounterRunHistoryIcon = "res://images/ui/run_history/history_floor_liberation_encounter.png";
    internal const string LiberationEncounterOutlineRunHistoryIcon = "res://images/ui/run_history/history_floor_liberation_encounter_outline.png";
    internal const string FlutteringPredationOverlayTexture = "res://images/vfx/history_floor_fluttering_predation_overlay.png";
    internal const string WaspBuffOverlayTexture = "res://images/vfx/wasp_buff_overlay.png";
    internal const string WaspLoyaltyOverlayTexture = "res://images/vfx/wasp_loyalty_overlay.png";
    internal const string LiberationEmeraldBoughEncounterScene = "res://scenes/encounters/history_floor_liberation_emerald_bough_encounter.tscn";
    internal const string LiberationFlutteringEncounterScene = "res://scenes/encounters/history_floor_liberation_fluttering_encounter.tscn";
    internal const string LiberationWaspEncounterScene = "res://scenes/encounters/history_floor_liberation_wasp_encounter.tscn";
    internal const string FourthMatchFlameAttackSfx = ScorchedGirlAssets.FourthMatchFlameAttackSfx;
    internal const string ScorchedGirlExplosionSfx = ScorchedGirlAssets.ScorchedGirlExplosionSfx;
    internal const string ImagesMonstersRoot = SharedAssets.ImagesMonstersRoot;
    internal const string AllAroundHelperTexture = AllAroundHelperAssets.AllAroundHelperTexture;
    internal const string AllAroundHelperAttackTexture = AllAroundHelperAssets.AllAroundHelperAttackTexture;
    internal const string AllAroundHelperHitTexture = AllAroundHelperAssets.AllAroundHelperHitTexture;
    internal const string LeftShoeTexture = RedShoesAssets.LeftShoeTexture;
    internal const string LeftShoeAttackTexture = RedShoesAssets.LeftShoeAttackTexture;
    internal const string LeftShoeHitTexture = RedShoesAssets.LeftShoeHitTexture;
    internal const string LeftShoeParryTexture = RedShoesAssets.LeftShoeParryTexture;
}
