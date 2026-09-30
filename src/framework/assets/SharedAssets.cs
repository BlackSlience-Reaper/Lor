namespace LibraryOfRuina.framework.assets;

// 被多个实体共用、不归属任何一个实体的 res:// 资源路径，含本模组引用的原版资源
// （原版路径另列在 tools/vanilla_res_paths.txt）。保持 const，理由同各实体的 Assets 类。
internal static class SharedAssets
{
    internal const string LanguageReceptionFloorGeburabattle1Bgm = "res://audio/bgm/language_reception_floor/GeburaBattle1.ogg";
    internal const string LiteratureReceptionFloor1Bgm = "res://audio/bgm/literature_reception_floor/literature_reception_floor_1.ogg";
    internal const string ImagesMonstersRoot = "res://images/monsters/";
    internal const string LibraryPassiveGreenIcon = "res://images/powers/library_passive_green.png";
    internal const string LibraryPassiveOrangeIcon = "res://images/powers/library_passive_orange.png";
    internal const string LibraryPassivePurpleIcon = "res://images/powers/library_passive_purple.png";
    internal const string SpiderBudUntargetablePowerIcon = "res://images/powers/spider_bud_untargetable_power.png";
    internal const string KreonBoldGlyphSpaceOneResource = "res://themes/kreon_bold_glyph_space_one.tres";
    internal const string KreonBoldGlyphSpaceTwoResource = "res://themes/kreon_bold_glyph_space_two.tres";
    internal const string KreonBoldSharedResource = "res://themes/kreon_bold_shared.tres";
    internal const string KreonRegularGlyphSpaceOneResource = "res://themes/kreon_regular_glyph_space_one.tres";
    internal const string KreonRegularSharedResource = "res://themes/kreon_regular_shared.tres";
}
