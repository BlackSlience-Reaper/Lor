using System.Linq;
using LibraryOfRuina.encounters.LanguageFloorLiberation;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.monsters.LanguageFloorLiberation;
using LibraryOfRuina.powers.LittleRedMercenary;

namespace LibraryOfRuina.combat.LanguageFloorLiberation;

internal sealed class LanguageFloorScarletAllyTurnProvider : IAllyTurnProvider<LanguageFloorScarletScar>
{
    public string AllyId => nameof(LanguageFloorScarletScar);

    public AllyType AllyType => AllyType.Friendly;

    public AllyPersistence AllyPersistence => AllyPersistence.Encounter;

    public bool IsActiveEncounter(CombatStateLike combatState) =>
        combatState.Encounter is LanguageFloorLiberationEncounter;

    public Creature? FindAlly(CombatStateLike combatState) =>
        combatState.Creatures.FirstOrDefault(
            static creature => creature.Monster is LanguageFloorScarletScar);

    public AllyType ResolveAllyType(Creature ally) =>
        ally.Monster is LanguageFloorScarletScar scarlet
        && (scarlet.IsRaging || scarlet.UnrelievedAnger)
            ? AllyType.Hostile
            : AllyType.Friendly;

    public bool HasFullAllyTurn => true;

    public bool CanTransferBlock => true;

    public bool ShouldClearBlockBeforePlayerTurn => true;
    
    
    public void OnCombatReset(Creature? creature)
    {
        TransferredBlockPower.Clear(creature);
    }
}
