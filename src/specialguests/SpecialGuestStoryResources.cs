using System;
using Godot;
using MegaCrit.Sts2.Core.Logging;

namespace LibraryOfRuina.specialguests;

/// <summary>
/// Original Library of Ruina story-screen resources copied byte-for-byte from
/// the unpacked project.  Keeping them in the special-guest asset set prevents
/// room transitions from unloading a resource while a story is visible.
/// </summary>
public static class SpecialGuestStoryResources
{
    private const string UiRoot = "res://images/special_guests/story_ui/";

    public const string BlackBackground = UiRoot + "lor_story_black.png";
    public const string DialogueOverlay = UiRoot + "lor_story_overlay.png";
    public const string EdgeFrame = UiRoot + "lor_story_frame.png";
    public const string Nameplate = UiRoot + "lor_story_nameplate.png";
    public const string AdvanceIcon = UiRoot + "lor_story_advance.png";

    public const string LatinKoreanFont =
        "res://fonts/story_ui/lor_story_arita_buri.otf";
    /// <summary>
    /// Noto Sans CJK SC 的子集：GB2312 全集加本模组 zhs 文案用到的字，`tools/check_zhs_font.py` 检查覆盖。
    /// </summary>
    public const string ChineseFont =
        "res://fonts/NotoSansCJKsc-Regular.otf";

    /// <summary>
    /// 本体自带的 Noto Sans CJK JP，字体数据与本模组原先打包的那份逐字节相同。本体按 MSDF 导入，
    /// <see cref="LoadFont"/> 在副本上改回普通光栅化。它归本体主题持有，不放进 <see cref="AssetPaths"/>。
    /// </summary>
    public const string JapaneseFont =
        "res://fonts/jpn/NotoSansCJKjp-Regular.otf";

    public static IReadOnlyList<string> AssetPaths { get; } =
    [
        BlackBackground,
        DialogueOverlay,
        EdgeFrame,
        Nameplate,
        AdvanceIcon,
        LatinKoreanFont,
        ChineseFont,
    ];

    public static string GetFontPath(string language)
    {
        if (language.Equals("zhs", StringComparison.OrdinalIgnoreCase))
        {
            return ChineseFont;
        }

        if (language.Equals("jpn", StringComparison.OrdinalIgnoreCase))
        {
            return JapaneseFont;
        }

        return LatinKoreanFont;
    }

    /// <summary>剧情画面用的字体；缺失时记错误并返回 null，由调用方沿用主题字体。</summary>
    internal static Font? LoadFont(string language)
    {
        string path = GetFontPath(language);
        Font? font = ResourceLoader.Load<Font>(path);
        if (font == null)
        {
            Log.Error("[SpecialGuestStory] Missing Library of Ruina story font: " + path);
            return null;
        }

        if (path == JapaneseFont && font is FontFile vanillaImport)
        {
            // 本体按 MSDF 导入这份字体；本模组一直用普通光栅化显示剧情文字。在副本上改回原来的导入参数，
            // 不动本体界面共用的那份。改完后与本模组原先的导入结果逐项相同。
            var copy = (FontFile)vanillaImport.Duplicate();
            copy.MultichannelSignedDistanceField = false;
            copy.MsdfPixelRange = 8;
            copy.ClearCache();
            return copy;
        }

        return font;
    }
}
