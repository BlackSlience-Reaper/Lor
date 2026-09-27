using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.compat;
using LibraryOfRuina.networking;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.specialguests;

/// <summary>
/// Host-authoritative whole-sequence barrier.  A client sends Ready only after
/// its local player has completed the story; the host waits for the currently
/// connected set, removes disconnects, and broadcasts one Release.  A player
/// that reconnects while a barrier is active is treated as already complete.
/// </summary>
public static class StoryReadySynchronizer
{
    private const int ReadyRetryMilliseconds = 250;
    private static readonly object Sync = new();
    private static readonly Dictionary<string, Barrier> Barriers = new(StringComparer.Ordinal);
    private static INetGameService? _registeredService;
    private static bool _registered;

    /// <summary>
    /// Creates the local barrier before the reader opens.  The RitsuLib sync
    /// descriptor is registered during mod initialization, while this early
    /// barrier creation lets a fast client finish before the host does.
    /// </summary>
    public static void Prepare(string storyId)
    {
        RunManager manager = RunManager.Instance;
        INetGameService netService = manager.NetService;
        EnsureRegistered(manager, netService);
        if (netService.Type != NetGameType.Singleplayer && netService.IsConnected)
        {
            string barrierId = BuildBarrierId(manager, storyId);
            bool created;
            lock (Sync)
            {
                created = !Barriers.ContainsKey(barrierId);
                _ = GetOrCreateBarrier(manager, barrierId);
            }

            if (created)
            {
                Log.Info(
                    $"[SpecialGuestStory.Barrier] Prepare barrier={barrierId} "
                    + $"localNetId={netService.NetId} role={netService.Type}.");
            }
        }
    }

    public static async Task WaitForReleaseAsync(string storyId)
    {
        RunManager manager = RunManager.Instance;
        INetGameService netService = manager.NetService;
        EnsureRegistered(manager, netService);

        if (netService.Type == NetGameType.Singleplayer || !netService.IsConnected)
        {
            return;
        }

        string barrierId = BuildBarrierId(manager, storyId);
        Barrier barrier = GetOrCreateBarrier(manager, barrierId);

        if (netService.Type == NetGameType.Host)
        {
            MarkReadyAndTryRelease(manager, barrierId, netService.NetId);
        }
        else
        {
            // A peer can reach the story before the host has installed its
            // handler (for example while the host is still doing presentation
            // setup).  Reliable transport cannot recover a packet discarded by
            // an absent handler, so retry Ready until the authoritative Release.
            Log.Info(
                $"[SpecialGuestStory.Barrier] Ready send barrier={barrierId} "
                + $"playerNetId={netService.NetId} role={netService.Type}.");
            int attempt = 0;
            while (!barrier.Completion.Task.IsCompleted && netService.IsConnected)
            {
                attempt++;
                SendBarrierMessage(
                    netService,
                    StoryBarrierMessageKind.Ready,
                    barrierId);
                Log.VeryDebug(
                    $"[SpecialGuestStory.Barrier] Ready sent barrier={barrierId} "
                    + $"playerNetId={netService.NetId} attempt={attempt}.");

                Task completed = await Task.WhenAny(
                    barrier.Completion.Task,
                    Task.Delay(ReadyRetryMilliseconds));
                if (ReferenceEquals(completed, barrier.Completion.Task))
                {
                    break;
                }
            }

            if (!netService.IsConnected)
            {
                Log.Info(
                    $"[SpecialGuestStory.Barrier] Disconnect cleanup barrier={barrierId} "
                    + $"localNetId={netService.NetId} role={netService.Type}.");
                barrier.Completion.TrySetResult();
            }
        }

        await barrier.Completion.Task;
    }

    public static void Reset()
    {
        lock (Sync)
        {
            foreach (Barrier barrier in Barriers.Values)
            {
                barrier.Completion.TrySetResult();
            }

            if (Barriers.Count > 0)
            {
                Log.Info(
                    $"[SpecialGuestStory.Barrier] Reset cleanup barriers={Barriers.Count} "
                    + $"localNetId={_registeredService?.NetId.ToString() ?? "none"}.");
            }

            Barriers.Clear();
            UnregisterHandlers();
        }
    }

    private static void EnsureRegistered(RunManager manager, INetGameService netService)
    {
        lock (Sync)
        {
            if (_registered && ReferenceEquals(_registeredService, netService))
            {
                return;
            }

            UnregisterHandlers();
            _registeredService = netService;
            RunLobbyEventsCompat.Register(
                manager,
                HandleDisconnected,
                HandleRejoined,
                HandleLocalDisconnected);

            _registered = true;
        }
    }

    private static void UnregisterHandlers()
    {
        if (!_registered || _registeredService == null)
        {
            _registered = false;
            _registeredService = null;
            return;
        }

        try
        {
            RunLobbyEventsCompat.Unregister(RunManager.Instance);
        }
        catch
        {
            // The game may already be tearing its network service down.
        }

        _registered = false;
        _registeredService = null;
    }

    internal static void HandleRitsuMessage(
        StoryBarrierMessageKind kind,
        string barrierId,
        ulong senderId)
    {
        HandleBarrierMessage(kind, barrierId, senderId);
    }

    private static void HandleBarrierMessage(
        StoryBarrierMessageKind kind,
        string barrierId,
        ulong senderId)
    {
        RunManager manager = RunManager.Instance;
        INetGameService netService = manager.NetService;
        Barrier preparedBarrier;
        lock (Sync)
        {
            if (!Barriers.TryGetValue(barrierId, out preparedBarrier!))
            {
                Log.Warn(
                    "[SpecialGuestStory.Barrier] Ignoring message for an unprepared barrier. "
                    + "kind="
                    + kind
                    + " barrier="
                    + barrierId
                    + " senderNetId="
                    + senderId
                    + ".");
                return;
            }
        }

        if (kind == StoryBarrierMessageKind.Ready)
        {
            if (netService.Type != NetGameType.Host)
            {
                return;
            }

            if (manager.RunLobby != null
                && !RunLobbyEventsCompat.GetConnectedPlayerIds(manager)
                    .Contains(senderId))
            {
                Log.Warn($"[SpecialGuestStory] Ignoring Ready from disconnected/unknown player {senderId}.");
                return;
            }

            Log.Info(
                $"[SpecialGuestStory.Barrier] Ready received barrier={barrierId} "
                + $"playerNetId={senderId} hostNetId={netService.NetId}.");
            MarkReadyAndTryRelease(manager, barrierId, senderId);
            return;
        }

        if (kind == StoryBarrierMessageKind.Release)
        {
            if (netService.Type != NetGameType.Client
                || netService is not NetClientGameService clientService
                || senderId != clientService.HostNetId)
            {
                Log.Warn($"[SpecialGuestStory] Ignoring non-host Release from player {senderId}.");
                return;
            }

            lock (Sync)
            {
                preparedBarrier.Completion.TrySetResult();
            }
            Log.Info(
                $"[SpecialGuestStory.Barrier] Release received barrier={barrierId} "
                + $"hostNetId={senderId} localNetId={netService.NetId}.");
        }
    }

    private static void MarkReadyAndTryRelease(RunManager manager, string barrierId, ulong playerId)
    {
        lock (Sync)
        {
            if (!Barriers.TryGetValue(barrierId, out Barrier? barrier))
            {
                Log.Warn(
                    "[SpecialGuestStory.Barrier] Ignoring Ready for an unprepared barrier. "
                    + "barrier="
                    + barrierId
                    + " playerNetId="
                    + playerId
                    + ".");
                return;
            }

            bool firstReady = barrier.ReadyPlayers.Add(playerId);
            if (firstReady)
            {
                Log.Info(
                    $"[SpecialGuestStory.Barrier] Ready accepted barrier={barrierId} "
                    + $"playerNetId={playerId} hostNetId={manager.NetService.NetId}.");
            }

            if (barrier.Released)
            {
                if (manager.NetService.Type == NetGameType.Host
                    && playerId != manager.NetService.NetId)
                {
                    Log.Info(
                        $"[SpecialGuestStory.Barrier] Release resend barrier={barrierId} "
                        + $"targetNetId={playerId} hostNetId={manager.NetService.NetId}.");
                    SendBarrierMessage(
                        manager.NetService,
                        StoryBarrierMessageKind.Release,
                        barrierId,
                        playerId);
                }

                return;
            }

            TryFlushPendingReleases(manager, barrierId, barrier);
        }
    }

    private static void TryFlushPendingReleases(
        RunManager manager,
        string barrierId,
        Barrier barrier)
    {
        if (manager.NetService.Type != NetGameType.Host || barrier.Released)
        {
            return;
        }

        IReadOnlyCollection<ulong> connected = GetConnectedPlayerIds(manager);
        if (connected.Any(id => !barrier.ReadyPlayers.Contains(id)))
        {
            return;
        }

        ulong hostNetId = manager.NetService.NetId;
        foreach (ulong peerId in connected.Where(id => id != hostNetId))
        {
            if (!barrier.QueuedReleasePeers.Contains(peerId))
            {
                barrier.PendingReleasePeers.Add(peerId);
            }
        }
        barrier.PendingReleasePeers.RemoveWhere(id => !connected.Contains(id));

        foreach (ulong peerId in barrier.PendingReleasePeers.ToArray())
        {
            if (SendBarrierMessage(
                    manager.NetService,
                    StoryBarrierMessageKind.Release,
                    barrierId,
                    peerId))
            {
                barrier.PendingReleasePeers.Remove(peerId);
                barrier.QueuedReleasePeers.Add(peerId);
            }
        }

        if (barrier.PendingReleasePeers.Count > 0)
        {
            return;
        }

        barrier.Released = true;
        Log.Info(
            $"[SpecialGuestStory.Barrier] Release queued for every connected peer barrier={barrierId} "
            + $"hostNetId={hostNetId} "
            + $"players={string.Join(",", connected.OrderBy(static id => id))}.");
        barrier.Completion.TrySetResult();
    }

    private static IReadOnlyCollection<ulong> GetConnectedPlayerIds(RunManager manager)
    {
        return manager.RunLobby != null
            ? RunLobbyEventsCompat.GetConnectedPlayerIds(manager)
            : manager.DebugOnlyGetState()?.Players
                .Select(static player => player.NetId)
                .ToArray()
              ?? Array.Empty<ulong>();
    }

    private static void HandleDisconnected(ulong playerId)
    {
        RunManager manager = RunManager.Instance;
        if (manager.NetService.Type != NetGameType.Host)
        {
            return;
        }

        lock (Sync)
        {
            foreach ((string barrierId, Barrier barrier) in Barriers.ToArray())
            {
                if (!barrier.Released)
                {
                    Log.Info(
                        $"[SpecialGuestStory.Barrier] Disconnect cleanup barrier={barrierId} "
                        + $"playerNetId={playerId} hostNetId={manager.NetService.NetId}.");
                    MarkReadyAndTryRelease(manager, barrierId, playerId);
                }
            }
        }
    }

    private static void HandleRejoined(ulong playerId)
    {
        RunManager manager = RunManager.Instance;
        if (manager.NetService.Type != NetGameType.Host)
        {
            return;
        }

        lock (Sync)
        {
            foreach ((string barrierId, Barrier barrier) in Barriers.ToArray())
            {
                // A runtime rejoin resumes from a host snapshot; it must not
                // replay and block an already-running local story.  Calling
                // this for an already-released barrier intentionally sends a
                // targeted Release again.
                Log.Info(
                    $"[SpecialGuestStory.Barrier] Rejoin cleanup barrier={barrierId} "
                    + $"playerNetId={playerId} hostNetId={manager.NetService.NetId}.");
                barrier.QueuedReleasePeers.Remove(playerId);
                MarkReadyAndTryRelease(manager, barrierId, playerId);
            }
        }
    }

    private static void HandleLocalDisconnected()
    {
        lock (Sync)
        {
            foreach ((string barrierId, Barrier barrier) in Barriers)
            {
                Log.Info(
                    $"[SpecialGuestStory.Barrier] Local disconnect cleanup barrier={barrierId} "
                    + $"localNetId={_registeredService?.NetId.ToString() ?? "none"}.");
                barrier.Completion.TrySetResult();
            }
        }
    }

    private static Barrier GetOrCreateBarrier(RunManager manager, string barrierId)
    {
        lock (Sync)
        {
            if (Barriers.TryGetValue(barrierId, out Barrier? existing))
            {
                return existing;
            }

            Barrier created = new();
            Barriers[barrierId] = created;
            return created;
        }
    }

    private static string BuildBarrierId(RunManager manager, string storyId)
    {
        IRunState? state = manager.DebugOnlyGetState();
        string location = state == null
            ? "no-run"
            : state.CurrentActIndex + ":" + (state.CurrentMapCoord?.ToString() ?? "none") + ":" + state.CurrentRoomCount;
        return location + ":" + storyId;
    }

    private static bool SendBarrierMessage(
        INetGameService netService,
        StoryBarrierMessageKind kind,
        string barrierId,
        ulong? targetPeerId = null)
    {
        if (!netService.IsConnected)
        {
            return false;
        }

        bool accepted;
        if (kind == StoryBarrierMessageKind.Ready)
        {
            accepted = LibraryNetwork.TrySendStoryReady(netService, barrierId);
        }
        else if (targetPeerId.HasValue)
        {
            accepted = LibraryNetwork.TrySendStoryReleaseToPeer(
                netService,
                targetPeerId.Value,
                barrierId);
        }
        else
        {
            accepted = false;
        }

        if (!accepted)
        {
            Log.VeryDebug(
                "[SpecialGuestStory.Barrier] Sync send deferred. kind="
                + kind
                + " barrier="
                + barrierId
                + " target="
                + (targetPeerId?.ToString() ?? "broadcast/host")
                + " localNetId="
                + netService.NetId
                + ".");
        }

        return accepted;
    }

    private sealed class Barrier
    {
        public HashSet<ulong> ReadyPlayers { get; } = [];

        public TaskCompletionSource Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public HashSet<ulong> PendingReleasePeers { get; } = [];

        public HashSet<ulong> QueuedReleasePeers { get; } = [];

        public bool Released { get; set; }
    }
}

public enum StoryBarrierMessageKind : byte
{
    Ready = 0,
    Release = 1,
}
