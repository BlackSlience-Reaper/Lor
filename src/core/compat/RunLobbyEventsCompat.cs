using System;
using System.Linq;
using MegaCrit.Sts2.Core.Runs;
#if STS2_BETA
using MegaCrit.Sts2.Core.Entities.Multiplayer;
#endif

namespace LibraryOfRuina.core.compat;

/// <summary>
/// Normalizes the run-lobby rejoin payload and connected-player collection
/// across the public and beta game assemblies.
/// </summary>
internal static class RunLobbyEventsCompat
{
    private static Action<ulong>? _disconnected;
    private static Action<ulong>? _rejoined;
    private static Action? _localDisconnected;
#if STS2_BETA
    private static Action<RunLobbyPlayer>? _rejoinedAdapter;

#endif

    public static void Register(
        RunManager manager,
        Action<ulong> disconnected,
        Action<ulong> rejoined,
        Action localDisconnected)
    {
        if (manager.RunLobby == null)
        {
            return;
        }

        Unregister(manager);
        _disconnected = disconnected;
        _rejoined = rejoined;
        _localDisconnected = localDisconnected;
        manager.RunLobby.RemotePlayerDisconnected += _disconnected;
        manager.RunLobby.LocalPlayerDisconnected += _localDisconnected;
#if STS2_BETA
        _rejoinedAdapter = player => _rejoined?.Invoke(player.id);
        manager.RunLobby.PlayerRejoined += _rejoinedAdapter;
#else
        manager.RunLobby.PlayerRejoined += _rejoined;
#endif
    }

    public static void Unregister(RunManager manager)
    {
        if (manager.RunLobby != null)
        {
            if (_disconnected != null)
            {
                manager.RunLobby.RemotePlayerDisconnected -= _disconnected;
            }

            if (_localDisconnected != null)
            {
                manager.RunLobby.LocalPlayerDisconnected -= _localDisconnected;
            }

#if STS2_BETA
            if (_rejoinedAdapter != null)
            {
                manager.RunLobby.PlayerRejoined -= _rejoinedAdapter;
            }
#else
            if (_rejoined != null)
            {
                manager.RunLobby.PlayerRejoined -= _rejoined;
            }
#endif
        }

        _disconnected = null;
        _rejoined = null;
        _localDisconnected = null;
#if STS2_BETA
        _rejoinedAdapter = null;
#endif
    }

    public static IReadOnlyCollection<ulong> GetConnectedPlayerIds(
        RunManager manager)
    {
        if (manager.RunLobby == null)
        {
            return Array.Empty<ulong>();
        }

#if STS2_BETA
        return manager.RunLobby.PlayerIds.ToArray();
#else
        return manager.RunLobby.ConnectedPlayerIds;
#endif
    }
}
