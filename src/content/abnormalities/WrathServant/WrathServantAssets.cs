namespace LibraryOfRuina.content.abnormalities.WrathServant;

// 本实体引用的 res:// 资源路径；tools/check_res_paths.py 按仓库文件核对（大小写敏感）。
// 保持 const：调用点编译期内联成同一个字面量，何时、怎样加载仍由调用点决定。
// 别处实体持有的路径引用对方的常量，不重复写值。
internal static class WrathServantAssets
{
    internal const string GreenStemHermitSfxRoot = "res://audio/sfx/green_stem_hermit/";
    internal const string WrathServantSfxRoot = "res://audio/sfx/wrath_servant/";
    internal const string TodayPlayPowerResource = "res://images/atlases/power_atlas.sprites/wrath_servant_today_play_power.tres";
    internal const string StrongBackground = "res://images/backgrounds/wrath_servant_strong/background.png";
    internal const string GreenStemHermitMonsterRoot = "res://images/monsters/green_stem_hermit/";
    internal const string GreenStemHermitGroundTexture = "res://images/monsters/green_stem_hermit/ground.png";
    internal const string GreenStemHermitHitTexture = "res://images/monsters/green_stem_hermit/hit.png";
    internal const string GreenStemHermitIdleTexture = "res://images/monsters/green_stem_hermit/idle.png";
    internal const string GreenStemHermitReachTexture = "res://images/monsters/green_stem_hermit/reach.png";
    internal const string GreenStemHermitThrustTexture = "res://images/monsters/green_stem_hermit/thrust.png";
    internal const string HermitStaffMonsterRoot = "res://images/monsters/hermit_staff/";
    internal const string WrathServantMonsterRoot = "res://images/monsters/wrath_servant/";
    internal const string AttackSlashTexture = "res://images/monsters/wrath_servant/attack_slash.png";
    internal const string AttackSlash2Texture = "res://images/monsters/wrath_servant/attack_slash2.png";
    internal const string AttackStrikeTexture = "res://images/monsters/wrath_servant/attack_strike.png";
    internal const string WrathServantHitTexture = "res://images/monsters/wrath_servant/hit.png";
    internal const string WrathServantIdleTexture = "res://images/monsters/wrath_servant/idle.png";
    internal const string WrathServantS1Texture = "res://images/monsters/wrath_servant/s1.png";
    internal const string WrathServantS2Texture = "res://images/monsters/wrath_servant/s2.png";
    internal const string WrathServantS3Texture = "res://images/monsters/wrath_servant/s3.png";
    internal const string WrathServantSpecialTexture = "res://images/monsters/wrath_servant/special.png";
    internal const string CanePowerIcon = "res://images/powers/cane_power.png";
    internal const string GreenStemHermitProtectionPowerIcon = "res://images/powers/green_stem_hermit_protection_power.png";
    internal const string CorrosionPowerIcon = "res://images/powers/wrath_servant_corrosion_power.png";
    internal const string NextTurnCorrosionPowerIcon = "res://images/powers/wrath_servant_next_turn_corrosion_power.png";
    internal const string SinnerCounterPowerIcon = "res://images/powers/wrath_servant_sinner_counter_power.png";
    internal const string StaffMarkPowerIcon = "res://images/powers/wrath_servant_staff_mark_power.png";
    internal const string StrongBg00ABackgroundScene = "res://scenes/backgrounds/wrath_servant_strong/layers/wrath_servant_strong_bg_00_a.tscn";
    internal const string StrongBackgroundScene = "res://scenes/backgrounds/wrath_servant_strong/wrath_servant_strong_background.tscn";
    internal const string GreenStemHermitScene = "res://scenes/creature_visuals/green_stem_hermit.tscn";
    internal const string GreenStemHermitAnimationsResource = "res://scenes/creature_visuals/green_stem_hermit_animations.tres";
    internal const string WrathServantScene = "res://scenes/creature_visuals/wrath_servant.tscn";
    internal const string WrathServantAnimationsResource = "res://scenes/creature_visuals/wrath_servant_animations.tres";
}
