using System;
using System.Globalization;
using System.Linq;

namespace LibraryOfRuina.specialguests.Iori;

/// <summary>
/// 伊织姿态的纯规则：可选姿态表、“一轮内不重复”的选择掩码、姿态对应的伤害类型与防御姿态的荆棘层数、
/// 钝击姿态给每位玩家施加的束缚链贡献的编码。不读写怪物状态；状态（当前姿态、掩码、贡献字符串）
/// 仍是 <see cref="IoriMonsterBase"/> 上的 SavedProperty，由 <c>IoriMonsterBase.Stance.cs</c> 按这些规则改写。
/// </summary>
internal static class IoriStanceController
{
    private static readonly IoriStance[] AllStances =
    [
        IoriStance.Slash,
        IoriStance.Pierce,
        IoriStance.Blunt,
        IoriStance.Defense,
    ];

    private static readonly IoriStance[] OffensiveStances =
    [
        IoriStance.Slash,
        IoriStance.Pierce,
        IoriStance.Blunt,
    ];

    /// <summary>二阶段没有防御姿态。</summary>
    internal static IoriStance[] SelectableStances(bool isSecondStage) =>
        isSecondStage ? OffensiveStances : AllStances;

    /// <summary>
    /// 下一个姿态的候选：可选姿态中排除当前姿态，再排除本轮已选过的；本轮全选过（掩码覆盖全部可选姿态）视为新一轮。
    /// 排除后为空时退回“除当前姿态外的全部可选姿态”。
    /// </summary>
    internal static IoriStance[] GetPreferredNextStances(
        IoriStance[] selectable,
        IoriStance current,
        int selectedStanceMask)
    {
        int selectableMask = GetStanceMask(selectable);
        int selectedMask = selectedStanceMask & selectableMask;
        if (selectedMask == selectableMask)
        {
            selectedMask = 0;
        }

        IoriStance[] unselected = selectable
            .Where(stance => stance != current)
            .Where(stance => (selectedMask & GetStanceBit(stance)) == 0)
            .ToArray();
        return unselected.Length > 0
            ? unselected
            : selectable.Where(stance => stance != current).ToArray();
    }

    /// <summary>记下本次选中的姿态；可选姿态都选过一遍后清零，开始新一轮。</summary>
    internal static int RecordStanceSelection(
        IoriStance[] selectable,
        int selectedStanceMask,
        IoriStance stance)
    {
        int selectableMask = GetStanceMask(selectable);
        int mask = (selectedStanceMask & selectableMask) | GetStanceBit(stance);
        if ((mask & selectableMask) == selectableMask)
        {
            mask = 0;
        }

        return mask;
    }

    internal static int ResolveDefenseThorns() =>
        new IoriAscensionValue(7, 9).Resolve();

    /// <summary>姿态招式的伤害类型；防御姿态与无姿态按斩击。</summary>
    internal static LibraryDamageType ResolveDamageType(
        IoriStance stance) => stance switch
    {
        IoriStance.Slash => LibraryDamageType.Slash,
        IoriStance.Pierce => LibraryDamageType.Pierce,
        IoriStance.Blunt => LibraryDamageType.Blunt,
        _ => LibraryDamageType.Slash,
    };

    /// <summary>解析 <c>网络ID:层数;…</c>；格式不对或层数不为正的条目跳过。</summary>
    internal static Dictionary<ulong, int> ParseContributions(string serialized)
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

    /// <summary>按网络 ID 升序编码，联机各端得到相同的字符串。</summary>
    internal static string SerializeContributions(
        IReadOnlyDictionary<ulong, int> contributions) =>
        string.Join(
            ';',
            contributions
                .OrderBy(static pair => pair.Key)
                .Select(static pair =>
                    pair.Key.ToString(CultureInfo.InvariantCulture)
                    + ":"
                    + pair.Value.ToString(CultureInfo.InvariantCulture)));

    private static int GetStanceMask(IEnumerable<IoriStance> stances) =>
        stances.Aggregate(0, (mask, stance) => mask | GetStanceBit(stance));

    private static int GetStanceBit(IoriStance stance) =>
        stance == IoriStance.None ? 0 : 1 << (int)stance;
}
