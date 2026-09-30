using LibraryOfRuina.content.guests.DawnOffice;

namespace LibraryOfRuina.content.guests.WedgeOffice;

// 本实体引用的 res:// 资源路径；tools/check_res_paths.py 按仓库文件核对（大小写敏感）。
// 保持 const：调用点编译期内联成同一个字面量，何时、怎样加载仍由调用点决定。
// 别处实体持有的路径引用对方的常量，不重复写值。
internal static class WedgeOfficeAssets
{
    internal const string EriTexture = "res://images/monsters/eri.webp";
    internal const string OscarTexture = "res://images/monsters/oscar.webp";
    internal const string PamelaTexture = "res://images/monsters/pamela.webp";
    internal const string PameliTexture = "res://images/monsters/pameli.webp";
    internal const string PhilipTexture = DawnOfficeAssets.PhilipTexture;
}
