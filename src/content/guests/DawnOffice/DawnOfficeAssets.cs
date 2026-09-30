namespace LibraryOfRuina.content.guests.DawnOffice;

// 本实体引用的 res:// 资源路径；tools/check_res_paths.py 按仓库文件核对（大小写敏感）。
// 保持 const：调用点编译期内联成同一个字面量，何时、怎样加载仍由调用点决定。
// 别处实体持有的路径引用对方的常量，不重复写值。
internal static class DawnOfficeAssets
{
    internal const string Battle1Bgm = "res://audio/bgm/dawn_office/dawn_office_battle_1.ogg";
    internal const string Battle2Bgm = "res://audio/bgm/dawn_office/dawn_office_battle_2.ogg";
    internal const string Battle3Bgm = "res://audio/bgm/dawn_office/dawn_office_battle_3.ogg";
    internal const string FinnBattle1Bgm = "res://audio/bgm/finn/finn_battle_1.ogg";
    internal const string GinTexture = "res://images/monsters/gin.png";
    internal const string PhilipTexture = "res://images/monsters/philip.png";
    internal const string SalvadorTexture = "res://images/monsters/salvador.png";
    internal const string SayoTexture = "res://images/monsters/sayo.png";
    internal const string SayoAttackSlashTexture = "res://images/monsters/sayo_attack_slash.png";
    internal const string SayoAttackStrikeTexture = "res://images/monsters/sayo_attack_strike.png";
    internal const string SayoAttackThrustTexture = "res://images/monsters/sayo_attack_thrust.png";
    internal const string SayoHitTexture = "res://images/monsters/sayo_hit.webp";
    internal const string YangTexture = "res://images/monsters/yang.png";
    internal const string YunaTexture = "res://images/monsters/yuna.png";
}
