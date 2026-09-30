using LibraryOfRuina.content.abnormalities.AddictedEmployee;
using LibraryOfRuina.content.abnormalities.AllAroundHelper;

namespace LibraryOfRuina.content.liberation.Technology;

// 本实体引用的 res:// 资源路径；tools/check_res_paths.py 按仓库文件核对（大小写敏感）。
// 保持 const：调用点编译期内联成同一个字面量，何时、怎样加载仍由调用点决定。
// 别处实体持有的路径引用对方的常量，不重复写值。
internal static class TechnologyFloorAssets
{
    internal const string MagicBulletAttackSfx = "res://audio/sfx/technology_floor/magic_bullet/attack.ogg";
    internal const string RegretAttackSfx = "res://audio/sfx/technology_floor/regret/regret_attack.ogg";
    internal const string Background1 = "res://images/backgrounds/technology_floor_liberation_encounter/background_1.webp";
    internal const string Background2 = "res://images/backgrounds/technology_floor_liberation_encounter/background_2.webp";
    internal const string Phase3Background = "res://images/backgrounds/technology_floor_liberation_encounter/phase3_background.png";
    internal const string Phase4Background = "res://images/backgrounds/technology_floor_liberation_encounter/phase4_background.png";
    internal const string Phase5Background = "res://images/backgrounds/technology_floor_liberation_encounter/phase5_background.png";
    internal const string LiberationEncounterMapIconPrefix = "res://images/map/placeholder/technology_floor_liberation_encounter_icon";
    internal const string AddictedEmployeeAttackTexture = "res://images/monsters/addicted_employee/addicted_employee_attack.png";
    internal const string AddictedEmployeeGuardTexture = "res://images/monsters/addicted_employee/addicted_employee_guard.png";
    internal const string AddictedEmployeeHitTexture = "res://images/monsters/addicted_employee/addicted_employee_hit.png";
    internal const string AddictedEmployeeIdleTexture = "res://images/monsters/addicted_employee/addicted_employee_idle.png";
    internal const string TechnologyFloorChordMonsterRoot = "res://images/monsters/technology_floor/chord/";
    internal const string GrinderMk4DodgeTexture = "res://images/monsters/technology_floor/grinder_mk4_dodge.png";
    internal const string GrinderMk4EgoS1Texture = "res://images/monsters/technology_floor/grinder_mk4_ego_s1.png";
    internal const string GrinderMk4EgoS2Texture = "res://images/monsters/technology_floor/grinder_mk4_ego_s2.png";
    internal const string GrinderMk4EgoS3Texture = "res://images/monsters/technology_floor/grinder_mk4_ego_s3.png";
    internal const string GrinderMk4HitTexture = "res://images/monsters/technology_floor/grinder_mk4_hit.png";
    internal const string GrinderMk4IdleTexture = "res://images/monsters/technology_floor/grinder_mk4_idle.png";
    internal const string GrinderMk4SlashTexture = "res://images/monsters/technology_floor/grinder_mk4_slash.png";
    internal const string GrinderMk4ThrustTexture = "res://images/monsters/technology_floor/grinder_mk4_thrust.png";
    internal const string MagicBulletMonsterRoot = "res://images/monsters/technology_floor/magic_bullet/";
    internal const string RegretAttackLeftTexture = "res://images/monsters/technology_floor/regret_attack_left.png";
    internal const string RegretAttackRightTexture = "res://images/monsters/technology_floor/regret_attack_right.png";
    internal const string RegretAttackSlashTexture = "res://images/monsters/technology_floor/regret_attack_slash.png";
    internal const string RegretEgoS1Texture = "res://images/monsters/technology_floor/regret_ego_s1.png";
    internal const string RegretHitTexture = "res://images/monsters/technology_floor/regret_hit.png";
    internal const string RegretIdleTexture = "res://images/monsters/technology_floor/regret_idle.png";
    internal const string RegretParryTexture = "res://images/monsters/technology_floor/regret_parry.png";
    internal const string SolemnMourningMonsterRoot = "res://images/monsters/technology_floor/solemn_mourning/";
    internal const string LiberationEncounterRunHistoryIcon = "res://images/ui/run_history/technology_floor_liberation_encounter.png";
    internal const string LiberationEncounterOutlineRunHistoryIcon = "res://images/ui/run_history/technology_floor_liberation_encounter_outline.png";
    internal const string LiberationChordEncounterScene = "res://scenes/encounters/technology_floor_liberation_chord_encounter.tscn";
    internal const string LiberationMagicBulletEncounterScene = "res://scenes/encounters/technology_floor_liberation_magic_bullet_encounter.tscn";
    internal const string LiberationMk4EncounterScene = "res://scenes/encounters/technology_floor_liberation_mk4_encounter.tscn";
    internal const string LiberationSolemnMourningEncounterScene = "res://scenes/encounters/technology_floor_liberation_solemn_mourning_encounter.tscn";
    internal const string AllAroundHelperAttackSfx = AllAroundHelperAssets.AllAroundHelperAttackSfx;
    internal const string SongMachineAttackSfx = AddictedEmployeeAssets.SongMachineAttackSfx;
    internal const string AllAroundHelperTexture = AllAroundHelperAssets.AllAroundHelperTexture;
    internal const string AllAroundHelperAttackTexture = AllAroundHelperAssets.AllAroundHelperAttackTexture;
    internal const string AllAroundHelperHitTexture = AllAroundHelperAssets.AllAroundHelperHitTexture;
}
