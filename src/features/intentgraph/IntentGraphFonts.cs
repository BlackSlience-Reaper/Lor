using Godot;
using LibraryOfRuina.framework.assets;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.Fonts;

namespace LibraryOfRuina.features.intentgraph;

/// <summary>
/// 意图图的字体，与 Intent Graph（Chaofan）相同：数值用 Kreon 粗体；标签也用 Kreon 粗体，
/// 简繁中文时拉丁字母仍用 Kreon、汉字接当前语言的粗体字体，其余需要换字体的语言整段用当前语言的粗体字体。
/// 图是直接画的（DrawString），不经过 MegaLabel，所以按语言换字体要在这里自己做。每次新建，不缓存，切换语言后自然生效。
/// </summary>
internal static class IntentGraphFonts
{
    public static Font CreateLabelFont()
    {
        Font kreon = ResourceLoader.Load<Font>(SharedAssets.KreonBoldGlyphSpaceOneResource);
        string? language = LocManager.Instance?.Language;
        Font? substitute = language != null && FontManager.NeedsFontSubstitution(language)
            ? FontManager.GetSubstituteFont(language, FontType.Bold)
            : null;
        if (substitute == null)
        {
            return kreon;
        }

        bool chinese = language is "zhs" or "zht";
        return new FontVariation
        {
            BaseFont = chinese ? kreon : substitute,
            Fallbacks = chinese ? new Godot.Collections.Array<Font> { substitute } : new Godot.Collections.Array<Font>()
        };
    }

    /// <summary>数值基本是数字和 x；本模组的意图可能带别的文字，接上当前语言的粗体字体兜底，拉丁字符仍用 Kreon。</summary>
    public static Font CreateValueFont()
    {
        Font kreon = ResourceLoader.Load<Font>(SharedAssets.KreonBoldGlyphSpaceOneResource);
        string? language = LocManager.Instance?.Language;
        Font? substitute = language != null && FontManager.NeedsFontSubstitution(language)
            ? FontManager.GetSubstituteFont(language, FontType.Bold)
            : null;
        return substitute == null
            ? kreon
            : new FontVariation { BaseFont = kreon, Fallbacks = new Godot.Collections.Array<Font> { substitute } };
    }
}
