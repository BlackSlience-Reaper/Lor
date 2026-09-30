using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;
using LibraryOfRuina.content.specialguests;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes;
using Environment = System.Environment;

namespace LibraryOfRuinaVerification;

/// <summary>
/// 剧情字体：日文借用本体的 Noto Sans CJK JP 并在副本上关掉 MSDF（本体那份保持 MSDF）；
/// 简体中文是子集字体，逐字检查 zhs 文案都能显示；其余语言的 Arita-buri 能加载。
/// </summary>
internal static class SpecialGuestStoryFontVerificationPatch
{
    private const string VerifyArg = "lor-verify-story-fonts";
    private const string LogPrefix = "[LibraryOfRuina.StoryFonts.Verify] ";
    private const string ZhsLocalizationDir = "res://LibraryOfRuina/localization/zhs";

    private static bool _started;

    internal static void Start()
    {
        if (_started || !HasVerifyArg())
        {
            return;
        }

        _started = true;
        Callable.From(Run).CallDeferred();
    }

    private static bool HasVerifyArg() =>
        CommandLineHelper.HasArg(VerifyArg)
        || Environment.GetCommandLineArgs().Any(arg => string.Equals(
            arg.TrimStart('-'),
            VerifyArg,
            StringComparison.OrdinalIgnoreCase));

    private static void Run()
    {
        try
        {
            FontFile japanese = SpecialGuestStoryResources.LoadFont("jpn") as FontFile
                ?? throw new InvalidOperationException("jpn story font did not load as FontFile.");
            FontFile vanilla = ResourceLoader.Load<FontFile>(SpecialGuestStoryResources.JapaneseFont);
            if (japanese.MultichannelSignedDistanceField
                || japanese.MsdfPixelRange != 8
                || !vanilla.MultichannelSignedDistanceField
                || ReferenceEquals(japanese, vanilla)
                || japanese.Data.Length != vanilla.Data.Length
                || japanese.FontName != "Noto Sans CJK JP Regular")
            {
                throw new InvalidOperationException(
                    $"jpn font: msdf={japanese.MultichannelSignedDistanceField} range={japanese.MsdfPixelRange} "
                    + $"vanillaMsdf={vanilla.MultichannelSignedDistanceField} name={japanese.FontName} "
                    + $"bytes={japanese.Data.Length}/{vanilla.Data.Length}");
            }

            Font chinese = SpecialGuestStoryResources.LoadFont("zhs")
                ?? throw new InvalidOperationException("zhs story font did not load.");
            HashSet<int> used = ReadZhsCodePoints(out int files);
            List<int> missing = used.Where(c => !Rune.IsWhiteSpace(new Rune(c)) && !chinese.HasChar(c))
                .OrderBy(c => c)
                .ToList();
            if (files == 0 || used.Count < 1000 || missing.Count > 0)
            {
                throw new InvalidOperationException(
                    $"zhs font: files={files} chars={used.Count} missing={missing.Count} "
                    + string.Join(" ", missing.Take(20).Select(c => $"U+{c:X4}")));
            }

            Font latin = SpecialGuestStoryResources.LoadFont("eng")
                ?? throw new InvalidOperationException("eng story font did not load.");

            Log.Info(LogPrefix
                + $"STORY_FONTS_OK jpnBytes={japanese.Data.Length} zhsFiles={files} zhsChars={used.Count} "
                + $"eng={latin.GetFontName()}");
            NGame.Instance?.GetTree().Quit();
        }
        catch (Exception ex)
        {
            Log.Error(LogPrefix + "STORY_FONTS_FAILED: " + ex);
            NGame.Instance?.GetTree().Quit(1);
        }
    }

    private static HashSet<int> ReadZhsCodePoints(out int files)
    {
        var chars = new HashSet<int>();
        files = 0;
        foreach (string name in DirAccess.GetFilesAt(ZhsLocalizationDir))
        {
            if (!name.EndsWith(".json", StringComparison.Ordinal))
            {
                continue;
            }

            files++;
            chars.UnionWith(FileAccess.GetFileAsString(ZhsLocalizationDir + "/" + name)
                .EnumerateRunes()
                .Select(rune => rune.Value));
        }

        return chars;
    }
}
