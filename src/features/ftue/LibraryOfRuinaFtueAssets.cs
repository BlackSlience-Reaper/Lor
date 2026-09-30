using LibraryOfRuina.content.abnormalities.BlueStar;
using LibraryOfRuina.content.liberation.Literature;
using LibraryOfRuina.content.specialguests.Iori;
using LibraryOfRuina.content.specialguests.Kali;
using LibraryOfRuina.content.specialguests.Rnfmabj;
using LibraryOfRuina.content.specialguests.Xiao;
using LibraryOfRuina.framework.assets;

namespace LibraryOfRuina.features.ftue;

// 本实体引用的 res:// 资源路径；tools/check_res_paths.py 按仓库文件核对（大小写敏感）。
// 保持 const：调用点编译期内联成同一个字面量，何时、怎样加载仍由调用点决定。
// 别处实体持有的路径引用对方的常量，不重复写值。
internal static class LibraryOfRuinaFtueAssets
{
    internal const string ModImageTexture = "res://LibraryOfRuina/mod_image.png";
    internal const string BluntNormalTexture = "res://LibraryOfRuinaLib/images/resistance/blunt_normal.png";
    internal const string PierceNormalTexture = "res://LibraryOfRuinaLib/images/resistance/pierce_normal.png";
    internal const string SlashChaosImmuneTexture = "res://LibraryOfRuinaLib/images/resistance/slash_chaos_immune.png";
    internal const string SlashNormalTexture = "res://LibraryOfRuinaLib/images/resistance/slash_normal.png";
    internal const string AbnormalityFtue0Texture = "res://images/ftue/abnormality_ftue_0.png";
    internal const string AbnormalityFtue1Texture = "res://images/ftue/abnormality_ftue_1.png";
    internal const string AbnormalityFtue2Texture = "res://images/ftue/abnormality_ftue_2.png";
    internal const string CombatFtue0Texture = "res://images/ftue/combat_ftue_0.png";
    internal const string FtuePointerArrowTexture = "res://images/ftue/ftue_pointer_arrow.png";
    internal const string FtuePopupTexture = "res://images/ftue/ftue_popup.png";
    internal const string GuestFtue0Texture = "res://images/ftue/guest_ftue_0.png";
    internal const string GuestFtue1Texture = "res://images/ftue/guest_ftue_1.png";
    internal const string GuestFtue2Texture = "res://images/ftue/guest_ftue_2.png";
    internal const string LiberationFtue0Texture = "res://images/ftue/liberation_ftue_0.png";
    internal const string LiberationFtue1Texture = "res://images/ftue/liberation_ftue_1.png";
    internal const string LiberationFtue2Texture = "res://images/ftue/liberation_ftue_2.png";
    internal const string SettingsTinyLeftArrowTexture = "res://images/packed/common_ui/settings_tiny_left_arrow.png";
    internal const string SettingsTinyRightArrowTexture = "res://images/packed/common_ui/settings_tiny_right_arrow.png";
    internal const string BlueStarPageRelicTexture = "res://images/relics/blue_star_page_relic.png";
    internal const string BookShadowRelicTexture = "res://images/relics/book_shadow_relic.png";
    internal const string FtuePopupResource = "res://shaders/ftue_popup.tres";
    internal const string BlueStarStrongBackground = BlueStarAssets.StrongBackground;
    internal const string CreatureMapLatitiaCompositeBackground = LiteratureFloorAssets.CreatureMapLatitiaCompositeBackground;
    internal const string IoriSpecialGuestEventTexture = IoriSpecialGuestIds.EventImage;
    internal const string KaliSpecialGuestEventTexture = KaliSpecialGuestIds.EventImage;
    internal const string RnfmabjSpecialGuestEventTexture = RnfmabjSpecialGuestIds.EventImage;
    internal const string XiaoSpecialGuestEventTexture = XiaoSpecialGuestIds.EventImage;
    internal const string KreonBoldGlyphSpaceOneResource = SharedAssets.KreonBoldGlyphSpaceOneResource;
    internal const string KreonBoldGlyphSpaceTwoResource = SharedAssets.KreonBoldGlyphSpaceTwoResource;
    internal const string KreonBoldSharedResource = SharedAssets.KreonBoldSharedResource;
    internal const string KreonRegularGlyphSpaceOneResource = SharedAssets.KreonRegularGlyphSpaceOneResource;
    internal const string KreonRegularSharedResource = SharedAssets.KreonRegularSharedResource;
}
