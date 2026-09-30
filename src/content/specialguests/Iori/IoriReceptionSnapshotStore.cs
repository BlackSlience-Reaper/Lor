using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.content.specialguests.Iori;

/// <summary>
/// 伊织一阶段撤离时的接待快照：情绪、意图上限、姿态与正面能力，存成 JSON 放在跑图的嘉宾载体
/// （<see cref="SpecialGuestRunStateModifier"/>）里，二阶段是新的战斗、新的生物，只能靠它延续。
/// JSON 的字段名就是下面记录的属性名，<c>Version</c> 不等于 1 的快照整体忽略；改记录会改变已有存档能否读回。
/// </summary>
internal static class IoriReceptionSnapshotStore
{
    internal static void Save(IoriMonsterBase iori)
    {
        Creature creature = iori.Creature;
        if (creature.CombatState?.RunState is not RunState runState)
        {
            return;
        }

        int round = creature.CombatState.RoundNumber;
        var snapshot = new IoriReceptionSnapshot(
            Version: 1,
            iori.EmotionLevel,
            iori.EmotionUnits,
            iori.IntentCapacity,
            iori.LevelFiveRoundCounter,
            iori.CurrentStance,
            iori.HasExpandedRoundTwoCapacity,
            ReceptionRound: iori.ReceptionRoundOffset + round,
            iori.LastRegularMove,
            iori.SelectedStanceMask,
            PositivePowers: creature.Powers
                .Where(static power =>
                    power.TypeForCurrentAmount == PowerType.Buff)
                .OrderBy(static power => power.Id.ToString(), StringComparer.Ordinal)
                .ThenBy(static power => power.Amount)
                .Select(power => IoriPowerSnapshot.Capture(power, round))
                .ToArray());

        SpecialGuestRunStateModifier.GetOrCreate(runState).SetValue(
            IoriSpecialGuestIds.SnapshotValueKey,
            JsonSerializer.Serialize(snapshot));
    }

    /// <summary>读回快照；没有、损坏或版本不符时返回 null（损坏会记错误日志）。</summary>
    internal static IoriReceptionSnapshot? TryLoad(IoriMonsterBase iori)
    {
        if (iori.Creature.CombatState?.RunState is not RunState runState)
        {
            return null;
        }

        string? json = SpecialGuestRunStateModifier.TryGet(runState)
            ?.GetValue(IoriSpecialGuestIds.SnapshotValueKey);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        IoriReceptionSnapshot? snapshot;
        try
        {
            snapshot = JsonSerializer.Deserialize<IoriReceptionSnapshot>(json);
        }
        catch (Exception exception)
        {
            Log.Error("[Iori] Invalid reception snapshot: " + exception);
            return null;
        }

        if (snapshot == null || snapshot.Version != 1)
        {
            return null;
        }

        return snapshot;
    }

    internal static Task RestorePower(Creature owner, IoriPowerSnapshot snapshot)
    {
        PowerModel? canonical;
        try
        {
            canonical = ModelDb.GetByIdOrNull<PowerModel>(
                ModelId.Deserialize(snapshot.ModelId));
        }
        catch (Exception exception)
        {
            Log.Warn(
                "[Iori] Could not parse saved power id "
                + snapshot.ModelId
                + ": "
                + exception.Message);
            return Task.CompletedTask;
        }

        if (canonical == null || snapshot.Amount == 0)
        {
            return Task.CompletedTask;
        }

        PowerModel mutable = canonical.ToMutable();
        if (mutable is LibraryDurationPowerModel duration)
        {
            duration.SetTurnsRemaining(
                snapshot.DurationTurns,
                notifyDisplay: false);
        }

        // Restore the serialized state directly. Replaying BeforeApplied for
        // powers such as temporary Strength would duplicate their companion
        // power, which is already present in this same positive-power snapshot.
        mutable.Applier = owner;
        mutable.ApplyInternal(
            owner,
            snapshot.Amount,
            silent: true);
        mutable.SkipNextDurationTick = snapshot.SkipNextDurationTick;

        if (mutable is LibraryTurnsPowerModel turnsPower)
        {
            int round = owner.CombatState?.RoundNumber ?? 0;
            turnsPower.AmountPlan = snapshot.TurnPlan
                .GroupBy(entry =>
                    round + Math.Max(0, entry.RoundOffset))
                .ToDictionary(
                    static group => group.Key,
                    static group => group.Sum(entry => entry.Amount))
                .ToSortedDictionary();
        }

        return Task.CompletedTask;
    }
}

internal sealed record IoriReceptionSnapshot(
    int Version,
    int EmotionLevel,
    int EmotionUnits,
    int IntentCapacity,
    int LevelFiveRoundCounter,
    IoriStance CurrentStance,
    bool HasExpandedRoundTwoCapacity,
    int ReceptionRound,
    int LastRegularMove,
    int SelectedStanceMask,
    IReadOnlyList<IoriPowerSnapshot> PositivePowers);

internal sealed record IoriPowerSnapshot(
    string ModelId,
    int Amount,
    bool SkipNextDurationTick,
    int DurationTurns,
    IReadOnlyList<IoriTurnPlanSnapshot> TurnPlan)
{
    internal static IoriPowerSnapshot Capture(
        PowerModel power,
        int currentRound)
    {
        int durationTurns = power is LibraryDurationPowerModel duration
            ? duration.TurnsRemaining
            : 0;
        IoriTurnPlanSnapshot[] plan = power is LibraryTurnsPowerModel turns
            ? turns.AmountPlan
                .OrderBy(static entry => entry.Key)
                .Select(entry => new IoriTurnPlanSnapshot(
                    Math.Max(0, entry.Key - currentRound),
                    entry.Value))
                .ToArray()
            : [];
        return new(
            power.Id.ToString(),
            power.Amount,
            power.SkipNextDurationTick,
            durationTurns,
            plan);
    }
}

internal sealed record IoriTurnPlanSnapshot(
    int RoundOffset,
    int Amount);

internal static class SortedDictionaryExtensions
{
    internal static SortedDictionary<TKey, TValue> ToSortedDictionary<TKey, TValue>(
        this IDictionary<TKey, TValue> source)
        where TKey : notnull => new(source);
}
