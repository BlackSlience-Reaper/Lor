using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.content.liberation.Social;

/// <summary>
/// 樵夫试炼：三个翡翠水晶。第 2 回合结束时水晶已全部摧毁、或第 3 回合结束时，进入稻草人试炼；
/// 第 2 回合的计划行动是大错误，这时移除水晶，之后不再补回（见 <c>ShouldKeepWoodsmanSummons</c>）。
/// </summary>
internal sealed class SocialTrialWoodsman : SocialTrial
{
    internal override FalseThroneMove ResolveDefaultMove(int round) =>
        round >= 2
            ? FalseThroneMove.BigMistake
            : FalseThroneMove.Manners;

    internal override void AddRoomSummons(
        SocialFloorLiberationEncounter encounter,
        List<(MonsterModel, string?)> monsters)
    {
        if (!encounter.ShouldKeepWoodsmanSummons)
        {
            return;
        }

        for (int index = 0; index < SocialFloorLiberationEncounter.CrystalSlots.Count; index++)
        {
            if ((encounter.DestroyedCrystalMask & (1 << index)) == 0)
            {
                monsters.Add((
                    ModelDb.Monster<EmeraldCrystal>().ToMutable(),
                    SocialFloorLiberationEncounter.CrystalSlots[index]));
            }
        }
    }

    internal override async Task SetupAsync(
        SocialFloorLiberationEncounter encounter,
        FalseThrone boss,
        CombatStateLike combatState)
    {
        encounter.DestroyedCrystalMask = 0;
        if (encounter.ShouldKeepWoodsmanSummons)
        {
            await SpawnMissingCrystals(encounter, combatState);
        }
    }

    internal override async Task SpawnMissingSummonsAsync(
        SocialFloorLiberationEncounter encounter,
        CombatStateLike combatState)
    {
        if (encounter.ShouldKeepWoodsmanSummons)
        {
            await SpawnMissingCrystals(encounter, combatState);
        }
    }

    internal override async Task AfterEnemyTurnAsync(
        SocialFloorLiberationEncounter encounter,
        PlayerChoiceContext choiceContext,
        FalseThrone boss,
        CombatStateLike combatState)
    {
        if (encounter.TrialRound >= 2 && encounter.AreAllCrystalsDestroyed)
        {
            encounter.QueueTrial(SocialFloorTrial.Scarecrow);
            await encounter.EnterPendingTrial(choiceContext, boss, combatState);
        }
        else if (encounter.TrialRound >= 3)
        {
            encounter.QueueTrial(SocialFloorTrial.Scarecrow);
            await encounter.EnterPendingTrial(choiceContext, boss, combatState);
        }
        else
        {
            encounter.PlannedMove = encounter.TrialRound >= 2
                ? FalseThroneMove.BigMistake
                : FalseThroneMove.Manners;
            if (encounter.PlannedMove == FalseThroneMove.BigMistake)
            {
                await RemoveTrialSummons<EmeraldCrystal>(combatState);
            }
            boss.ForcePlannedMove(encounter.PlannedMove);
        }
    }

    internal override async Task CleanupAsync(
        SocialFloorLiberationEncounter encounter,
        CombatStateLike combatState)
    {
        // Trial has already advanced, so the conditional max-energy
        // restriction is lifted before any asynchronous cleanup runs.
        await RemoveTrialSummons<EmeraldCrystal>(combatState);
    }

    private static async Task SpawnMissingCrystals(
        SocialFloorLiberationEncounter encounter,
        CombatStateLike combatState)
    {
        for (int index = 0; index < SocialFloorLiberationEncounter.CrystalSlots.Count; index++)
        {
            int bit = 1 << index;
            string slot = SocialFloorLiberationEncounter.CrystalSlots[index];
            if ((encounter.DestroyedCrystalMask & bit) != 0
                || combatState.Enemies.Any(enemy =>
                    enemy.IsAlive
                    && enemy.Monster is EmeraldCrystal
                    && enemy.SlotName == slot))
            {
                continue;
            }

            Creature spawned = await CreatureCmd.Add(
                ModelDb.Monster<EmeraldCrystal>().ToMutable(),
                combatState,
                CombatSide.Enemy,
                slot);
            spawned.PrepareForNextTurn(combatState.PlayerCreatures);
        }

        combatState.SortEnemiesBySlotName();
    }
}
