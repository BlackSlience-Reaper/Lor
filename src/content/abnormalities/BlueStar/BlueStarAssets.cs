namespace LibraryOfRuina.content.abnormalities.BlueStar;

// 本实体引用的 res:// 资源路径；tools/check_res_paths.py 按仓库文件核对（大小写敏感）。
// 保持 const：调用点编译期内联成同一个字面量，何时、怎样加载仍由调用点决定。
// 别处实体持有的路径引用对方的常量，不重复写值。
internal static class BlueStarAssets
{
    internal const string BlueStarSfxRoot = "res://audio/sfx/blue_star/";
    internal const string StrongBackground = "res://images/backgrounds/blue_star_strong/blue_star_strong_background.png";
    internal const string BlueStarAltarMonsterRoot = "res://images/monsters/blue_star_altar/";
    internal const string BlueStarFollowerMonsterRoot = "res://images/monsters/blue_star_follower/";
    internal const string BlueStarMartyrdomTexture = "res://images/packed/card_portraits/colorless/blue_star_martyrdom.png";
    internal const string DivinePowerIcon = "res://images/powers/blue_star_divine_power.png";
    internal const string FollowerVoicePowerIcon = "res://images/powers/blue_star_follower_voice_power.png";
    internal const string MartyrPowerIcon = "res://images/powers/blue_star_martyr_power.png";
    internal const string MartyrdomPowerIcon = "res://images/powers/blue_star_martyrdom_power.png";
    internal const string NovaVoicePowerIcon = "res://images/powers/blue_star_nova_voice_power.png";
    internal const string ReturnToStarsPowerIcon = "res://images/powers/blue_star_return_to_stars_power.png";
    internal const string StrongBackgroundScene = "res://scenes/backgrounds/blue_star_strong/blue_star_strong_background.tscn";
    internal const string StrongBg00ABackgroundScene = "res://scenes/backgrounds/blue_star_strong/layers/blue_star_strong_bg_00_a.tscn";
    internal const string BlueStarAltarScene = "res://scenes/creature_visuals/blue_star_altar.tscn";
    internal const string AltarNormalAnimationsResource = "res://scenes/creature_visuals/blue_star_altar_normal_animations.tres";
    internal const string AltarNovaAnimationsResource = "res://scenes/creature_visuals/blue_star_altar_nova_animations.tres";
    internal const string BlueStarFollowerScene = "res://scenes/creature_visuals/blue_star_follower.tscn";
    internal const string FollowerAnimationsResource = "res://scenes/creature_visuals/blue_star_follower_animations.tres";
}
