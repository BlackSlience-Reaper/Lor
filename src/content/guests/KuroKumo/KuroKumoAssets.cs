namespace LibraryOfRuina.content.guests.KuroKumo;

// 本实体引用的 res:// 资源路径；tools/check_res_paths.py 按仓库文件核对（大小写敏感）。
// 保持 const：调用点编译期内联成同一个字面量，何时、怎样加载仍由调用点决定。
// 别处实体持有的路径引用对方的常量，不重复写值。
internal static class KuroKumoAssets
{
    internal const string KurokumoGuestBattle1Bgm = "res://audio/bgm/kurokumo/kurokumo_guest_battle_1.ogg";
    internal const string KurokumoGuestBattle2Bgm = "res://audio/bgm/kurokumo/kurokumo_guest_battle_2.ogg";
    internal const string KurokumoGuestBattle3Bgm = "res://audio/bgm/kurokumo/kurokumo_guest_battle_3.ogg";
    internal const string FramesManifestJson = "res://images/backgrounds/kuro_kumo_normal/frames/manifest.json";
}
