using System;
using System.Linq;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.content.specialguests;

/// <summary>
/// Public, run-scoped successful-reception information for guest dependencies.
/// Escaping from an event never satisfies this contract.
/// </summary>
public static class SpecialGuestReceptionProgress
{
    private const string CompletedResolution = "completed";

    public static IReadOnlyCollection<string> GetSuccessfullyReceivedGuestIds(
        IRunState? runState)
    {
        SpecialGuestRunStateModifier? state =
            SpecialGuestRunStateModifier.TryGet(runState);
        if (state == null)
        {
            return Array.Empty<string>();
        }

        HashSet<string> guestIds = state.SuccessfullyReceivedGuestIds
            .ToHashSet(StringComparer.Ordinal);
        foreach (SpecialGuestDefinition definition in SpecialGuestRegistry.All)
        {
            if (string.Equals(
                    state.GetValue("resolution." + definition.Id),
                    CompletedResolution,
                    StringComparison.Ordinal))
            {
                guestIds.Add(definition.Id);
            }
        }

        return guestIds.OrderBy(static id => id, StringComparer.Ordinal)
            .ToArray();
    }

    public static bool WasSuccessfullyReceived(
        IRunState? runState,
        string guestId)
    {
        if (string.IsNullOrWhiteSpace(guestId))
        {
            return false;
        }

        SpecialGuestRunStateModifier? state =
            SpecialGuestRunStateModifier.TryGet(runState);
        return state != null
               && (state.WasSuccessfullyReceived(guestId)
                   || string.Equals(
                       state.GetValue("resolution." + guestId),
                       CompletedResolution,
                       StringComparison.Ordinal));
    }

    internal static void MarkSuccessfullyReceived(
        RunState runState,
        string guestId)
    {
        SpecialGuestRunStateModifier.GetOrCreate(runState)
            .MarkSuccessfullyReceived(guestId);
    }
}
