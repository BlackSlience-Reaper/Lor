using System;
using System.Threading.Tasks;
using LibraryOfRuina.core.networking;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.encounters;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.abnormalities.LittleRedMercenary;

public sealed class BlockTransferAction(Player player, int combatRound, uint? partnerCombatId = null) : GameAction
{
    public const int MaxBlockSpent = 10;

    public override ulong OwnerId => player.NetId;

    public override GameActionType ActionType => GameActionType.CombatPlayPhaseOnly;

    public static bool TryRequest(Player player, int combatRound, uint? partnerCombatId = null) =>
        LibraryNetwork.TryRequestLittleRedBlockTransfer(
            player,
            combatRound,
            partnerCombatId);

    protected override Task ExecuteAction() =>
        ExecuteSyncedAction(
            player,
            combatRound,
            partnerCombatId,
            new GameActionPlayerChoiceContext(this));

    internal static async Task ExecuteSyncedAction(
        Player player,
        int combatRound,
        uint? partnerCombatId,
        PlayerChoiceContext choiceContext)
    {
        CombatStateLike? combatState = player.Creature.CombatState;
        if (combatState == null
            || !CombatManager.Instance.IsInProgress
            || CombatManager.Instance.IsOverOrEnding
            || !BlockTransferEncounterTargetHelper.IsSupportedEncounter(combatState))
        {
            return;
        }

        if (combatState.RoundNumber != combatRound)
        {
            Log.Info("[BlockTransfer] Ignoring stale transfer action for player "
                + player.NetId
                + ". Current round: "
                + combatState.RoundNumber
                + " action round: "
                + combatRound
                + " CombatState: "
                + RunManager.Instance.ActionQueueSynchronizer.CombatState);
            return;
        }

        Creature source = player.Creature;
        Creature? partner = partnerCombatId.HasValue
            ? combatState.GetCreature(partnerCombatId)
            : BlockTransferEncounterTargetHelper.FindPartner(combatState);
        if (combatState.CurrentSide != CombatSide.Player
            || source.IsDead
            || source.Block <= 0
            || partner is not { IsAlive: true }
            || !AllyTurnRegistry.CanTransferBlockWith(partner))
        {
            return;
        }

        int spent = Math.Min(MaxBlockSpent, source.Block);
        if (spent <= 0)
        {
            return;
        }

        int playerCount = Math.Max(1, combatState.Players.Count);
        int transfer = playerCount == 1 ? spent : (int)Math.Ceiling((decimal)spent / playerCount);

        await CreatureCmd.LoseBlock(
            choiceContext,
            source,
            spent,
            source);
        if (transfer <= 0 || !partner.IsAlive)
        {
            return;
        }

        decimal gained = await CreatureCmd.GainBlock(partner, transfer, ValueProp.Unpowered, null, fast: true);
        if (gained > 0m)
        {
            await TransferredBlockPower.TrackTransfer(partner, (int)gained, source);
        }
    }

    public override INetAction ToNetAction()
    {
        return new NetLittleRedBlockTransferAction
        {
            CombatRound = combatRound
            ,PartnerCombatId = partnerCombatId
        };
    }

    public override string ToString()
    {
        return "LittleRedBlockTransferAction for player " + player.NetId + " round " + combatRound;
    }
}

public struct NetLittleRedBlockTransferAction : INetAction, IPacketSerializable
{
    public int CombatRound;
    public uint? PartnerCombatId;

    public GameAction ToGameAction(Player player)
    {
        return new BlockTransferAction(player, CombatRound, PartnerCombatId);
    }

    public void Serialize(PacketWriter writer)
    {
        writer.WriteInt(CombatRound);
        writer.WriteBool(PartnerCombatId.HasValue);
        if (PartnerCombatId.HasValue)
        {
            writer.WriteUInt(PartnerCombatId.Value);
        }
    }

    public void Deserialize(PacketReader reader)
    {
        CombatRound = reader.ReadInt();
        PartnerCombatId = reader.ReadBool() ? reader.ReadUInt() : null;
    }

    public override string ToString()
    {
        return "NetLittleRedBlockTransferAction round " + CombatRound;
    }
}
