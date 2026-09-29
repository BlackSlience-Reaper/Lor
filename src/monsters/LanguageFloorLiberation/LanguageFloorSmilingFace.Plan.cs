using System;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Random;

namespace LibraryOfRuina.monsters.LanguageFloorLiberation;

public sealed partial class LanguageFloorSmilingFace
{
    internal async Task RefreshPlannedTargetsAfterRosterChanged(
        bool retargetAll)
    {
        int capacity = GetIntentCapacity(Form);
        for (int slot = 0; slot < capacity; slot++)
        {
            LanguageFloorSmilingFaceMove move = GetPlannedMove(slot);
            if (!IsTargetedMove(move))
            {
                SetPlannedTarget(slot, null);
                continue;
            }

            if (retargetAll || GetPlannedTarget(slot) == null)
            {
                SetPlannedTarget(slot, ChooseRandomTarget(RunRng.MonsterAi));
            }
        }

        RefreshPlannedIntents();
        await RefreshTargetedIntentDisplay();
    }

    internal Task RefreshTargetedIntentDisplay()
    {
        if (Creature.CombatState is not { } combatState)
        {
            return Task.CompletedTask;
        }

        return NCombatRoom.Instance?.GetCreatureNode(Creature)
                   ?.UpdateIntent(combatState.PlayerCreatures)
            ?? Task.CompletedTask;
    }

    private string ChooseNextMoveId(Rng rng)
    {
        FormTurnCount++;
        if (ShouldUseSpecial(Form, FormTurnCount))
        {
            return MoveId(Form == LanguageFloorSmilingFaceForm.Second
                ? LanguageFloorSmilingFaceMove.Scream
                : LanguageFloorSmilingFaceMove.Vomit);
        }

        return MoveId(ChooseNormalMove(rng));
    }

    private void PlanTurn(Rng rng)
    {
        FormTurnCount++;
        int capacity = GetIntentCapacity(Form);
        bool useSpecial = ShouldUseSpecial(Form, FormTurnCount);
        for (int slot = 0; slot < capacity; slot++)
        {
            LanguageFloorSmilingFaceMove move = useSpecial && slot == 0
                ? Form == LanguageFloorSmilingFaceForm.Second
                    ? LanguageFloorSmilingFaceMove.Scream
                    : LanguageFloorSmilingFaceMove.Vomit
                : ChooseNormalMove(rng);
            SetPlannedMove(slot, move);
            SetPlannedTarget(
                slot,
                IsTargetedMove(move) ? ChooseRandomTarget(rng) : null);
        }

        for (int slot = capacity; slot < StoredIntentSlotCount; slot++)
        {
            SetPlannedMove(slot, null);
            SetPlannedTarget(slot, null);
        }

        RefreshPlannedIntents();
    }

    private LanguageFloorSmilingFaceMove ChooseNormalMove(Rng rng)
    {
        if (Form == LanguageFloorSmilingFaceForm.First)
        {
            PreviousNormalMove = (int)LanguageFloorSmilingFaceMove.Devour;
            return LanguageFloorSmilingFaceMove.Devour;
        }

        LanguageFloorSmilingFaceMove[] candidates = Form switch
        {
            LanguageFloorSmilingFaceForm.Second =>
            [
                LanguageFloorSmilingFaceMove.Devour,
                LanguageFloorSmilingFaceMove.Absorb
            ],
            _ =>
            [
                LanguageFloorSmilingFaceMove.Devour,
                LanguageFloorSmilingFaceMove.Absorb,
                LanguageFloorSmilingFaceMove.Sit
            ]
        };
        LanguageFloorSmilingFaceMove[] filtered = candidates
            .Where(move => (int)move != PreviousNormalMove)
            .ToArray();
        LanguageFloorSmilingFaceMove selected = rng.NextItem(
            filtered.Length > 0 ? filtered : candidates);
        PreviousNormalMove = (int)selected;
        return selected;
    }

    internal static bool ShouldUseSpecial(
        LanguageFloorSmilingFaceForm form,
        int formTurn)
    {
        if (formTurn <= 0)
        {
            return false;
        }

        return form switch
        {
            LanguageFloorSmilingFaceForm.Second => (formTurn - 1) % 2 == 0,
            LanguageFloorSmilingFaceForm.Third => (formTurn - 1) % 3 == 0,
            _ => false
        };
    }

    internal static int GetIntentCapacity(
        LanguageFloorSmilingFaceForm form) => form switch
    {
        LanguageFloorSmilingFaceForm.First => FormOneIntentCapacity,
        LanguageFloorSmilingFaceForm.Second => FormTwoIntentCapacity,
        _ => FormThreeIntentCapacity
    };

    private static bool IsTargetedMove(
        LanguageFloorSmilingFaceMove move) => move is
        LanguageFloorSmilingFaceMove.Devour
        or LanguageFloorSmilingFaceMove.Absorb;

    internal LanguageFloorSmilingFaceMove GetPlannedMove(int slot)
    {
        int value = slot switch
        {
            0 => PlannedMoveOne,
            1 => PlannedMoveTwo,
            2 => PlannedMoveThree,
            _ => PlannedMoveFour
        };
        return Enum.IsDefined(typeof(LanguageFloorSmilingFaceMove), value)
            ? (LanguageFloorSmilingFaceMove)value
            : LanguageFloorSmilingFaceMove.Devour;
    }

    internal Creature? GetPlannedTarget(int slot)
    {
        int combatId = slot switch
        {
            0 => PlannedTargetOne,
            1 => PlannedTargetTwo,
            2 => PlannedTargetThree,
            _ => PlannedTargetFour
        };
        if (combatId < 0 || Creature.CombatState is not { } combatState)
        {
            return null;
        }

        return combatState.Creatures.FirstOrDefault(target =>
            target.IsAlive
            && target.CombatId == (uint)combatId);
    }

    private void SetPlannedMove(
        int slot,
        LanguageFloorSmilingFaceMove? move)
    {
        int value = move == null ? -1 : (int)move.Value;
        switch (slot)
        {
            case 0:
                PlannedMoveOne = value;
                break;
            case 1:
                PlannedMoveTwo = value;
                break;
            case 2:
                PlannedMoveThree = value;
                break;
            default:
                PlannedMoveFour = value;
                break;
        }
    }

    private void SetPlannedTarget(int slot, Creature? target)
    {
        int value = target?.CombatId is uint combatId
            ? checked((int)combatId)
            : -1;
        switch (slot)
        {
            case 0:
                PlannedTargetOne = value;
                break;
            case 1:
                PlannedTargetTwo = value;
                break;
            case 2:
                PlannedTargetThree = value;
                break;
            default:
                PlannedTargetFour = value;
                break;
        }
    }

    private void RefreshPlannedIntents()
    {
        RefreshIntentArray(_formOnePlannedIntents);
        RefreshIntentArray(_formTwoPlannedIntents);
        RefreshIntentArray(_formThreePlannedIntents);
    }

    private void RefreshIntentArray(AbstractIntent[]? intents)
    {
        if (intents == null)
        {
            return;
        }

        for (int slot = 0; slot < intents.Length; slot++)
        {
            int plannedSlot = slot;
            intents[slot] = CreateIntent(
                GetPlannedMove(slot),
                _ => GetPlannedTarget(plannedSlot));
        }
    }

    private MoveState GetCurrentCompositeState() => Form switch
    {
        LanguageFloorSmilingFaceForm.First => _formOneCompositeState!,
        LanguageFloorSmilingFaceForm.Second => _formTwoCompositeState!,
        _ => _formThreeCompositeState!
    };

    private bool HasPlannedTurn => Enumerable.Range(0, GetIntentCapacity(Form))
        .All(slot => slot switch
        {
            0 => PlannedMoveOne >= 0,
            1 => PlannedMoveTwo >= 0,
            2 => PlannedMoveThree >= 0,
            _ => PlannedMoveFour >= 0
        });

    internal IReadOnlyList<LanguageFloorSmilingFaceMove> PlannedMoves =>
        Enumerable.Range(0, GetIntentCapacity(Form))
            .Select(GetPlannedMove)
            .ToArray();

    internal IReadOnlyList<Creature?> PlannedTargets =>
        Enumerable.Range(0, GetIntentCapacity(Form))
            .Select(GetPlannedTarget)
            .ToArray();
}
