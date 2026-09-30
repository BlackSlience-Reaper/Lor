using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PlayerIntMapCheck;

/// <summary>
/// 合并进 PlayerIntMapSerializer 之前的四份实现，逐字取自 main 1c1e003f，只把实例成员改成参数。
/// 这是存档格式的参照，不要改。
/// </summary>
internal static class Legacy
{
    // src/content/liberation/Social/SocialFloorLiberationEncounter.cs SerializePlayerValues
    public static string SocialSerializePlayerValues(
        IEnumerable<KeyValuePair<ulong, int>> values) =>
        string.Join(
            ";",
            values.OrderBy(static entry => entry.Key).Select(entry =>
                entry.Key.ToString(CultureInfo.InvariantCulture)
                + ":"
                + Math.Max(0, entry.Value).ToString(
                    CultureInfo.InvariantCulture)));

    // src/content/liberation/Social/SocialFloorLiberationEncounter.cs ParsePlayerValues
    public static IReadOnlyDictionary<ulong, int> SocialParsePlayerValues(
        string serialized)
    {
        var values = new Dictionary<ulong, int>();
        foreach (string entry in serialized.Split(
                     ';',
                     StringSplitOptions.RemoveEmptyEntries
                     | StringSplitOptions.TrimEntries))
        {
            string[] parts = entry.Split(':', 2);
            if (parts.Length == 2
                && ulong.TryParse(
                    parts[0],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out ulong netId)
                && int.TryParse(
                    parts[1],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int value))
            {
                values[netId] = Math.Max(0, value);
            }
        }
        return values;
    }

    // src/content/specialguests/Xiao/XiaoSpecialGuestPowers.cs SetCount 的写出部分（CardLimit = 12）
    public static string XiaoFormat(Dictionary<ulong, int> counts) =>
        string.Join(
            ';',
            counts.OrderBy(static pair => pair.Key)
                .Select(static pair =>
                    pair.Key.ToString(CultureInfo.InvariantCulture)
                    + ":"
                    + pair.Value.ToString(CultureInfo.InvariantCulture)));

    // src/content/specialguests/Xiao/XiaoSpecialGuestPowers.cs ParseCounts
    public static Dictionary<ulong, int> XiaoParseCounts(string cardsPlayedByPlayerNetId, int cardLimit)
    {
        var counts = new Dictionary<ulong, int>();
        foreach (string entry in cardsPlayedByPlayerNetId.Split(
                     ';',
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int separator = entry.IndexOf(':');
            if (separator <= 0
                || !ulong.TryParse(
                    entry[..separator],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out ulong netId)
                || !int.TryParse(
                    entry[(separator + 1)..],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int count))
            {
                continue;
            }

            counts[netId] = Math.Clamp(count, 0, cardLimit);
        }

        return counts;
    }

    // src/content/specialguests/Rnfmabj/RnfmabjDirectiveTracker.cs ParseProgress
    public static Dictionary<ulong, int> RnfmabjParseProgress(
        string serialized,
        int sequenceLength)
    {
        var result = new Dictionary<ulong, int>();
        foreach (string entry in serialized.Split(
                     ';',
                     StringSplitOptions.RemoveEmptyEntries
                     | StringSplitOptions.TrimEntries))
        {
            int separator = entry.IndexOf(':');
            if (separator <= 0
                || !ulong.TryParse(
                    entry[..separator],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out ulong netId)
                || !int.TryParse(
                    entry[(separator + 1)..],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int value))
            {
                continue;
            }

            result[netId] = Math.Clamp(
                value,
                0,
                Math.Max(0, sequenceLength));
        }

        return result;
    }

    // src/content/specialguests/Rnfmabj/RnfmabjDirectiveTracker.cs FormatProgress
    public static string RnfmabjFormatProgress(IReadOnlyDictionary<ulong, int> progress) =>
        string.Join(
            ";",
            progress.OrderBy(static pair => pair.Key)
                .Select(static pair =>
                    pair.Key.ToString(CultureInfo.InvariantCulture)
                    + ":"
                    + pair.Value.ToString(CultureInfo.InvariantCulture)));

    // src/content/specialguests/Iori/IoriStanceController.cs ParseContributions
    public static Dictionary<ulong, int> IoriParseContributions(string serialized)
    {
        var result = new Dictionary<ulong, int>();
        foreach (string item in serialized.Split(
                     ';',
                     StringSplitOptions.RemoveEmptyEntries
                     | StringSplitOptions.TrimEntries))
        {
            int separator = item.IndexOf(':');
            if (separator <= 0
                || !ulong.TryParse(
                    item[..separator],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out ulong netId)
                || !int.TryParse(
                    item[(separator + 1)..],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int amount)
                || amount <= 0)
            {
                continue;
            }

            result[netId] = amount;
        }

        return result;
    }

    // src/content/specialguests/Iori/IoriStanceController.cs SerializeContributions
    public static string IoriSerializeContributions(
        IReadOnlyDictionary<ulong, int> contributions) =>
        string.Join(
            ';',
            contributions
                .OrderBy(static pair => pair.Key)
                .Select(static pair =>
                    pair.Key.ToString(CultureInfo.InvariantCulture)
                    + ":"
                    + pair.Value.ToString(CultureInfo.InvariantCulture)));
}
