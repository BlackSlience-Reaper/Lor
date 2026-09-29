using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.audio;
using LibraryOfRuina.compat;
using LibraryOfRuina.encounters.LanguageFloorLiberation;
using LibraryOfRuina.helpers;
using LibraryOfRuina.patches;
using LibraryOfRuina.powers.LanguageFloorLiberation;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;

namespace LibraryOfRuina.monsters.LanguageFloorLiberation;

using BaseSmilingBodies =
    SmilingBodies.SmilingBodies;

public sealed partial class LanguageFloorSmilingFace
{
    public bool CanEnterFakeDeath(Creature creature) =>
        creature == Creature
        && !ForceKillable
        && !IsFakeDead
        && Creature.CombatState?.Encounter
            is LanguageFloorLiberationEncounter { PhaseComplete: false, CurrentPhase: 3 };

    public async Task EnterFakeDeathFromDeath()
    {
        if (IsFakeDead)
        {
            return;
        }

        if (!CanEnterFakeDeath(Creature))
        {
            ForceKillable = true;
            return;
        }

        await FakeDeathDebuffHelper.ClearDebuffs(Creature);
        if (Form == LanguageFloorSmilingFaceForm.First)
        {
            PendingCorpseSpawns = 0;
            CorpseTrialPending = true;
            CorpseTrialActive = false;
            CorpseTrialPlayerTurnsRemaining = 0;
        }
        else
        {
            WaitingForDowngrade = true;
            FakeDeathPlayerTurnsRemaining = 0;
            QueueFormTransition(
                Form == LanguageFloorSmilingFaceForm.Third
                    ? LanguageFloorSmilingFaceForm.Second
                    : LanguageFloorSmilingFaceForm.First,
                isPromotion: false);
        }

        if (Form == LanguageFloorSmilingFaceForm.First)
        {
            EnterHiddenFakeDeathIntent();
        }
        else
        {
            EnterReviveIntent();
        }

        Log.Info(
            "[LanguageFloorSmilingFace] fake-death enter "
            + $"form={Form} pending={CorpseTrialPending} "
            + $"active={CorpseTrialActive}");
    }

    public async Task OnAllyKilled()
    {
        if (Creature.IsDead || IsFakeDead)
        {
            return;
        }

        int percent = Form == LanguageFloorSmilingFaceForm.Third ? 30 : 40;
        int amount = Math.Max(
            1,
            (int)Math.Ceiling(Creature.MaxHp * percent / 100m));
        await CreatureCmd.Heal(Creature, amount);
    }

    public void MarkEncounterComplete()
    {
        ForceKillable = true;
        WaitingForDowngrade = false;
        CorpseTrialPending = false;
        CorpseTrialActive = false;
        CorpseTrialPlayerTurnsRemaining = 0;
        FakeDeathPlayerTurnsRemaining = 0;
        PendingFormTransition = -1;
        PendingFormTransitionIsPromotion = false;
    }

    private async Task GainPermanentStrong(PlayerChoiceContext choiceContext)
    {
        LibraryStrongPower? existing = Creature
            .GetPowerInstances<LibraryStrongPower>()
            .FirstOrDefault(static power => power.AmountPlan.Count == 0);
        if (existing == null)
        {
            await LibraryPowerCmd.Apply<LibraryStrongPower>(new ThrowingPlayerChoiceContext(),
                Creature,
                PermanentStrongPerPlayerTurn,
                0,
                true,
                Creature,
                null);
        }
        else
        {
            await PowerCmd.ModifyAmount(
                choiceContext,
                existing,
                PermanentStrongPerPlayerTurn,
                Creature,
                null);
        }
    }

    private Task TryPromoteAtFullHealth()
    {
        if (_isTransitioning
            || Creature.IsDead
            || IsFakeDead
            || PendingFormTransition >= 0
            || Creature.CurrentHp < Creature.MaxHp)
        {
            return Task.CompletedTask;
        }

        LanguageFloorSmilingFaceForm? next = Form switch
        {
            LanguageFloorSmilingFaceForm.First =>
                LanguageFloorSmilingFaceForm.Second,
            LanguageFloorSmilingFaceForm.Second =>
                LanguageFloorSmilingFaceForm.Third,
            _ => null
        };
        if (next != null)
        {
            QueueFormTransition(next.Value, isPromotion: true);
        }

        return Task.CompletedTask;
    }

    private void QueueFormTransition(
        LanguageFloorSmilingFaceForm target,
        bool isPromotion)
    {
        if (target == Form)
        {
            return;
        }

        PendingFormTransition = (int)target;
        PendingFormTransitionIsPromotion = isPromotion;
    }

    private async Task ResolvePendingFormTransition()
    {
        if (!Enum.IsDefined(
                typeof(LanguageFloorSmilingFaceForm),
                PendingFormTransition))
        {
            return;
        }

        var target =
            (LanguageFloorSmilingFaceForm)PendingFormTransition;
        string sfxPath = PendingFormTransitionIsPromotion
            ? BaseSmilingBodies.PhaseUpSfxPath
            : BaseSmilingBodies.PhaseDownSfxPath;
        PendingFormTransition = -1;
        PendingFormTransitionIsPromotion = false;
        WaitingForDowngrade = false;
        FakeDeathPlayerTurnsRemaining = 0;
        await TransitionToForm(target, sfxPath);
    }

    private async Task TransitionToForm(
        LanguageFloorSmilingFaceForm target,
        string sfxPath)
    {
        if (_isTransitioning || Form == target)
        {
            return;
        }

        _isTransitioning = true;
        try
        {
            Form = target;
            PendingCorpseSpawns += target == LanguageFloorSmilingFaceForm.Third
                ? 0
                : EntryCorpseCount;
            ResetFormMoves();
            LocalOggOneShotPlayer.Play(sfxPath, -1.5f);
            await CreatureCmd.TriggerAnim(Creature, "Phase", 0.55f);
            if (!CombatManager.Instance.IsInProgress
                || Creature.CombatState is not { } combatState)
            {
                Log.Warn(
                    "[LanguageFloorSmilingFace] skipped transition tail because combat ended.");
                return;
            }

            await ApplyFormStats(target);
            ResetSpawnThreshold();
            ResetMoveStateForCurrentForm();
            await RefreshFormPowers();
            Creature.PrepareForNextTurn(combatState.PlayerCreatures);
        }
        finally
        {
            _isTransitioning = false;
        }
    }

    private async Task ApplyFormStats(
        LanguageFloorSmilingFaceForm form,
        bool resetMaxHp = true)
    {
        _isApplyingFormStats = true;
        try
        {
            if (resetMaxHp)
            {
                int maxHp = await ResolveFormMaxHp(form);
                await CreatureCmd.SetMaxHp(Creature, maxHp);
            }

            decimal entryHp = MultiplayerScalingPatchHelper.ScaleHpAmount(
                Creature.CombatState,
                this,
                EntryHp(form));
            await CreatureCmd.SetCurrentHp(
                Creature,
                Math.Min(Creature.MaxHp, entryHp));

            if (Creature is LibraryCreature libraryCreature)
            {
                await MultiplayerScalingPatchHelper.SetMonsterBaseMaxAndCurrentChaoValue(
                    libraryCreature,
                    MaxChaoResistance);
            }
        }
        finally
        {
            _isApplyingFormStats = false;
        }
    }

    private async Task<int> ResolveFormMaxHp(
        LanguageFloorSmilingFaceForm form)
    {
        int saved = form switch
        {
            LanguageFloorSmilingFaceForm.First => FormOneMaxHp,
            LanguageFloorSmilingFaceForm.Second => FormTwoMaxHp,
            _ => FormThreeMaxHp
        };
        if (saved > 0)
        {
            return saved;
        }

        (int min, int max) = FormMaxHpRange(form);
        int rolled = RunRng.MonsterAi.NextInt(min, max + 1);
        decimal scaled = MultiplayerScalingPatchHelper.ScaleHpAmount(
            Creature.CombatState,
            this,
            rolled);
        await CreatureCmd.SetMaxHp(Creature, scaled);
        int actual = Creature.MaxHp;
        switch (form)
        {
            case LanguageFloorSmilingFaceForm.First:
                FormOneMaxHp = actual;
                break;
            case LanguageFloorSmilingFaceForm.Second:
                FormTwoMaxHp = actual;
                break;
            default:
                FormThreeMaxHp = actual;
                break;
        }

        return actual;
    }

    private async Task RefreshFormPowers()
    {
        await PowerCmdCompat.Ensure<LanguageFloorSmilingFaceFindCorpsesPower>(
            Creature);
        if (Form == LanguageFloorSmilingFaceForm.First)
        {
            await PowerCmdCompat.Ensure<
                LanguageFloorSmilingFaceFormOneSplitPower>(Creature);
            await PowerCmdCompat.Ensure<LanguageFloorSmilingFaceFusionPower>(
                Creature);
            await PowerCmdCompat.RemoveIfPresent<
                LanguageFloorSmilingFaceSplitAndFusionPower>(Creature);
            await PowerCmdCompat.RemoveIfPresent<
                LanguageFloorSmilingFaceScreamPower>(Creature);
            await PowerCmdCompat.RemoveIfPresent<
                LanguageFloorSmilingFaceFormThreeSplitPower>(Creature);
            await PowerCmdCompat.RemoveIfPresent<
                LanguageFloorSmilingFaceVomitPower>(Creature);
            return;
        }

        if (Form == LanguageFloorSmilingFaceForm.Second)
        {
            await PowerCmdCompat.Ensure<
                LanguageFloorSmilingFaceSplitAndFusionPower>(Creature);
            await PowerCmdCompat.Ensure<LanguageFloorSmilingFaceScreamPower>(
                Creature);
            await PowerCmdCompat.RemoveIfPresent<
                LanguageFloorSmilingFaceFormOneSplitPower>(Creature);
            await PowerCmdCompat.RemoveIfPresent<
                LanguageFloorSmilingFaceFusionPower>(Creature);
            await PowerCmdCompat.RemoveIfPresent<
                LanguageFloorSmilingFaceFormThreeSplitPower>(Creature);
            await PowerCmdCompat.RemoveIfPresent<
                LanguageFloorSmilingFaceVomitPower>(Creature);
            return;
        }

        await PowerCmdCompat.Ensure<
            LanguageFloorSmilingFaceFormThreeSplitPower>(Creature);
        await PowerCmdCompat.Ensure<LanguageFloorSmilingFaceVomitPower>(
            Creature);
        await PowerCmdCompat.RemoveIfPresent<
            LanguageFloorSmilingFaceFormOneSplitPower>(Creature);
        await PowerCmdCompat.RemoveIfPresent<
            LanguageFloorSmilingFaceFusionPower>(Creature);
        await PowerCmdCompat.RemoveIfPresent<
            LanguageFloorSmilingFaceSplitAndFusionPower>(Creature);
        await PowerCmdCompat.RemoveIfPresent<
            LanguageFloorSmilingFaceScreamPower>(Creature);
    }

    private void ResetFormMoves()
    {
        FormTurnCount = 0;
        PreviousNormalMove = -1;
        for (int slot = 0; slot < StoredIntentSlotCount; slot++)
        {
            SetPlannedMove(slot, null);
            SetPlannedTarget(slot, null);
        }

        RefreshPlannedIntents();
    }

    private void ResetSpawnThreshold()
    {
        HpAtLastSpawnThreshold = Creature.CurrentHp;
    }

    private void ResetMoveStateForCurrentForm()
    {
        ResetStateMachine();
        SetUpForCombat();
    }

    private void EnterHiddenFakeDeathIntent()
    {
        if (_fakeDeathHiddenState != null)
        {
            SetMoveImmediate(_fakeDeathHiddenState, forceTransition: true);
        }
    }

    private void EnterReviveIntent()
    {
        if (_reviveState != null)
        {
            SetMoveImmediate(_reviveState, forceTransition: true);
        }
    }

    private Task ReviveMove(IReadOnlyList<Creature> targets)
    {
        if (!WaitingForDowngrade || PendingFormTransition < 0)
        {
            return Task.CompletedTask;
        }

        return Task.CompletedTask;
    }

    private static int EntryHp(LanguageFloorSmilingFaceForm form) =>
        form switch
        {
            LanguageFloorSmilingFaceForm.First => FormOneEntryHp,
            LanguageFloorSmilingFaceForm.Second => FormTwoEntryHp,
            _ => FormThreeEntryHp
        };

    private static (int Min, int Max) FormMaxHpRange(
        LanguageFloorSmilingFaceForm form)
    {
        return form switch
        {
            LanguageFloorSmilingFaceForm.First =>
                (AscensionHelper.GetValueIfAscension(
                        AscensionLevel.ToughEnemies, 197, 190),
                    AscensionHelper.GetValueIfAscension(
                        AscensionLevel.ToughEnemies, 200, 193)),
            LanguageFloorSmilingFaceForm.Second =>
                (AscensionHelper.GetValueIfAscension(
                        AscensionLevel.ToughEnemies, 297, 290),
                    AscensionHelper.GetValueIfAscension(
                        AscensionLevel.ToughEnemies, 300, 293)),
            _ =>
                (AscensionHelper.GetValueIfAscension(
                        AscensionLevel.ToughEnemies, 348, 340),
                    AscensionHelper.GetValueIfAscension(
                        AscensionLevel.ToughEnemies, 350, 345))
        };
    }
}
