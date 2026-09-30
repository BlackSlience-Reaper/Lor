using System;
using System.Globalization;
using System.Linq;

namespace LibraryOfRuina.framework.encounters;

/// <summary>
/// 遭遇自定义状态（<c>EncounterModel.SaveCustomState</c>/<c>LoadCustomState</c>）的读写辅助。
/// <para>
/// 原版只在战斗胜利、房间标记为 pre-finished 后调用 <c>SaveCustomState</c>，结果写进
/// <c>SerializableRoom.EncounterState</c>：本地存档走 JSON，联机的存档同步走 <c>PacketWriter</c>，两者都按字典的枚举顺序写出。
/// 继续这局时，原版用 <c>ToMutable()</c> 新建遭遇再调用 <c>LoadCustomState</c>。所以键名、值的文本和写入顺序都是存档格式，
/// 读取时的缺省值和钳制范围是读旧档的兼容规则；这里只收拢解析，不决定这些规则，写入仍由各遭遇按自己的顺序构造字典。
/// </para>
/// <para>
/// 数字有两种写法，不能混用：
/// <list type="bullet">
/// <item><see cref="ReadInt(string, int)"/> 等不带 Invariant 的方法按当前区域性解析，与 <c>int.ToString()</c> 写入配对
/// （艺术、历史、技术、语言、文学、自然层）。</item>
/// <item><see cref="ReadInvariantInt"/> 与 <see cref="FormatInvariant(int)"/> 等按不变区域性（哲学、社会层）。</item>
/// </list>
/// 游戏启动时把默认区域性设成不变区域性，两种写法平常结果相同；但换一种写法就改变了存档格式对区域性的依赖，所以保持各遭遇原来的写法。
/// 布尔值都用 <c>bool.ToString()</c>（"True"/"False"）写入、<c>bool.TryParse</c> 读取，与区域性无关。
/// </para>
/// </summary>
internal readonly struct EncounterStateBag
{
    private readonly IReadOnlyDictionary<string, string> _state;

    public EncounterStateBag(IReadOnlyDictionary<string, string> state)
    {
        _state = state;
    }

    /// <summary>键缺失或不能按当前区域性解析为整数时返回 <paramref name="fallback"/>。</summary>
    public int ReadInt(string key, int fallback = 0) =>
        _state.TryGetValue(key, out string? text) && int.TryParse(text, out int value)
            ? value
            : fallback;

    /// <summary>
    /// <see cref="ReadInt(string, int)"/> 的结果（包括缺省值）再钳制到 [<paramref name="min"/>, <paramref name="max"/>]。
    /// </summary>
    public int ReadClampedInt(string key, int fallback, int min, int max) =>
        Math.Clamp(ReadInt(key, fallback), min, max);

    /// <summary>键缺失或不是 "True"/"False"（不区分大小写，允许首尾空白）时返回 <paramref name="fallback"/>。</summary>
    public bool ReadBool(string key, bool fallback = false) =>
        _state.TryGetValue(key, out string? text) && bool.TryParse(text, out bool value)
            ? value
            : fallback;

    /// <summary>只有键存在且能解析时返回 true；用于“没有这个键”要走另一条兼容分支的读取。</summary>
    public bool TryReadBool(string key, out bool value)
    {
        value = false;
        return _state.TryGetValue(key, out string? text) && bool.TryParse(text, out value);
    }

    /// <summary>键缺失时返回空串；值为 null 时也返回空串。</summary>
    public string ReadString(string key) =>
        _state.TryGetValue(key, out string? value)
            ? value ?? string.Empty
            : string.Empty;

    /// <summary>按不变区域性解析整数，与 <see cref="FormatInvariant(int)"/> 配对。</summary>
    public int ReadInvariantInt(string key, int fallback = 0) =>
        _state.TryGetValue(key, out string? text)
        && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? value
            : fallback;

    /// <summary>按不变区域性解析；键缺失、空串或不能解析时返回 null，与 <see cref="FormatInvariant(ulong?)"/> 配对。</summary>
    public ulong? ReadInvariantNullableUlong(string key) =>
        _state.TryGetValue(key, out string? text)
        && ulong.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong value)
            ? value
            : null;

    /// <summary>
    /// 枚举按底层整数值保存（<see cref="FormatInvariantEnum{T}"/>）。读到未定义的值时返回 <paramref name="fallback"/>；
    /// <see cref="FlagsAttribute"/> 枚举只检查有没有未定义的位，组合值是合法的。
    /// </summary>
    public T ReadInvariantEnum<T>(string key, T fallback) where T : struct, Enum
    {
        int raw = ReadInvariantInt(key, Convert.ToInt32(fallback));
        T value = (T)Enum.ToObject(typeof(T), raw);
        if (typeof(T).IsDefined(typeof(FlagsAttribute), inherit: false))
        {
            int allowedBits = Enum.GetValues<T>()
                .Aggregate(
                    0,
                    static (mask, entry) =>
                        mask | Convert.ToInt32(entry));
            return (raw & ~allowedBits) == 0 ? value : fallback;
        }

        return Enum.IsDefined(value) ? value : fallback;
    }

    /// <summary>
    /// 逗号分隔的整数（<see cref="FormatInvariantIntArray"/>）。键缺失时返回 <paramref name="fallback"/> 的副本；
    /// 空串或全是空白时返回空数组；任何一项不能解析时整组作废，返回 <paramref name="fallback"/> 的副本。
    /// </summary>
    public int[] ReadInvariantIntArray(string key, int[] fallback)
    {
        if (!_state.TryGetValue(key, out string? text))
        {
            return [.. fallback];
        }
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        string[] parts = text.Split(
            ',',
            StringSplitOptions.RemoveEmptyEntries
            | StringSplitOptions.TrimEntries);
        var values = new int[parts.Length];
        for (int index = 0; index < parts.Length; index++)
        {
            if (!int.TryParse(
                    parts[index],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out values[index]))
            {
                return [.. fallback];
            }
        }
        return values;
    }

    public static string FormatInvariant(int value) =>
        value.ToString(CultureInfo.InvariantCulture);

    /// <summary>null 写成空串。</summary>
    public static string FormatInvariant(ulong? value) =>
        value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    public static string FormatInvariantEnum<T>(T value) where T : struct, Enum =>
        Convert.ToInt32(value, CultureInfo.InvariantCulture)
            .ToString(CultureInfo.InvariantCulture);

    public static string FormatInvariantIntArray(IEnumerable<int> values) =>
        string.Join(",", values.Select(static value => FormatInvariant(value)));
}
