using System;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.monsters.SocialFloorLiberation;
using LibraryOfRuina.powers.SocialFloorLiberation;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace LibraryOfRuina.encounters.SocialFloorLiberation;

/// <summary>
/// 愤怒试炼：王座恢复眩晕前的抗性并回满混乱值，一名存活玩家拿到奥兹玛。
/// 奥兹玛持有者死亡后，下一个玩家回合开始时重新发给一名存活玩家，魔法粉的费用重置。
/// </summary>
internal sealed class SocialTrialRage : SocialTrial
{
    internal override FalseThroneMove ResolveDefaultMove(int round) =>
        FalseThroneMove.FunIsOver;

    internal override async Task SetupAsync(
        SocialFloorLiberationEncounter encounter,
        FalseThrone boss,
        CombatStateLike combatState)
    {
        if (boss.Creature is LibraryCreature
            libraryCreature)
        {
            libraryCreature.RestorePreStunResistance();
            await LibraryCreatureCmd
                .SetCurrentChaoValue(
                    libraryCreature,
                    libraryCreature.MaxChaoValue);
        }
        await GrantOzma(
            encounter,
            combatState,
            SocialFloorLiberationEncounter.InitialPowderCost);
    }

    internal override Task AfterEnemyTurnAsync(
        SocialFloorLiberationEncounter encounter,
        PlayerChoiceContext choiceContext,
        FalseThrone boss,
        CombatStateLike combatState)
    {
        encounter.PlannedMove = FalseThroneMove.FunIsOver;
        boss.ForcePlannedMove(encounter.PlannedMove);
        return Task.CompletedTask;
    }

    internal override async Task BeforePlayerTurnAsync(
        SocialFloorLiberationEncounter encounter,
        FalseThrone boss,
        CombatStateLike combatState)
    {
        if (!encounter.OzmaReplacementPending)
        {
            return;
        }

        await GrantOzma(
            encounter,
            combatState,
            SocialFloorLiberationEncounter.InitialPowderCost);
    }

    internal override async Task RestoreAfterSharedStateAsync(
        SocialFloorLiberationEncounter encounter,
        CombatStateLike combatState)
    {
        if (encounter.OzmaReplacementPending)
        {
            return;
        }

        Player? ozmaHolder = SocialFloorLiberationEncounter.ResolveLivingPlayer(
            combatState,
            encounter.OzmaPlayerNetId);
        if (ozmaHolder == null)
        {
            encounter.OzmaPlayerNetId = null;
            encounter.OzmaReplacementPending = true;
        }
        else
        {
            await SocialFloorPlayerMechanics.RestoreOzma(
                ozmaHolder,
                encounter.PowderCost,
                ensurePowder: !encounter.Transformed);
        }
    }

    internal override async Task OnPlayerDiedAsync(
        SocialFloorLiberationEncounter encounter,
        Player player,
        ulong netId)
    {
        if (encounter.OzmaPlayerNetId == netId)
        {
            await SocialFloorPlayerMechanics.InvalidateOzma(player);
            encounter.OzmaReplacementPending = true;
            encounter.OzmaPlayerNetId = null;
        }
    }

    private static async Task GrantOzma(
        SocialFloorLiberationEncounter encounter,
        CombatStateLike combatState,
        int initialCost)
    {
        Player? holder = SocialFloorLiberationEncounter.ResolveLivingPlayer(
            combatState,
            encounter.OzmaPlayerNetId);
        holder ??= SocialFloorLiberationEncounter.ChooseRandomLivingPlayer(combatState);
        if (holder == null)
        {
            return;
        }

        encounter.OzmaPlayerNetId = holder.NetId;
        encounter.PowderCost = Math.Max(0, initialCost);
        encounter.OzmaReplacementPending = false;
        await SocialFloorPlayerMechanics.GrantOzma(holder, encounter.PowderCost);
    }
}
