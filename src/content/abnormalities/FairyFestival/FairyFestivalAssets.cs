namespace LibraryOfRuina.content.abnormalities.FairyFestival;

// 本实体引用的 res:// 资源路径；tools/check_res_paths.py 按仓库文件核对（大小写敏感）。
// 保持 const：调用点编译期内联成同一个字面量，何时、怎样加载仍由调用点决定。
// 别处实体持有的路径引用对方的常量，不重复写值。
internal static class FairyFestivalAssets
{
    internal const string HistoryLayer1Bgm = "res://audio/bgm/fairy_festival/history_layer_1.ogg";
    internal const string HistoryLayer2Bgm = "res://audio/bgm/fairy_festival/history_layer_2.ogg";
    internal const string HistoryLayer3Bgm = "res://audio/bgm/fairy_festival/history_layer_3.ogg";
    internal const string FairyFestivalSfxRoot = "res://audio/sfx/fairy_festival/";
    internal const string MassAttackSfx = "res://audio/sfx/fairy_festival/mass_attack.ogg";
    internal const string Background1 = "res://images/backgrounds/fairy_festival_strong/background_1.png";
    internal const string Background2 = "res://images/backgrounds/fairy_festival_strong/background_2.png";
    internal const string FairyFestivalMonsterRoot = "res://images/monsters/fairy_festival/";
    internal const string PredationOverlayTexture = "res://images/vfx/fairy_festival_predation_overlay.png";
}
