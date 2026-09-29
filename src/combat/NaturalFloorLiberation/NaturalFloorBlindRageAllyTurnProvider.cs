using LibraryOfRuina.content.abnormalities.LittleRedMercenary;
using LibraryOfRuina.encounters.NaturalFloorLiberation;
using LibraryOfRuina.framework.combat;

namespace LibraryOfRuina.combat.NaturalFloorLiberation;

internal sealed class NaturalFloorBlindRageAllyTurnProvider : IAllyTurnProvider
{
    public string AllyId => "NaturalFloorBlindRage";

    public AllyType AllyType => AllyType.Friendly;

    public AllyPersistence AllyPersistence => AllyPersistence.Encounter;

    public bool IsActiveEncounter(CombatStateLike combatState) =>
        combatState.Encounter is NaturalFloorLiberationEncounter
        {
            CurrentPhase: 2,
            TransitionPending: false,
            SecondPhaseResolutionPending: false
        };

    public Creature? FindAlly(CombatStateLike combatState) =>
        (combatState.Encounter as NaturalFloorLiberationEncounter)?.Rage;

    public bool HasFullAllyTurn => false;

    public bool ShouldClearBlockBeforePlayerTurn => true;

    public bool CanTransferBlock => true;

    public void OnCombatReset(Creature? creature)
    {
        TransferredBlockPower.Clear(creature);
    }
}
