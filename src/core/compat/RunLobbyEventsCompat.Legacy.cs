#if STS2_0_107_1
using System;
using System.Linq;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.core.compat;

/// <summary>
/// Normalizes the run-lobby rejoin payload and connected-player collection
/// across the public and beta game assemblies.
/// </summary>
internal static partial class RunLobbyEventsCompat
{
    private static Action<ulong>? _disconnected;
    private static Action<ulong>? _rejoined;
    private static Action? _localDisconnected;

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
        manager.RunLobby.PlayerRejoined += _rejoined;
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

            if (_rejoined != null)
            {
                manager.RunLobby.PlayerRejoined -= _rejoined;
            }
        }

        _disconnected = null;
        _rejoined = null;
        _localDisconnected = null;
    }

    public static IReadOnlyCollection<ulong> GetConnectedPlayerIds(
        RunManager manager)
    {
        if (manager.RunLobby == null)
        {
            return Array.Empty<ulong>();
        }

        return manager.RunLobby.ConnectedPlayerIds;
    }
}
#endif
