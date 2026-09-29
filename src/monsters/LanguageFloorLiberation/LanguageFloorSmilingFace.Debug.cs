using System.Threading.Tasks;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Random;

namespace LibraryOfRuina.monsters.LanguageFloorLiberation;

using BaseSmilingBodies =
    SmilingBodies.SmilingBodies;

// 只给验证程序集（InternalsVisibleTo）用的入口，正式流程不调用。
public sealed partial class LanguageFloorSmilingFace
{
    internal async Task DebugTransitionToForm(
        LanguageFloorSmilingFaceForm form) =>
        await TransitionToForm(form, BaseSmilingBodies.PhaseDownSfxPath);

    internal Task DebugPerformMove(LanguageFloorSmilingFaceMove move) =>
        move switch
        {
            LanguageFloorSmilingFaceMove.Devour => DevourMove([]),
            LanguageFloorSmilingFaceMove.Absorb => AbsorbMove([]),
            LanguageFloorSmilingFaceMove.Sit => SitMove([]),
            LanguageFloorSmilingFaceMove.Scream => ScreamMove([]),
            _ => VomitMove([])
        };

    internal string DebugChooseNextMoveId(Rng rng) => ChooseNextMoveId(rng);

    internal string DebugChooseNextMoveId() =>
        ChooseNextMoveId(RunRng.MonsterAi);

    internal void DebugSetMovePlanState(
        LanguageFloorSmilingFaceForm form,
        int formTurnCount = 0,
        int previousNormalMove = -1)
    {
        Form = form;
        FormTurnCount = formTurnCount;
        PreviousNormalMove = previousNormalMove;
    }

    internal Creature? DebugChooseDevourTarget() => GetPlannedTarget(0);

    internal void DebugSetNextMove(LanguageFloorSmilingFaceMove move) =>
        SetMoveImmediate(GetMoveState(MoveId(move)), forceTransition: true);

    internal void DebugPlanTurn()
    {
        PlanTurn(RunRng.MonsterAi);
        if (_formOneCompositeState != null
            && _formTwoCompositeState != null
            && _formThreeCompositeState != null)
        {
            SetMoveImmediate(GetCurrentCompositeState(), forceTransition: true);
        }
    }

    internal void DebugSetPlan(
        LanguageFloorSmilingFaceForm form,
        int formTurnCount,
        IReadOnlyList<LanguageFloorSmilingFaceMove> moves,
        IReadOnlyList<Creature?> targets)
    {
        Form = form;
        FormTurnCount = formTurnCount;
        for (int slot = 0; slot < StoredIntentSlotCount; slot++)
        {
            SetPlannedMove(slot, slot < moves.Count ? moves[slot] : null);
            SetPlannedTarget(slot, slot < targets.Count ? targets[slot] : null);
        }

        RefreshPlannedIntents();
        if (_formOneCompositeState != null
            && _formTwoCompositeState != null
            && _formThreeCompositeState != null)
        {
            SetMoveImmediate(GetCurrentCompositeState(), forceTransition: true);
        }
    }

    internal Task DebugPerformPlannedMove(
        LanguageFloorSmilingFaceMove move,
        int slot) => PerformPlannedMove(move, slot);

    internal Task DebugResolvePlayerTurnStart(
        PlayerChoiceContext choiceContext,
        CombatStateLike combatState) =>
        ResolvePlayerTurnStart(choiceContext, combatState);

    internal async Task DebugRevive()
    {
        await ReviveMove([]);
        await ResolvePendingFormTransition();
    }
}
