using LibraryOfRuina.encounters.RoadHome;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.monsters.RoadHome;
using LibraryOfRuina.powers.LittleRedMercenary;

namespace LibraryOfRuina.combat.RoadHome;

internal sealed class RoadHomeHouseAllyTurnProvider : IAllyTurnProvider<RoadHomeHouse>
{
    public string AllyId => nameof(RoadHomeHouse);

    public AllyType AllyType => AllyType.Friendly;

    public AllyPersistence AllyPersistence => AllyPersistence.Encounter;

    public bool IsActiveEncounter(CombatStateLike combatState) =>
        RoadHomeEncounterHelper.IsRoadHomeElite(combatState);

    public Creature? FindAlly(CombatStateLike combatState) =>
        RoadHomeEncounterHelper.FindHouse(combatState);

    public bool HasFullAllyTurn => false;

    public bool CanTransferBlock => true;

    public bool ShouldClearBlockBeforePlayerTurn => true;

    public void OnCombatReset(Creature? creature)
    {
        TransferredBlockPower.Clear(creature);
    }
}
