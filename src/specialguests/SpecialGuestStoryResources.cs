using System;

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
    public const string ChineseFont =
        "res://fonts/NotoSansCJKsc-Regular.otf";
    public const string JapaneseFont =
        "res://fonts/NotoSansCJKjp-Regular.otf";

    public static IReadOnlyList<string> AssetPaths { get; } =
    [
        BlackBackground,
        DialogueOverlay,
        EdgeFrame,
        Nameplate,
        AdvanceIcon,
        LatinKoreanFont,
        ChineseFont,
        JapaneseFont,
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
}
