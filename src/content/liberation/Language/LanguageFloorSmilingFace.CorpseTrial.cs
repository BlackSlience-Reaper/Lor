using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.encounters;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.encounters;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.content.liberation.Language;

using BaseSmilingBodies =
    abnormalities.SmilingBodies.SmilingBodies;

public sealed partial class LanguageFloorSmilingFace
{
    public async Task OnCorpseDeath()
    {
        if (Creature.CombatState is not { } combatState)
        {
            return;
        }

        Log.Info(
            "[LanguageFloorSmilingFace] corpse death resolved "
            + $"trialActive={CorpseTrialActive} "
            + $"livingCorpses={CountLivingCorpses(combatState)}");
        await RefreshPlannedTargetsAfterRosterChanged(retargetAll: false);
        if (!CorpseTrialActive
            || CountLivingCorpses(combatState) > 0)
        {
            return;
        }

        Log.Info("[LanguageFloorSmilingFace] corpse trial won; completing phase three.");
        if (combatState.Encounter is LanguageFloorLiberationEncounter encounter)
        {
            await encounter.CompletePhaseThree(this, combatState);
        }
    }

    internal async Task TickCorpseTrialOnPlayerTurnStart(
        CombatStateLike combatState)
    {
        int playerTurn = GetCurrentPlayerTurn(combatState);
        if (playerTurn >= 0 && playerTurn == _lastCorpseTrialTickPlayerTurn)
        {
            return;
        }

        if (playerTurn >= 0)
        {
            _lastCorpseTrialTickPlayerTurn = playerTurn;
        }

        if (!CorpseTrialPending && !CorpseTrialActive)
        {
            return;
        }

        Log.Info(
            "[LanguageFloorSmilingFace] corpse trial tick "
            + $"turn={playerTurn} pending={CorpseTrialPending} "
            + $"active={CorpseTrialActive} "
            + $"remaining={CorpseTrialPlayerTurnsRemaining} "
            + $"corpses={CountLivingCorpses(combatState)}");
        await ResolveCorpseTrial(combatState);
    }

    private async Task<bool> ResolveCorpseTrial(CombatStateLike combatState)
    {
        if (CorpseTrialPending)
        {
            CorpseTrialPending = false;
            CorpseTrialActive = true;
            CorpseTrialPlayerTurnsRemaining = TrialPlayerTurns;
            PendingCorpseSpawns = 0;
            Log.Info(
                "[LanguageFloorSmilingFace] corpse trial activated "
                + $"turns={CorpseTrialPlayerTurnsRemaining} "
                + $"corpses={CountLivingCorpses(combatState)}");
            await FillCorpsesToCount(combatState, TrialCorpseCount);
            return true;
        }

        if (!CorpseTrialActive)
        {
            return false;
        }

        int livingCorpses = CountLivingCorpses(combatState);
        if (livingCorpses == 0)
        {
            await OnCorpseDeath();
            return true;
        }

        CorpseTrialPlayerTurnsRemaining--;
        Log.Info(
            "[LanguageFloorSmilingFace] corpse trial decrement "
            + $"remaining={CorpseTrialPlayerTurnsRemaining} "
            + $"corpses={livingCorpses}");
        if (CorpseTrialPlayerTurnsRemaining <= 0)
        {
            await FailCorpseTrial(combatState);
        }

        return true;
    }

    private static int GetCurrentPlayerTurn(CombatStateLike combatState)
    {
        return combatState.Players
            .FirstOrDefault(static player =>
                player.PlayerCombatState != null)
            ?.PlayerCombatState?.TurnNumber
            ?? -1;
    }

    private void QueueCorpseSpawnsForThresholds()
    {
        if (Creature.MaxHp <= 0 || HpAtLastSpawnThreshold <= 0)
        {
            return;
        }

        int threshold = Math.Max(
            1,
            (int)Math.Ceiling(
                Creature.MaxHp * CorpseSpawnThresholdPercent / 100m));
        while (HpAtLastSpawnThreshold - Creature.CurrentHp >= threshold)
        {
            PendingCorpseSpawns++;
            HpAtLastSpawnThreshold -= threshold;
        }
    }

    private async Task ResolvePendingCorpseSpawns(CombatStateLike combatState)
    {
        int available = MaxCorpseCount - CountLivingCorpses(combatState);
        int count = Math.Min(PendingCorpseSpawns, Math.Max(0, available));
        for (int i = 0; i < count; i++)
        {
            if (!await SpawnOneCorpse(combatState))
            {
                break;
            }

            PendingCorpseSpawns--;
        }

        combatState.SortEnemiesBySlotName();
        await RefreshPlannedTargetsAfterRosterChanged(retargetAll: true);
    }

    private async Task FillCorpsesToCount(
        CombatStateLike combatState,
        int targetCount)
    {
        while (CountLivingCorpses(combatState) < targetCount)
        {
            if (!await SpawnOneCorpse(combatState))
            {
                break;
            }
        }

        combatState.SortEnemiesBySlotName();
        await RefreshPlannedTargetsAfterRosterChanged(retargetAll: true);
    }

    private async Task<bool> SpawnOneCorpse(CombatStateLike combatState)
    {
        string? slot = NextOpenCorpseSlot(combatState);
        if (slot == null)
        {
            return false;
        }

        Creature corpse = await CreatureCmd.Add(
            ModelDb.Monster<LanguageFloorMeltingCorpse>().ToMutable(),
            combatState,
            CombatSide.Enemy,
            slot);
        corpse.PrepareForNextTurn(combatState.PlayerCreatures);
        return true;
    }

    private async Task FailCorpseTrial(CombatStateLike combatState)
    {
        Log.Info("[LanguageFloorSmilingFace] corpse trial failed; resetting to form one.");
        CorpseTrialActive = false;
        CorpseTrialPlayerTurnsRemaining = 0;
        foreach (Creature corpse in combatState.Enemies
                     .Where(static enemy =>
                         enemy.Monster is LanguageFloorMeltingCorpse)
                     .ToArray())
        {
            await LiberationPhaseCleanup.RemoveTransitionCreature(
                corpse,
                combatState);
        }

        LocalOggOneShotPlayer.Play(BaseSmilingBodies.PhaseUpSfxPath, -1.5f);
        PendingCorpseSpawns = EntryCorpseCount;
        await ApplyFormStats(LanguageFloorSmilingFaceForm.First);
        ResetFormMoves();
        ResetSpawnThreshold();
        ResetMoveStateForCurrentForm();
        await RefreshFormPowers();
        Creature.PrepareForNextTurn(combatState.PlayerCreatures);
        Log.Info(
            "[LanguageFloorSmilingFace] corpse trial fail complete "
            + $"nextMove={NextMove.Id}");
    }

    private static int CountLivingCorpses(CombatStateLike combatState) =>
        combatState.Enemies.Count(static enemy =>
            enemy.IsAlive && enemy.Monster is LanguageFloorMeltingCorpse);

    private static string? NextOpenCorpseSlot(CombatStateLike combatState) =>
        LanguageFloorLiberationEncounter.CorpseSlots.FirstOrDefault(slot =>
            combatState.Enemies.All(enemy =>
                enemy.SlotName != slot || !enemy.IsAlive));
}
