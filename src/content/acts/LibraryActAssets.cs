namespace LibraryOfRuina.content.acts;

// 本实体引用的 res:// 资源路径；tools/check_res_paths.py 按仓库文件核对（大小写敏感）。
// 保持 const：调用点编译期内联成同一个字面量，何时、怎样加载仍由调用点决定。
// 别处实体持有的路径引用对方的常量，不重复写值。
internal static class LibraryActAssets
{
    internal const string BlueReverberationTexture = "res://images/map/reverberation_ensemble/blue_reverberation.png";
    internal const string MapBottomBinahTexture = "res://images/packed/map/map_bgs/binah/map_bottom_binah.png";
    internal const string MapMiddleBinahTexture = "res://images/packed/map/map_bgs/binah/map_middle_binah.png";
    internal const string MapTopBinahTexture = "res://images/packed/map/map_bgs/binah/map_top_binah.png";
    internal const string MapBottomChesedTexture = "res://images/packed/map/map_bgs/chesed/map_bottom_chesed.png";
    internal const string MapMiddleChesedTexture = "res://images/packed/map/map_bgs/chesed/map_middle_chesed.png";
    internal const string MapTopChesedTexture = "res://images/packed/map/map_bgs/chesed/map_top_chesed.png";
    internal const string MapBottomGeburaTexture = "res://images/packed/map/map_bgs/gebura/map_bottom_gebura.png";
    internal const string MapMiddleGeburaTexture = "res://images/packed/map/map_bgs/gebura/map_middle_gebura.png";
    internal const string MapTopGeburaTexture = "res://images/packed/map/map_bgs/gebura/map_top_gebura.png";
    internal const string MapBottomHodTexture = "res://images/packed/map/map_bgs/hod/map_bottom_hod.png";
    internal const string MapMiddleHodTexture = "res://images/packed/map/map_bgs/hod/map_middle_hod.png";
    internal const string MapTopHodTexture = "res://images/packed/map/map_bgs/hod/map_top_hod.png";
    internal const string MapBottomMalkuthTexture = "res://images/packed/map/map_bgs/malkuth/map_bottom_malkuth.png";
    internal const string MapMiddleMalkuthTexture = "res://images/packed/map/map_bgs/malkuth/map_middle_malkuth.png";
    internal const string MapTopMalkuthTexture = "res://images/packed/map/map_bgs/malkuth/map_top_malkuth.png";
    internal const string MapBottomNetzachTexture = "res://images/packed/map/map_bgs/netzach/map_bottom_netzach.png";
    internal const string MapMiddleNetzachTexture = "res://images/packed/map/map_bgs/netzach/map_middle_netzach.png";
    internal const string MapTopNetzachTexture = "res://images/packed/map/map_bgs/netzach/map_top_netzach.png";
    internal const string ReverberationEnsembleRoot = "res://images/packed/map/map_bgs/reverberation_ensemble/";
    internal const string MapBottomTipherethTexture = "res://images/packed/map/map_bgs/tiphereth/map_bottom_tiphereth.png";
    internal const string MapMiddleTipherethTexture = "res://images/packed/map/map_bgs/tiphereth/map_middle_tiphereth.png";
    internal const string MapTopTipherethTexture = "res://images/packed/map/map_bgs/tiphereth/map_top_tiphereth.png";
    internal const string MapBottomYesodTexture = "res://images/packed/map/map_bgs/yesod/map_bottom_yesod.png";
    internal const string MapMiddleYesodTexture = "res://images/packed/map/map_bgs/yesod/map_middle_yesod.png";
    internal const string MapTopYesodTexture = "res://images/packed/map/map_bgs/yesod/map_top_yesod.png";
    internal const string ReverberationBossOutlineTexture = "res://shaders/reverberation_boss_outline.gdshader";
}
