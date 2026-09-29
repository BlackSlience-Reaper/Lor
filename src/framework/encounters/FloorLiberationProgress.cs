using System;
using System.Linq;
using LibraryOfRuina.specialguests;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.framework.encounters;

/// <summary>
/// Stable public IDs used by liberation encounters and event unlock conditions.
/// String IDs allow later floors, such as Literature, to join the contract
/// without changing a serialized enum.
/// </summary>
public static class LiberationFloorIds
{
    public const string History = "HISTORY";
    public const string Technology = "TECHNOLOGY";
    public const string Literature = "LITERATURE";
    public const string Art = "ART";
    public const string Language = "LANGUAGE";
    public const string Philosophy = "PHILOSOPHY";
    public const string Social = "SOCIAL";
    public const string Natural = "NATURAL";

    public static IReadOnlyList<string> FirstAct { get; } =
        [History, Technology, Literature];

    public static IReadOnlyList<string> SecondAct { get; } =
        [Art, Language, Philosophy];
}

/// <summary>
/// Implemented by liberation battles so the common victory hook can publish
/// their completion without knowing each encounter type.
/// </summary>
public interface IFloorLiberationEncounter
{
    string LiberationFloorId { get; }

    bool IsFullyLiberated { get; }
}

/// <summary>
/// Public, run-scoped liberation completion information for event conditions.
/// </summary>
public static class FloorLiberationProgress
{
    private const string LegacyHistoryValueKey =
        "kali.history-floor-liberation-complete";

    public static IReadOnlyCollection<string> GetFullyLiberatedFloorIds(
        IRunState? runState)
    {
        SpecialGuestRunStateModifier? state =
            SpecialGuestRunStateModifier.TryGet(runState);
        if (state == null)
        {
            return Array.Empty<string>();
        }

        HashSet<string> floorIds = state.FullyLiberatedFloorIds
            .ToHashSet(StringComparer.Ordinal);
        if (string.Equals(
                state.GetValue(LegacyHistoryValueKey),
                bool.TrueString,
                StringComparison.Ordinal))
        {
            floorIds.Add(LiberationFloorIds.History);
        }

        return floorIds.OrderBy(static id => id, StringComparer.Ordinal)
            .ToArray();
    }

    public static bool IsFullyLiberated(
        IRunState? runState,
        string floorId)
    {
        if (string.IsNullOrWhiteSpace(floorId))
        {
            return false;
        }

        SpecialGuestRunStateModifier? state =
            SpecialGuestRunStateModifier.TryGet(runState);
        if (state == null)
        {
            return false;
        }

        return state.IsFloorFullyLiberated(floorId)
               || (string.Equals(
                       floorId,
                       LiberationFloorIds.History,
                       StringComparison.Ordinal)
                   && string.Equals(
                       state.GetValue(LegacyHistoryValueKey),
                       bool.TrueString,
                       StringComparison.Ordinal));
    }

    public static bool IsAnyFullyLiberated(
        IRunState? runState,
        IEnumerable<string> floorIds)
    {
        ArgumentNullException.ThrowIfNull(floorIds);
        return floorIds.Any(floorId => IsFullyLiberated(runState, floorId));
    }

    internal static void MarkFullyLiberated(
        IRunState? runState,
        string floorId)
    {
        if (runState is not RunState concreteRunState
            || string.IsNullOrWhiteSpace(floorId))
        {
            return;
        }

        SpecialGuestRunStateModifier state =
            SpecialGuestRunStateModifier.GetOrCreate(concreteRunState);
        state.MarkFloorFullyLiberated(floorId);

        // Mirror the removed Kali-specific key so saves made by transitional
        // builds remain readable in both directions.
        if (string.Equals(
                floorId,
                LiberationFloorIds.History,
                StringComparison.Ordinal))
        {
            state.SetValue(LegacyHistoryValueKey, bool.TrueString);
        }
    }
}
