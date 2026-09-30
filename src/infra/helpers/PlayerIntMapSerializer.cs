using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace LibraryOfRuina.infra.helpers;

/// <summary>
/// “玩家网络 ID → 整数”映射与 <c>网络ID:值;网络ID:值</c> 字符串的互转。
/// <para>
/// 这些字符串写进遭遇的自定义存档、能力与怪物的战斗内属性，也在联机两端各自生成后比对，所以是存档格式：
/// 写出按网络 ID 升序、用不变文化、不带空白；解析按 <c>;</c> 分条（去掉空条目与首尾空白），
/// 每条在第一个 <c>:</c> 处分开，网络 ID 只接受纯数字（<see cref="NumberStyles.None"/>），
/// 值按 <see cref="NumberStyles.Integer"/> 解析（允许正负号与首尾空白）；格式不对的条目跳过，重复的网络 ID 以最后一条为准。
/// </para>
/// <para>
/// 值的取舍（夹到范围、丢掉非正数）各调用方不同，由调用方选择 <see cref="ParseClamped"/> 或 <see cref="ParsePositive"/>。
/// 改动任何细节之前先跑 <c>tools/PlayerIntMapCheck</c>，它用合并前的四份实现逐字节比对。
/// </para>
/// </summary>
internal static class PlayerIntMapSerializer
{
    public static string Format(IEnumerable<KeyValuePair<ulong, int>> values) =>
        string.Join(
            ';',
            values.OrderBy(static pair => pair.Key)
                .Select(static pair =>
                    pair.Key.ToString(CultureInfo.InvariantCulture)
                    + ":"
                    + pair.Value.ToString(CultureInfo.InvariantCulture)));

    /// <summary>解析，值夹到 [<paramref name="minValue"/>, <paramref name="maxValue"/>]。</summary>
    public static Dictionary<ulong, int> ParseClamped(string serialized, int minValue, int maxValue)
    {
        var result = new Dictionary<ulong, int>();
        foreach (string entry in SplitEntries(serialized))
        {
            if (TryParseEntry(entry, out ulong netId, out int value))
            {
                result[netId] = Math.Clamp(value, minValue, maxValue);
            }
        }

        return result;
    }

    /// <summary>解析，值不为正的条目整条跳过（不覆盖同一网络 ID 之前的条目）。</summary>
    public static Dictionary<ulong, int> ParsePositive(string serialized)
    {
        var result = new Dictionary<ulong, int>();
        foreach (string entry in SplitEntries(serialized))
        {
            if (TryParseEntry(entry, out ulong netId, out int value) && value > 0)
            {
                result[netId] = value;
            }
        }

        return result;
    }

    private static string[] SplitEntries(string serialized) =>
        serialized.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static bool TryParseEntry(string entry, out ulong netId, out int value)
    {
        int separator = entry.IndexOf(':');
        if (separator <= 0
            || !ulong.TryParse(entry[..separator], NumberStyles.None, CultureInfo.InvariantCulture, out netId))
        {
            netId = 0;
            value = 0;
            return false;
        }

        return int.TryParse(entry[(separator + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }
}
