using System;
using System.Globalization;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Random;

namespace LibraryOfRuina.content.specialguests.Rnfmabj;

/// <summary>
/// Rnfmabj 指令（Prescript）任务的纯规则：每个可取消的计划槽位对应一个任务，每个任务是 4 张指定类型的出牌序列；
/// 所有玩家都按顺序打完当前序列，就取消最左边一个可取消的意图并进入下一个任务。
/// 本类不读写怪物状态；状态是 <see cref="Rnfmabj"/> 上的战斗内属性（序列编码、当前任务、各玩家进度），
/// 由 <c>Rnfmabj.Directives.cs</c> 按这里的规则改写。进度与玩家列表按网络 ID 升序编码，联机各端得到相同字符串。
/// </summary>
internal static class RnfmabjDirectiveTracker
{
    internal const int ChecksPerTask = 4;

    private static readonly CardType[][] FirstRoundDirectiveSequences =
    [
        [
            CardType.Attack,
            CardType.Attack,
            CardType.Skill,
            CardType.Skill,
        ],
        [
            CardType.Power,
            CardType.Power,
            CardType.Skill,
            CardType.Skill,
        ],
    ];

    private static readonly CardType[][] SecondRoundDirectiveSequences =
    [
        [
            CardType.Skill,
            CardType.Attack,
            CardType.Power,
            CardType.Power,
        ],
        [
            CardType.Attack,
            CardType.Skill,
            CardType.Power,
            CardType.Power,
        ],
    ];

    private static readonly CardType[] RandomDirectiveCardTypes =
    [
        CardType.Attack,
        CardType.Skill,
        CardType.Power,
    ];

    private static readonly CardType[] RandomDirectiveNonPowerCardTypes =
    [
        CardType.Attack,
        CardType.Skill,
    ];

    internal static bool IsCancelableDirectiveMove(RnfmabjMove move) =>
        move is RnfmabjMove.ExecuteAttack or RnfmabjMove.ExecuteGuard;

    internal static bool IsDirectiveCardType(CardType cardType) =>
        cardType is CardType.Attack or CardType.Skill or CardType.Power;

    /// <summary>
    /// 为 <paramref name="taskCount"/> 个任务生成序列编码（每个任务 <see cref="ChecksPerTask"/> 个）。
    /// 第 1、2 回合的前两个任务用固定序列、不消耗随机数；其余逐格掷 <paramref name="rng"/>，同一任务里最多一张能力牌。
    /// </summary>
    internal static int[] RollSequenceCodes(int round, int taskCount, Rng rng)
    {
        CardType[][]? fixedSequences = round switch
        {
            1 => FirstRoundDirectiveSequences,
            2 => SecondRoundDirectiveSequences,
            _ => null,
        };
        var codes = new int[taskCount * ChecksPerTask];
        for (int task = 0; task < taskCount; task++)
        {
            CardType[]? fixedSequence = fixedSequences != null
                && task < fixedSequences.Length
                    ? fixedSequences[task]
                    : null;
            bool alreadyRequiresPower = false;
            for (int step = 0; step < ChecksPerTask; step++)
            {
                CardType cardType = fixedSequence?[step]
                    ?? RollRandomDirectiveCardType(
                        rng,
                        alreadyRequiresPower);
                alreadyRequiresPower |= cardType == CardType.Power;
                codes[task * ChecksPerTask + step] = (int)cardType;
            }
        }

        return codes;
    }

    internal static int GetTaskCount(int sequenceLength, int[] sequenceCodes) =>
        sequenceLength > 0
            ? sequenceCodes.Length / sequenceLength
            : 0;

    /// <summary>存档里的指令状态是否自洽；没有任务（长度 0 且编码为空）也算自洽。</summary>
    internal static bool HasUsableState(
        int sequenceLength,
        int[] sequenceCodes,
        int currentTaskIndex)
    {
        if (sequenceLength == 0)
        {
            return sequenceCodes.Length == 0;
        }

        int taskCount = GetTaskCount(sequenceLength, sequenceCodes);
        return sequenceLength > 0
            && sequenceCodes.Length % sequenceLength == 0
            && taskCount > 0
            && currentTaskIndex >= 0
            && currentTaskIndex < taskCount
            && sequenceCodes.All(code =>
                IsDirectiveCardType((CardType)code));
    }

    internal static bool TryGetSequence(
        int sequenceLength,
        int[] sequenceCodes,
        int currentTaskIndex,
        out CardType[] sequence)
    {
        sequence = [];
        if (!HasUsableState(sequenceLength, sequenceCodes, currentTaskIndex)
            || sequenceLength <= 0)
        {
            return false;
        }

        int offset = currentTaskIndex * sequenceLength;
        if (offset < 0
            || offset + sequenceLength > sequenceCodes.Length)
        {
            return false;
        }

        sequence = new CardType[sequenceLength];
        for (int step = 0; step < sequenceLength; step++)
        {
            sequence[step] = (CardType)sequenceCodes[offset + step];
        }

        return true;
    }

    internal static string FormatRequiredPlayerNetIds(IEnumerable<ulong> netIds) =>
        string.Join(
            ";",
            netIds
                .Distinct()
                .OrderBy(static netId => netId)
                .Select(static netId =>
                    netId.ToString(CultureInfo.InvariantCulture)));

    internal static IReadOnlyList<ulong> ParseRequiredPlayerNetIds(string serialized)
    {
        var result = new List<ulong>();
        foreach (string entry in serialized.Split(
                     ';',
                     StringSplitOptions.RemoveEmptyEntries
                     | StringSplitOptions.TrimEntries))
        {
            if (ulong.TryParse(
                    entry,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out ulong netId))
            {
                result.Add(netId);
            }
        }

        result.Sort();
        return result;
    }

    /// <summary>解析 <c>网络ID:进度;…</c>，进度夹到 [0, 序列长度]；格式不对的条目跳过。</summary>
    internal static Dictionary<ulong, int> ParseProgress(
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

    internal static string FormatProgress(IReadOnlyDictionary<ulong, int> progress) =>
        string.Join(
            ";",
            progress.OrderBy(static pair => pair.Key)
                .Select(static pair =>
                    pair.Key.ToString(CultureInfo.InvariantCulture)
                    + ":"
                    + pair.Value.ToString(CultureInfo.InvariantCulture)));

    private static CardType RollRandomDirectiveCardType(
        Rng rng,
        bool alreadyRequiresPower)
    {
        CardType[] cardTypes = alreadyRequiresPower
            ? RandomDirectiveNonPowerCardTypes
            : RandomDirectiveCardTypes;
        return cardTypes[rng.NextInt(cardTypes.Length)];
    }
}

internal readonly record struct RnfmabjDirectiveSnapshot(
    bool IsVisible,
    CardType[] Sequence,
    int Progress,
    int TaskNumber,
    int TaskCount,
    int CompletedPlayers,
    int RequiredPlayers,
    bool LocalPlayerCompleted,
    bool CurrentTaskCompleted,
    string Fingerprint)
{
    public static RnfmabjDirectiveSnapshot Hidden { get; } = new(
        IsVisible: false,
        Sequence: [],
        Progress: 0,
        TaskNumber: 0,
        TaskCount: 0,
        CompletedPlayers: 0,
        RequiredPlayers: 0,
        LocalPlayerCompleted: false,
        CurrentTaskCompleted: false,
        Fingerprint: "hidden");
}
