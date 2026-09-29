using System;
using System.Linq;
using LibraryOfRuina.core.settings;
using LibraryOfRuina.encounters;
using LibraryOfRuina.guests;
using LibraryOfRuina.specialguests;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.relics.BookShadow;

internal static class BookShadowEncounterReplacement
{
    internal static bool TryReplaceActiveGuestEncounter(
        ActModel act,
        RoomType roomType,
        EncounterModel current,
        out EncounterModel replacement)
    {
        replacement = current;
        if (!LibraryRunSettings.MonsterExtensionEnabled
            || !CanReplaceRoomType(roomType)
            || current is ISpecialGuestEncounterStage
            || LiberationBossRegistry.IsLiberationEncounter(current)
            || RunManager.Instance.DebugOnlyGetState() is not RunState runState)
        {
            return false;
        }

        Player? host = BookShadowHostPlayerResolver.Resolve(runState);
        BookShadowRelic? relic = host?.Relics.OfType<BookShadowRelic>()
            .FirstOrDefault();
        if (relic is not { IsActive: true })
        {
            return false;
        }

        if (current is IGuestReceptionEncounter)
        {
            return true;
        }

        string rollKey = BuildRollKey(runState, act, roomType, current);
        if (StableHash64(rollKey + "|chance") % 100UL
            >= BookShadowRelic.GuestReplacementChancePercent)
        {
            return false;
        }

        EncounterModel[] candidates = GuestReceptionPoolRegistry
            .GetGuestEncounterCandidates(
                act,
                roomType,
                current.IsWeak,
                runState)
            .Where(candidate => candidate.Id != current.Id)
            .Where(static candidate => candidate is not ISpecialGuestEncounterStage)
            .Where(candidate => !LiberationBossRegistry.IsLiberationEncounter(candidate))
            .OrderBy(static candidate => candidate.Id.Entry, StringComparer.Ordinal)
            .ToArray();
        if (candidates.Length == 0)
        {
            return false;
        }

        ulong guestRoll = StableHash64(rollKey + "|guest");
        replacement = candidates[(int)(guestRoll % (ulong)candidates.Length)];
        return true;
    }

    internal static bool CanReplaceRoomType(RoomType roomType) =>
        roomType == RoomType.Monster;

    private static string BuildRollKey(
        RunState runState,
        ActModel act,
        RoomType roomType,
        EncounterModel current)
    {
        string mapCoord = runState.CurrentMapCoord is { } coord
            ? coord.col + "," + coord.row
            : "none";
        return runState.Rng.Seed
               + "|act=" + runState.CurrentActIndex
               + "|floor=" + runState.TotalFloor
               + "|coord=" + mapCoord
               + "|actId=" + act.Id.Entry
               + "|room=" + roomType
               + "|encounter=" + current.Id.Entry;
    }

    private static ulong StableHash64(string value)
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
