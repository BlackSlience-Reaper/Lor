using System;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using LibraryOfRuina.infra.lifecycle;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Rewards;
using LibraryOfRuina.interop;

namespace LibraryOfRuina.content.specialguests;

/// <summary>
/// Hidden, run-scoped persistence carrier for every special guest.  Keeping the
/// framework in one modifier means unlocks and replacement rolls are saved by
/// the native run serializer and stay identical on all peers.
/// </summary>
public sealed class SpecialGuestRunStateModifier : ModifierModel
{
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string LibraryOfRuina_SpecialGuestUnlockedIds { get; set; } = string.Empty;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string LibraryOfRuina_SpecialGuestConsumedIds { get; set; } = string.Empty;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string LibraryOfRuina_SpecialGuestAttemptedRollKeys { get; set; } = string.Empty;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string LibraryOfRuina_SpecialGuestCompletedStoryIds { get; set; } = string.Empty;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string LibraryOfRuina_SpecialGuestSuccessfullyReceivedIds { get; set; } = string.Empty;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string LibraryOfRuina_FullyLiberatedFloorIds { get; set; } = string.Empty;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string LibraryOfRuina_SpecialGuestValues { get; set; } = string.Empty;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string LibraryOfRuina_SpecialGuestActiveId { get; set; } = string.Empty;

    [SavedProperty]
    public int LibraryOfRuina_SpecialGuestCurrentStage { get; set; } = -1;

    [SavedProperty]
    public int LibraryOfRuina_SpecialGuestCompletedStage { get; set; } = -1;

    public override LocString Title => new("modifiers", "SPECIAL_GUEST_RUN_STATE.title");

    public override LocString Description => new("modifiers", "SPECIAL_GUEST_RUN_STATE.description");

    public string? ActiveGuestId => EmptyToNull(LibraryOfRuina_SpecialGuestActiveId);

    public int CurrentStageIndex
    {
        get => LibraryOfRuina_SpecialGuestCurrentStage;
        set => LibraryOfRuina_SpecialGuestCurrentStage = value;
    }

    public int CompletedStageIndex
    {
        get => LibraryOfRuina_SpecialGuestCompletedStage;
        set => LibraryOfRuina_SpecialGuestCompletedStage = value;
    }

    public override bool IsEquivalent(ModifierModel other) => other is SpecialGuestRunStateModifier;

    protected override void AfterRunCreated(RunState runState) => SpecialGuestRegistry.EnsureUnlocks(runState, this);

    protected override void AfterRunLoaded(RunState runState) => SpecialGuestRegistry.EnsureUnlocks(runState, this);

    // 嘉宾战的剧情、阶段推进与奖励增补。载体在进入嘉宾战之前由 GetOrCreate 追加，所以一定在 CombatRoom 构造时的
    // Modifiers 快照里，是战斗内的监听者。局中追加的载体没有经过 OnRunCreated/OnRunLoaded，基类 RunState 会抛异常，
    // 运行状态从钩子参数或当前局取。原版覆写这些钩子的都是遗物、牌、能力，排在 Modifiers 之前，与剧情的先后不变；
    // 排在载体之后的 Modifier、Badge、战斗订阅者及挂在它们身上的 RitsuLib 能力监听者改到剧情之后（重构指导附录 B）。

    // 用 Late：Start 一遍（以及排在前面的 Late，例如石化蟾蜍）结算完再播战前剧情，原版效果的先后与原来的后缀一致。
    public override Task BeforeCombatStartLate() =>
        CurrentRun.State is { } runState
            ? SpecialGuestStageFlow.BeforeCombatStartAsync(runState)
            : Task.CompletedTask;

    public override Task AfterCombatVictory(CombatRoom room) =>
        SpecialGuestStageFlow.AfterCombatVictoryAsync(room.CombatState.RunState, room);

#if STS2_0_111_0
    public override Task BeforeCombatRewardOffered(RewardsSet rewards, CombatRoom room) =>
#else
    public Task BeforeCombatRewardOffered(RewardsSet rewards, CombatRoom room) =>
#endif
        SpecialGuestStageFlow.AugmentRewardsAsync(rewards.Player.RunState, room, rewards);

    public bool IsUnlocked(string guestId) => ParseSet(LibraryOfRuina_SpecialGuestUnlockedIds).Contains(guestId);

    public bool IsConsumed(string guestId) => ParseSet(LibraryOfRuina_SpecialGuestConsumedIds).Contains(guestId);

    public bool HasAttemptedRoll(string rollKey) => ParseSet(LibraryOfRuina_SpecialGuestAttemptedRollKeys).Contains(rollKey);

    public bool IsStoryCompleted(string storyId) => ParseSet(LibraryOfRuina_SpecialGuestCompletedStoryIds).Contains(storyId);

    public bool WasSuccessfullyReceived(string guestId) =>
        ParseSet(LibraryOfRuina_SpecialGuestSuccessfullyReceivedIds).Contains(guestId);

    public bool IsFloorFullyLiberated(string floorId) =>
        ParseSet(LibraryOfRuina_FullyLiberatedFloorIds).Contains(floorId);

    public IReadOnlyCollection<string> SuccessfullyReceivedGuestIds =>
        ParseSet(LibraryOfRuina_SpecialGuestSuccessfullyReceivedIds)
            .OrderBy(static id => id, StringComparer.Ordinal)
            .ToArray();

    public IReadOnlyCollection<string> FullyLiberatedFloorIds =>
        ParseSet(LibraryOfRuina_FullyLiberatedFloorIds)
            .OrderBy(static id => id, StringComparer.Ordinal)
            .ToArray();

    public void MarkUnlocked(string guestId) => LibraryOfRuina_SpecialGuestUnlockedIds = AddToSet(LibraryOfRuina_SpecialGuestUnlockedIds, guestId);

    public void MarkConsumed(string guestId) => LibraryOfRuina_SpecialGuestConsumedIds = AddToSet(LibraryOfRuina_SpecialGuestConsumedIds, guestId);

    public void MarkRollAttempted(string rollKey) => LibraryOfRuina_SpecialGuestAttemptedRollKeys = AddToSet(LibraryOfRuina_SpecialGuestAttemptedRollKeys, rollKey);

    public void MarkStoryCompleted(string storyId) => LibraryOfRuina_SpecialGuestCompletedStoryIds = AddToSet(LibraryOfRuina_SpecialGuestCompletedStoryIds, storyId);

    public void MarkSuccessfullyReceived(string guestId) =>
        LibraryOfRuina_SpecialGuestSuccessfullyReceivedIds = AddToSet(
            LibraryOfRuina_SpecialGuestSuccessfullyReceivedIds,
            guestId);

    public void MarkFloorFullyLiberated(string floorId) =>
        LibraryOfRuina_FullyLiberatedFloorIds = AddToSet(
            LibraryOfRuina_FullyLiberatedFloorIds,
            floorId);

    public void BeginGuest(string guestId)
    {
        LibraryOfRuina_SpecialGuestActiveId = guestId;
        CurrentStageIndex = 0;
        CompletedStageIndex = -1;
    }

    public void AdvanceToStage(int stageIndex)
    {
        CurrentStageIndex = stageIndex;
    }

    public void MarkStageCompleted(int stageIndex)
    {
        CompletedStageIndex = Math.Max(CompletedStageIndex, stageIndex);
    }

    public void ClearActiveGuest()
    {
        LibraryOfRuina_SpecialGuestActiveId = string.Empty;
        CurrentStageIndex = -1;
        CompletedStageIndex = -1;
    }

    public string? GetValue(string key)
    {
        Dictionary<string, string> values = ParseValues(LibraryOfRuina_SpecialGuestValues);
        return values.TryGetValue(key, out string? value) ? value : null;
    }

    public void SetValue(string key, string? value)
    {
        Dictionary<string, string> values = ParseValues(LibraryOfRuina_SpecialGuestValues);
        if (value == null)
        {
            values.Remove(key);
        }
        else
        {
            values[key] = value;
        }

        LibraryOfRuina_SpecialGuestValues = SerializeValues(values);
    }

    public static SpecialGuestRunStateModifier? TryGet(IRunState? runState) =>
        runState?.Modifiers.OfType<SpecialGuestRunStateModifier>().FirstOrDefault();

    public static SpecialGuestRunStateModifier GetOrCreate(RunState runState)
    {
        SpecialGuestRunStateModifier? existing = TryGet(runState);
        if (existing != null)
        {
            return existing;
        }

        SpecialGuestRunStateModifier carrier =
            (SpecialGuestRunStateModifier)ModelDb.Modifier<SpecialGuestRunStateModifier>().ToMutable();
        if (!SpecialGuestRunStateModifierStore.TryAppend(runState, carrier))
        {
            throw new InvalidOperationException("Unable to attach the special-guest run-state carrier.");
        }

        return carrier;
    }

    private static HashSet<string> ParseSet(string serialized) =>
        serialized.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);

    private static string AddToSet(string serialized, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return serialized;
        }

        HashSet<string> values = ParseSet(serialized);
        values.Add(value);
        return string.Join('\n', values.OrderBy(static item => item, StringComparer.Ordinal));
    }

    private static Dictionary<string, string> ParseValues(string serialized)
    {
        Dictionary<string, string> values = new(StringComparer.Ordinal);
        foreach (string line in serialized.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            int separator = line.IndexOf('\t');
            if (separator <= 0)
            {
                continue;
            }

            try
            {
                string key = Decode(line[..separator]);
                string value = Decode(line[(separator + 1)..]);
                values[key] = value;
            }
            catch (FormatException)
            {
                // Ignore a single damaged optional value without invalidating the run.
            }
        }

        return values;
    }

    private static string SerializeValues(IReadOnlyDictionary<string, string> values) =>
        string.Join('\n', values.OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .Select(static pair => Encode(pair.Key) + "\t" + Encode(pair.Value)));

    private static string Encode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));

    private static string Decode(string value) => Encoding.UTF8.GetString(Convert.FromBase64String(value));

    private static string? EmptyToNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
}

internal static class SpecialGuestRunStateModifierStore
{

    public static bool TryAppend(RunState runState, SpecialGuestRunStateModifier carrier)
    {
        if (SpecialGuestRunStateModifier.TryGet(runState) != null)
        {
            return true;
        }

        if (!VanillaPrivate.RunStateModifiers.IsAvailable)
        {
            Log.Error("[SpecialGuest] RunState.Modifiers backing field was not found.");
            return false;
        }

        List<ModifierModel> modifiers = runState.Modifiers.ToList();
        modifiers.Add(carrier);
        VanillaPrivate.RunStateModifiers.Set(runState, modifiers);
        return true;
    }
}
