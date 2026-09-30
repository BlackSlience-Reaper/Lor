using System;
using System.Collections.Generic;
using System.Linq;
using LibraryOfRuina.infra.helpers;

namespace PlayerIntMapCheck;

/// <summary>
/// 每个调用点按它在 src/ 里的参数调用 PlayerIntMapSerializer，与 Legacy 里的旧实现比对：
/// 解析结果（含字典枚举顺序）相同、写出的字符串逐字节相同、写出再读回的结果相同。任何不同都以非零退出码失败。
/// 调用点的参数改了，这里要跟着改。
/// </summary>
internal static class Program
{
    private const int XiaoCardLimit = 12; // XiaoSpecialGuestPowers.CardLimit

    private static readonly string[] Samples =
    [
        "", " ", ";", ";;", " ; ",
        "1:2", "2:1;1:2", "1:2;1:3", "1:3;2:5;1:0", "1:-5", "1:0", "0:3", "0:0",
        ":3", "3", "3:", "1:2:3", "1::2", " 1:2 ; 3:4 ", "\t1:2\t;\n3:4\n",
        "1 :2", "1: 2", "1:2 ", " 1:2", "+1:2", "-1:2", "1:+2", "1:-0", "01:002",
        "1:abc", "abc:1", "a;b;;1:1;;", "1:1;x:2;2:2", "1,000:2", "1:1,000", "1:2.0", "1:1e3",
        "18446744073709551615:7", "18446744073709551616:7", "4294967296:1",
        "1:2147483647", "1:2147483648", "1:-2147483648", "1:-2147483649",
        "1:4", "1:5", "1:11", "1:12", "1:13", "1:99",
        "76561198000000000:4;76561198000000001:-1;76561197999999999:12",
        "\uFF11:2", "1:\uFF12", "\u0661:2", "1:\u0662",
        "5:1;4:2;3:3;2:4;1:5;5:6",
    ];

    private static readonly int[] RnfmabjSequenceLengths = [-1, 0, 1, 4, 12];

    private static int _failures;
    private static int _checks;

    private static int Main()
    {
        foreach (string sample in Samples)
        {
            // SocialFloorLiberationEncounter.ParsePlayerValues / SerializePlayerValues
            CheckParse("Social.Parse", sample,
                Legacy.SocialParsePlayerValues(sample),
                PlayerIntMapSerializer.ParseClamped(sample, 0, int.MaxValue));
            // XiaoCardsPlayedPower.ParseCounts
            CheckParse("Xiao.Parse", sample,
                Legacy.XiaoParseCounts(sample, XiaoCardLimit),
                PlayerIntMapSerializer.ParseClamped(sample, 0, XiaoCardLimit));
            // RnfmabjDirectiveTracker.ParseProgress
            foreach (int length in RnfmabjSequenceLengths)
            {
                CheckParse($"Rnfmabj.Parse(len={length})", sample,
                    Legacy.RnfmabjParseProgress(sample, length),
                    PlayerIntMapSerializer.ParseClamped(sample, 0, Math.Max(0, length)));
            }
            // IoriStanceController.ParseContributions
            CheckParse("Iori.Parse", sample,
                Legacy.IoriParseContributions(sample),
                PlayerIntMapSerializer.ParsePositive(sample));

            // Write back what each site parsed, then read it again.
            Dictionary<ulong, int> raw = RawEntries(sample);
            CheckFormats(sample, raw);
            CheckFormats(sample + " (Iori)", Legacy.IoriParseContributions(sample));
            CheckFormats(sample + " (Xiao)", Legacy.XiaoParseCounts(sample, XiaoCardLimit));
        }

        // Values a caller may hold in memory before writing: negatives, extremes, unsorted keys.
        var inMemory = new List<Dictionary<ulong, int>>
        {
            new(),
            new() { [1] = 2 },
            new() { [3] = 1, [1] = 5, [2] = -3 },
            new() { [ulong.MaxValue] = int.MinValue, [0] = int.MaxValue },
            new() { [76561198000000001] = 0, [76561198000000000] = 7 },
        };
        foreach (Dictionary<ulong, int> map in inMemory)
        {
            CheckFormats("in-memory " + Describe(map), map);
        }

        // Social serializes any enumerable; duplicate keys keep their input order (stable sort).
        var socialInput = new List<KeyValuePair<ulong, int>>
        {
            new(2, 1), new(1, 5), new(2, -4), new(1, 0),
        };
        CheckString("Social.Format(duplicates)",
            Legacy.SocialSerializePlayerValues(socialInput),
            SocialFormat(socialInput));

        Console.WriteLine($"PlayerIntMapCheck: {_checks} checks, {_failures} failures");
        return _failures == 0 ? 0 : 1;
    }

    // SocialFloorLiberationEncounter.SerializePlayerValues after the change.
    private static string SocialFormat(IEnumerable<KeyValuePair<ulong, int>> values) =>
        PlayerIntMapSerializer.Format(
            values.Select(static entry =>
                new KeyValuePair<ulong, int>(entry.Key, Math.Max(0, entry.Value))));

    private static void CheckFormats(string label, Dictionary<ulong, int> map)
    {
        CheckString($"Social.Format {label}", Legacy.SocialSerializePlayerValues(map), SocialFormat(map));
        CheckString($"Xiao.Format {label}", Legacy.XiaoFormat(map), PlayerIntMapSerializer.Format(map));
        CheckString($"Rnfmabj.Format {label}", Legacy.RnfmabjFormatProgress(map), PlayerIntMapSerializer.Format(map));
        CheckString($"Iori.Format {label}", Legacy.IoriSerializeContributions(map), PlayerIntMapSerializer.Format(map));

        string written = PlayerIntMapSerializer.Format(map);
        CheckParse($"Social.RoundTrip {label}", written,
            Legacy.SocialParsePlayerValues(SocialFormat(map)),
            PlayerIntMapSerializer.ParseClamped(SocialFormat(map), 0, int.MaxValue));
        CheckParse($"Xiao.RoundTrip {label}", written,
            Legacy.XiaoParseCounts(written, XiaoCardLimit),
            PlayerIntMapSerializer.ParseClamped(written, 0, XiaoCardLimit));
        CheckParse($"Rnfmabj.RoundTrip {label}", written,
            Legacy.RnfmabjParseProgress(written, 4),
            PlayerIntMapSerializer.ParseClamped(written, 0, 4));
        CheckParse($"Iori.RoundTrip {label}", written,
            Legacy.IoriParseContributions(written),
            PlayerIntMapSerializer.ParsePositive(written));
    }

    // Every well-formed entry with its value untouched, so the format checks see negatives and large values too.
    private static Dictionary<ulong, int> RawEntries(string sample) =>
        PlayerIntMapSerializer.ParseClamped(sample, int.MinValue, int.MaxValue);

    private static void CheckParse(
        string label,
        string input,
        IReadOnlyDictionary<ulong, int> expected,
        IReadOnlyDictionary<ulong, int> actual)
    {
        _checks++;
        // Compare in enumeration order: callers iterate these dictionaries.
        if (!expected.SequenceEqual(actual))
        {
            _failures++;
            Console.WriteLine($"FAIL {label} input={Quote(input)} expected={Describe(expected)} actual={Describe(actual)}");
        }
    }

    private static void CheckString(string label, string expected, string actual)
    {
        _checks++;
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            _failures++;
            Console.WriteLine($"FAIL {label} expected={Quote(expected)} actual={Quote(actual)}");
        }
    }

    private static string Describe(IEnumerable<KeyValuePair<ulong, int>> map) =>
        "{" + string.Join(", ", map.Select(static pair => $"{pair.Key}={pair.Value}")) + "}";

    private static string Quote(string value) =>
        "\"" + value.Replace("\t", "\\t").Replace("\n", "\\n") + "\"";
}
