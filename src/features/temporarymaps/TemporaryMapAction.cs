using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.core.networking;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.features.temporarymaps;

public enum TemporaryMapActionKind
{
    Enter,
    Return
}

public sealed class TemporaryMapAction : GameAction
{
    private const int RequestRetryMilliseconds = 250;
    private const int RetryWarningInterval = 40;

    private static readonly object PendingRequestLock = new();
    private static readonly HashSet<PendingRequestKey> PendingRequests = [];

    private readonly Player _player;
    private readonly TemporaryMapActionKind _kind;
    private readonly string _definitionId;

    public override ulong OwnerId => _player.NetId;

    public override GameActionType ActionType => GameActionType.NonCombat;

    public TemporaryMapAction(Player player, TemporaryMapActionKind kind, string definitionId)
    {
        _player = player;
        _kind = kind;
        _definitionId = definitionId;
    }

    public static void EnqueueEnter(Player player, string definitionId) =>
        QueueWithRetry(player, TemporaryMapActionKind.Enter, definitionId);

    public static void EnqueueReturn(Player player) =>
        QueueWithRetry(player, TemporaryMapActionKind.Return, string.Empty);

    internal static bool TryEnqueueEnter(Player player, string definitionId) =>
        LibraryNetwork.TryRequestTemporaryMapTransition(
            player,
            TemporaryMapActionKind.Enter,
            definitionId);

    internal static bool TryEnqueueReturn(Player player) =>
        LibraryNetwork.TryRequestTemporaryMapTransition(
            player,
            TemporaryMapActionKind.Return,
            string.Empty);

    private static void QueueWithRetry(
        Player player,
        TemporaryMapActionKind kind,
        string definitionId)
    {
        if (!LibraryNetwork.IsLocalOwner(player))
        {
            return;
        }

        IRunState? runState = player.RunState;
        if (runState == null
            || !ReferenceEquals(RunManager.Instance.DebugOnlyGetState(), runState))
        {
            return;
        }

        var key = new PendingRequestKey(
            runState,
            player.NetId,
            kind,
            definitionId ?? string.Empty);
        lock (PendingRequestLock)
        {
            if (!PendingRequests.Add(key))
            {
                return;
            }
        }

        _ = TaskHelper.RunSafely(RetryUntilAccepted(player, key));
    }

    private static async Task RetryUntilAccepted(
        Player player,
        PendingRequestKey key)
    {
        try
        {
            int attempt = 0;
            while (IsRequestStillRelevant(player, key.RunState))
            {
                attempt++;
                bool accepted = key.Kind switch
                {
                    TemporaryMapActionKind.Enter => TryEnqueueEnter(
                        player,
                        key.DefinitionId),
                    TemporaryMapActionKind.Return => TryEnqueueReturn(player),
                    _ => false
                };
                if (accepted)
                {
                    if (attempt > 1)
                    {
                        Log.Info(
                            "[TemporaryMap] Ritsu managed-action request accepted after "
                            + attempt
                            + " attempts. kind="
                            + key.Kind
                            + " definitionId="
                            + key.DefinitionId
                            + ".");
                    }

                    return;
                }

                if (attempt % RetryWarningInterval == 0)
                {
                    Log.Warn(
                        "[TemporaryMap] Still waiting for Ritsu managed-action availability. "
                        + "kind="
                        + key.Kind
                        + " definitionId="
                        + key.DefinitionId
                        + " attempts="
                        + attempt
                        + ".");
                }

                await Task.Delay(RequestRetryMilliseconds);
            }
        }
        finally
        {
            lock (PendingRequestLock)
            {
                PendingRequests.Remove(key);
            }
        }
    }

    private static bool IsRequestStillRelevant(
        Player player,
        IRunState capturedRunState)
    {
        try
        {
            IRunState? state = RunManager.Instance.DebugOnlyGetState();
            var netService = RunManager.Instance.NetService;
            bool canSend = netService.Type == NetGameType.Singleplayer
                || (netService.Type is NetGameType.Host or NetGameType.Client
                    && netService.IsConnected);
            return canSend
                && ReferenceEquals(state, capturedRunState)
                && ReferenceEquals(player.RunState, capturedRunState)
                && state.Players.Any(candidate => candidate.NetId == player.NetId)
                && LibraryNetwork.IsLocalOwner(player);
        }
        catch
        {
            return false;
        }
    }

    protected override Task ExecuteAction()
    {
        return _kind switch
        {
            TemporaryMapActionKind.Enter => TemporaryMapController.EnterFromSyncedAction(_player, _definitionId),
            TemporaryMapActionKind.Return => TemporaryMapSessionManager.RestoreOriginalMapFromSyncedAction(_player),
            _ => Task.CompletedTask
        };
    }

    public override INetAction ToNetAction()
    {
        return new NetTemporaryMapAction
        {
            Kind = _kind,
            DefinitionId = _definitionId
        };
    }

    public override string ToString()
    {
        return "TemporaryMapAction " + _kind + " " + _definitionId + " for player " + _player.NetId;
    }

    private readonly record struct PendingRequestKey(
        IRunState RunState,
        ulong PlayerNetId,
        TemporaryMapActionKind Kind,
        string DefinitionId);
}

public struct NetTemporaryMapAction : INetAction, IPacketSerializable
{
    public TemporaryMapActionKind Kind;
    public string DefinitionId;

    public GameAction ToGameAction(Player player)
    {
        return new TemporaryMapAction(player, Kind, DefinitionId ?? string.Empty);
    }

    public void Serialize(PacketWriter writer)
    {
        writer.WriteEnum(Kind);
        writer.WriteString(DefinitionId ?? string.Empty);
    }

    public void Deserialize(PacketReader reader)
    {
        Kind = reader.ReadEnum<TemporaryMapActionKind>();
        DefinitionId = reader.ReadString();
    }

    public override string ToString()
    {
        return "NetTemporaryMapAction " + Kind + " " + DefinitionId;
    }
}
