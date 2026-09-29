using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.content.abnormalities.LittleRedMercenary;
using LibraryOfRuina.features.temporarymaps;
using LibraryOfRuina.specialguests;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Networking.ManagedActions;
using STS2RitsuLib.Networking.Sidecar;

namespace LibraryOfRuina.core.networking;

internal static class LibraryNetwork
{
    private const string ModuleId = "LibraryOfRuina";
    private const string BlockTransferActionKey = "little-red-block-transfer.v1";
    private const string TemporaryMapActionKey = "temporary-map-transition.v1";
    private const string StoryBarrierMessageKey = "special-guest/story-barrier.v1";
    private const int StoryBarrierMaxPayloadBytes = 4096;
    private const int StoryBarrierMaxIdLength = 1024;
    private const int TemporaryMapMaxDefinitionIdLength = 256;

    private static readonly Lock InitLock = new();

    private static readonly RitsuLibManagedNetActionDescriptor<LittleRedBlockTransferPayload>
        LittleRedBlockTransferDescriptor = new(
            ModuleId,
            BlockTransferActionKey,
            SerializeLittleRedBlockTransfer,
            DeserializeLittleRedBlockTransfer,
            ExecuteLittleRedBlockTransfer,
            GameActionType.CombatPlayPhaseOnly);

    private static readonly RitsuLibManagedNetActionDescriptor<TemporaryMapTransitionPayload>
        TemporaryMapTransitionDescriptor = new(
            ModuleId,
            TemporaryMapActionKey,
            SerializeTemporaryMapTransition,
            DeserializeTemporaryMapTransition,
            ExecuteTemporaryMapTransition,
            GameActionType.NonCombat);

    private static readonly RitsuLibSidecarSyncMessageDescriptor<StoryBarrierPayload>
        StoryBarrierDescriptor = new(
            ModuleId,
            StoryBarrierMessageKey,
            SerializeStoryBarrier,
            DeserializeStoryBarrier,
            HandleStoryBarrierMessage,
            locationTargeted: true,
            shouldBuffer: true);

    private static bool _initialized;

    internal static void Initialize()
    {
        lock (InitLock)
        {
            if (_initialized)
            {
                return;
            }

            RitsuLibManagedNetActions.Register(LittleRedBlockTransferDescriptor);
            RitsuLibManagedNetActions.Register(TemporaryMapTransitionDescriptor);
            RitsuLibSidecarSyncMessages.Register(StoryBarrierDescriptor);

            _initialized = true;
            Version? version = typeof(RitsuLibManagedNetActions).Assembly.GetName().Version;
            Log.Info(
                "[LibraryOfRuina.Net] Registered action keys '"
                + BlockTransferActionKey
                + "', '"
                + TemporaryMapActionKey
                + "' and sync message '"
                + StoryBarrierMessageKey
                + "' with RitsuLib "
                + (version?.ToString() ?? "unknown")
                + ".");
        }
    }

    internal static bool TryRequestLittleRedBlockTransfer(
        Player player,
        int combatRound,
        uint? partnerCombatId)
    {
        if (!IsLocalOwner(player))
        {
            return false;
        }

        bool queued;
        try
        {
            queued = RitsuLibManagedNetActions.Request(
                RunManager.Instance,
                LittleRedBlockTransferDescriptor,
                new LittleRedBlockTransferPayload(combatRound, partnerCombatId),
                player.NetId);
        }
        catch (Exception exception)
        {
            Log.Error("[LibraryOfRuina.Net] Block-transfer request failed: " + exception);
            return false;
        }

        if (!queued)
        {
            Log.Warn(
                "[LibraryOfRuina.Net] Block-transfer request was not accepted. "
                + "playerNetId="
                + player.NetId
                + " round="
                + combatRound
                + ".");
        }

        return queued;
    }

    internal static bool TryRequestTemporaryMapTransition(
        Player player,
        TemporaryMapActionKind kind,
        string definitionId)
    {
        if (!IsLocalOwner(player))
        {
            return false;
        }

        bool queued;
        try
        {
            queued = RitsuLibManagedNetActions.Request(
                RunManager.Instance,
                TemporaryMapTransitionDescriptor,
                new TemporaryMapTransitionPayload(kind, definitionId ?? string.Empty),
                player.NetId);
        }
        catch (Exception exception)
        {
            Log.Error("[LibraryOfRuina.Net] Temporary-map request failed: " + exception);
            return false;
        }

        if (!queued)
        {
            Log.VeryDebug(
                "[LibraryOfRuina.Net] Temporary-map request was not accepted. "
                + "playerNetId="
                + player.NetId
                + " kind="
                + kind
                + " definitionId="
                + definitionId
                + ".");
        }

        return queued;
    }

    internal static bool IsLocalOwner(Player player)
    {
        try
        {
            return player.NetId == RunManager.Instance.NetService.NetId;
        }
        catch
        {
            return false;
        }
    }

    internal static bool TrySendStoryReady(
        INetGameService netService,
        string barrierId)
    {
        return RitsuLibSidecarSyncMessages.SendToHost(
            netService,
            StoryBarrierDescriptor,
            new StoryBarrierPayload(
                StoryBarrierMessageKind.Ready,
                barrierId));
    }

    internal static bool TrySendStoryReleaseToPeer(
        INetGameService netService,
        ulong peerNetId,
        string barrierId)
    {
        return RitsuLibSidecarSyncMessages.SendToPeer(
            netService,
            peerNetId,
            StoryBarrierDescriptor,
            new StoryBarrierPayload(
                StoryBarrierMessageKind.Release,
                barrierId));
    }

    private static byte[] Serialize<T>(T payload) =>
        JsonSerializer.SerializeToUtf8Bytes(payload);

    private static byte[] SerializeLittleRedBlockTransfer(
        LittleRedBlockTransferPayload payload)
    {
        ValidateLittleRedBlockTransfer(payload);
        return Serialize(payload);
    }

    private static byte[] SerializeTemporaryMapTransition(
        TemporaryMapTransitionPayload payload)
    {
        ValidateTemporaryMapTransition(payload);
        return Serialize(payload);
    }

    private static LittleRedBlockTransferPayload DeserializeLittleRedBlockTransfer(
        ReadOnlySpan<byte> bytes)
    {
        LittleRedBlockTransferPayload payload =
            JsonSerializer.Deserialize<LittleRedBlockTransferPayload>(bytes);
        ValidateLittleRedBlockTransfer(payload);
        return payload;
    }

    private static TemporaryMapTransitionPayload DeserializeTemporaryMapTransition(
        ReadOnlySpan<byte> bytes)
    {
        TemporaryMapTransitionPayload payload =
            JsonSerializer.Deserialize<TemporaryMapTransitionPayload>(bytes);
        ValidateTemporaryMapTransition(payload);
        return payload;
    }

    private static void ValidateLittleRedBlockTransfer(
        LittleRedBlockTransferPayload payload)
    {
        if (payload.CombatRound <= 0)
        {
            throw new InvalidDataException("Block-transfer combat round must be positive.");
        }
    }

    private static void ValidateTemporaryMapTransition(
        TemporaryMapTransitionPayload payload)
    {
        if (!Enum.IsDefined(payload.Kind))
        {
            throw new InvalidDataException("Unknown temporary-map transition kind.");
        }
        if (payload.DefinitionId == null
            || payload.DefinitionId.Length > TemporaryMapMaxDefinitionIdLength)
        {
            throw new InvalidDataException("Invalid temporary-map definition ID length.");
        }
        if (payload.Kind == TemporaryMapActionKind.Enter
            && string.IsNullOrWhiteSpace(payload.DefinitionId))
        {
            throw new InvalidDataException("Temporary-map entry requires a definition ID.");
        }
        if (payload.Kind == TemporaryMapActionKind.Return
            && payload.DefinitionId.Length != 0)
        {
            throw new InvalidDataException("Temporary-map return requires an empty definition ID.");
        }
    }

    private static Task ExecuteLittleRedBlockTransfer(
        RitsuLibManagedNetActionContext<LittleRedBlockTransferPayload> context) =>
        BlockTransferAction.ExecuteSyncedAction(
            context.Player,
            context.Message.CombatRound,
            context.Message.PartnerCombatId,
            context.PlayerChoiceContext);

    private static Task ExecuteTemporaryMapTransition(
        RitsuLibManagedNetActionContext<TemporaryMapTransitionPayload> context)
    {
        return context.Message.Kind switch
        {
            TemporaryMapActionKind.Enter => TemporaryMapController.EnterFromSyncedAction(
                context.Player,
                context.Message.DefinitionId),
            TemporaryMapActionKind.Return => TemporaryMapSessionManager.RestoreOriginalMapFromSyncedAction(
                context.Player),
            _ => Task.CompletedTask
        };
    }

    private static byte[] SerializeStoryBarrier(StoryBarrierPayload payload)
    {
        ValidateStoryBarrier(payload);
        byte[] bytes = Serialize(payload);
        if (bytes.Length > StoryBarrierMaxPayloadBytes)
        {
            throw new InvalidDataException("Story-barrier payload exceeds the endpoint limit.");
        }

        return bytes;
    }

    private static StoryBarrierPayload DeserializeStoryBarrier(ReadOnlySpan<byte> bytes)
    {
        StoryBarrierPayload payload =
            JsonSerializer.Deserialize<StoryBarrierPayload>(bytes);
        ValidateStoryBarrier(payload);
        return payload;
    }

    private static Task HandleStoryBarrierMessage(
        RitsuLibSidecarSyncMessageContext<StoryBarrierPayload> context)
    {
        if (NGame.IsMainThread())
        {
            StoryReadySynchronizer.HandleRitsuMessage(
                context.Message.Kind,
                context.Message.BarrierId,
                context.SenderNetId);
            return Task.CompletedTask;
        }

        TaskCompletionSource completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            Callable.From(() =>
            {
                try
                {
                    StoryReadySynchronizer.HandleRitsuMessage(
                        context.Message.Kind,
                        context.Message.BarrierId,
                        context.SenderNetId);
                    completion.TrySetResult();
                }
                catch (Exception exception)
                {
                    completion.TrySetException(exception);
                }
            }).CallDeferred();
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }

        return completion.Task;
    }

    private static void ValidateStoryBarrier(StoryBarrierPayload payload)
    {
        if (!Enum.IsDefined(payload.Kind))
        {
            throw new InvalidDataException("Unknown story-barrier message kind.");
        }
        if (string.IsNullOrWhiteSpace(payload.BarrierId)
            || payload.BarrierId.Length > StoryBarrierMaxIdLength)
        {
            throw new InvalidDataException("Invalid story-barrier ID length.");
        }
    }

    private readonly record struct LittleRedBlockTransferPayload(
        int CombatRound,
        uint? PartnerCombatId);

    private readonly record struct TemporaryMapTransitionPayload(
        TemporaryMapActionKind Kind,
        string DefinitionId);

    private readonly record struct StoryBarrierPayload(
        StoryBarrierMessageKind Kind,
        string BarrierId);
}
