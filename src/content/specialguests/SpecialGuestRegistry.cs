using System;
using System.Linq;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.content.specialguests;

/// <summary>
/// Registration and deterministic replacement policy for special guests.
/// Definitions are intentionally absent from the ordinary event pools.
/// </summary>
public static class SpecialGuestRegistry
{
    private static readonly object Sync = new();
    private static readonly Dictionary<string, SpecialGuestDefinition> Definitions =
        new(StringComparer.Ordinal);

    public static IReadOnlyList<SpecialGuestDefinition> All
    {
        get
        {
            lock (Sync)
            {
                return Definitions.Values.OrderBy(static definition => definition.Id, StringComparer.Ordinal).ToArray();
            }
        }
    }

    public static void Register(SpecialGuestDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (string.IsNullOrWhiteSpace(definition.Id))
        {
            throw new ArgumentException("A special guest must have a stable non-empty ID.", nameof(definition));
        }

        if (definition.Stages.Count == 0)
        {
            throw new ArgumentException($"Special guest {definition.Id} must contain at least one stage.", nameof(definition));
        }

        lock (Sync)
        {
            if (Definitions.TryGetValue(definition.Id, out SpecialGuestDefinition? existing)
                && !ReferenceEquals(existing, definition))
            {
                throw new InvalidOperationException($"Special guest ID {definition.Id} was registered twice.");
            }

            Definitions[definition.Id] = definition;
        }
    }

    public static bool TryGet(string id, out SpecialGuestDefinition definition)
    {
        lock (Sync)
        {
            return Definitions.TryGetValue(id, out definition!);
        }
    }

    public static SpecialGuestDefinition Get(string id) =>
        TryGet(id, out SpecialGuestDefinition? definition)
            ? definition
            : throw new KeyNotFoundException($"Special guest {id} is not registered.");

    public static void EnsureUnlocks(IRunState runState, SpecialGuestRunStateModifier? state = null)
    {
        if (runState is not RunState concreteRunState)
        {
            return;
        }

        state ??= SpecialGuestRunStateModifier.TryGet(concreteRunState);
        if (state == null)
        {
            return;
        }

        foreach (SpecialGuestDefinition definition in All)
        {
            if (state.IsUnlocked(definition.Id))
            {
                continue;
            }

            bool unlocked;
            try
            {
                unlocked = definition.UnlockCondition(runState);
            }
            catch (Exception exception)
            {
                Log.Error($"[SpecialGuest] Unlock predicate failed for {definition.Id}: {exception}");
                continue;
            }

            if (!unlocked)
            {
                continue;
            }

            state.MarkUnlocked(definition.Id);
            Log.Info($"[SpecialGuest] Permanently unlocked {definition.Id} for this run.");
        }
    }

    /// <summary>
    /// Applies one coordinate-scoped replacement attempt.  The hash contains no
    /// mutable RNG state, so repeated calls, save/load, and multiplayer peers
    /// resolve the same result without advancing a shared run RNG.
    /// </summary>
    public static EventModel TryReplaceNextEvent(IRunState runState, EventModel finalEvent)
    {
        ArgumentNullException.ThrowIfNull(finalEvent);

        if (runState is not RunState concreteRunState
            || finalEvent is SpecialGuestEventBase)
        {
            return finalEvent;
        }

        SpecialGuestRunStateModifier? state =
            SpecialGuestRunStateModifier.TryGet(concreteRunState);
        EnsureUnlocks(runState, state);

        string rollKey = BuildRollKey(runState, finalEvent);
        string replacementKey = "replacement." + rollKey;
        if (state != null)
        {
            string? previousGuestId = state.GetValue(replacementKey);
            if (previousGuestId != null
                && TryGet(previousGuestId, out SpecialGuestDefinition? previousGuest))
            {
                if (string.Equals(
                        state.GetValue("resolved." + previousGuestId),
                        "true",
                        StringComparison.Ordinal))
                {
                    return finalEvent;
                }

                if (!CanAppear(previousGuest, runState))
                {
                    return finalEvent;
                }

                EventModel previousReplacement = previousGuest.EventFactory();
                previousReplacement.AssertCanonical();
                return previousReplacement;
            }

            if (state.HasAttemptedRoll(rollKey))
            {
                return finalEvent;
            }
        }

        var candidates = new List<SpecialGuestDefinition>();
        foreach (SpecialGuestDefinition definition in All)
        {
            bool unlocked = state?.IsUnlocked(definition.Id)
                            ?? IsUnlockConditionMet(definition, runState);
            if (!unlocked
                || (state?.IsConsumed(definition.Id) ?? false)
                || !CanAppear(definition, runState))
            {
                continue;
            }

            candidates.Add(definition);
        }

        if (candidates.Count == 0)
        {
            return finalEvent;
        }

        state ??= SpecialGuestRunStateModifier.GetOrCreate(concreteRunState);
        EnsureUnlocks(runState, state);
        state.MarkRollAttempted(rollKey);
        int chosenIndex = (int)(StableHash64(rollKey + "|guest") % (ulong)candidates.Count);
        SpecialGuestDefinition chosen = candidates[chosenIndex];
        EventModel replacement = chosen.EventFactory();
        replacement.AssertCanonical();

        state.MarkConsumed(chosen.Id);
        state.SetValue(replacementKey, chosen.Id);
        Log.Info($"[SpecialGuest] Replaced {finalEvent.Id} with {replacement.Id}; guest={chosen.Id}, key={rollKey}.");
        return replacement;
    }

    private static bool IsUnlockConditionMet(
        SpecialGuestDefinition definition,
        IRunState runState)
    {
        try
        {
            return definition.UnlockCondition(runState);
        }
        catch (Exception exception)
        {
            Log.Error(
                $"[SpecialGuest] Unlock predicate failed for {definition.Id}: {exception}");
            return false;
        }
    }

    private static bool CanAppear(
        SpecialGuestDefinition definition,
        IRunState runState)
    {
        try
        {
            return definition.CanAppear(runState);
        }
        catch (Exception exception)
        {
            Log.Error(
                $"[SpecialGuest] Availability predicate failed for {definition.Id}: {exception}");
            return false;
        }
    }

    public static string BuildRollKey(IRunState runState, EventModel originalEvent)
    {
        string coord = runState.CurrentMapCoord is { } mapCoord
            ? mapCoord.col + "," + mapCoord.row
            : "none";
        return runState.Rng.Seed
               + "|act=" + runState.CurrentActIndex
               + "|coord=" + coord
               + "|event=" + originalEvent.Id;
    }

    public static ulong StableHash64(string value)
    {
        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        ulong hash = offset;
        foreach (char character in value)
        {
            hash ^= (byte)character;
            hash *= prime;
            hash ^= (byte)(character >> 8);
            hash *= prime;
        }

        return hash;
    }
}
