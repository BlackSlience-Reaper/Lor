using System;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace LibraryOfRuina.specialguests;

internal static class SpecialGuestRoomPersistence
{
    internal static async Task SaveCurrentEventRoomAsync(
        RunState runState,
        ModelId expectedEventId)
    {
        if (!TryGetCurrentEventRoom(runState, expectedEventId, out EventRoom eventRoom))
        {
            return;
        }

        // Unknown map points are normally saved before their room roll and are
        // reconstructed by replaying that RNG.  A special guest is selected
        // after the native checkpoint, so its post-entry checkpoint must carry
        // the exact room or loading will roll a new shop/event/encounter.
        await SaveManager.Instance.SaveRun(eventRoom, saveProgress: false);
    }

    internal static async Task SaveFinishedCurrentEventRoomAsync(
        RunState runState,
        ModelId expectedEventId)
    {
        if (!TryGetCurrentEventRoom(runState, expectedEventId, out EventRoom eventRoom))
        {
            return;
        }

        eventRoom.MarkPreFinished();
        await SaveManager.Instance.SaveRun(eventRoom, saveProgress: false);
    }

    internal static bool RepairMissingActiveEventRoom(SerializableRun save)
    {
        if (save.PreFinishedRoom != null
            || save.CurrentActIndex < 0
            || save.CurrentActIndex >= save.MapPointHistory.Count)
        {
            return false;
        }

        var actHistory = save.MapPointHistory[save.CurrentActIndex];
        // Every act starts with one history entry before the first map coord.
        // Equal counts mean the save is the native pre-entry checkpoint for the
        // next coord; only count+1 proves that the latest room was already entered.
        if (actHistory.Count != save.VisitedMapCoords.Count + 1)
        {
            return false;
        }

        MapPointRoomHistoryEntry? latestRoom = actHistory
            .LastOrDefault()?
            .Rooms
            .LastOrDefault();
        if (latestRoom is not
            {
                RoomType: RoomType.Event,
                ModelId: { } eventId,
            }
            || !IsSpecialGuestEvent(eventId))
        {
            return false;
        }

        save.PreFinishedRoom = new SerializableRoom
        {
            RoomType = RoomType.Event,
            EventId = eventId,
            IsPreFinished = false,
        };
        Log.Warn(
            $"[SpecialGuest] Repaired missing active event room from map history: {eventId}.");
        return true;
    }

    /// <summary>
    /// A mid-reception save can also carry no checkpoint room at all when a
    /// third-party save overwrites it during the stage combat.  If the latest
    /// map-history room is a special-guest stage combat, resolve that stage and
    /// the guest's parent event so the load can return to the event start.
    /// A post-victory save always carries a pre-finished room, so this only
    /// matches in-flight receptions.
    /// </summary>
    internal static (ISpecialGuestEncounterStage Stage, ModelId EventId)?
        TryResolveLatestStageRoom(SerializableRun save)
    {
        if (save.PreFinishedRoom != null
            || save.CurrentActIndex < 0
            || save.CurrentActIndex >= save.MapPointHistory.Count)
        {
            return null;
        }

        var actHistory = save.MapPointHistory[save.CurrentActIndex];
        if (actHistory.Count != save.VisitedMapCoords.Count + 1)
        {
            return null;
        }

        MapPointRoomHistoryEntry? latestRoom = actHistory
            .LastOrDefault()?
            .Rooms
            .LastOrDefault();
        if (latestRoom is not
            {
                RoomType: RoomType.Monster or RoomType.Elite or RoomType.Boss,
                ModelId: { } encounterId,
            })
        {
            return null;
        }

        try
        {
            if (ModelDb.GetByIdOrNull<EncounterModel>(encounterId)
                    is not ISpecialGuestEncounterStage stage
                || !SpecialGuestRegistry.TryGet(
                    stage.SpecialGuestId,
                    out SpecialGuestDefinition? guest))
            {
                return null;
            }

            return (stage, guest.EventFactory().Id);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static bool IsSpecialGuestEvent(ModelId eventId)
    {
        try
        {
            return ModelDb.GetById<EventModel>(eventId) is SpecialGuestEventBase;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool TryGetCurrentEventRoom(
        RunState runState,
        ModelId expectedEventId,
        out EventRoom eventRoom)
    {
        if (runState.CurrentRoom is EventRoom currentEventRoom
            && currentEventRoom.CanonicalEvent.Id == expectedEventId)
        {
            eventRoom = currentEventRoom;
            return true;
        }

        Log.Error(
            $"[SpecialGuest] Refused to checkpoint {expectedEventId}: "
            + "the matching EventRoom is not current.");
        eventRoom = null!;
        return false;
    }
}
