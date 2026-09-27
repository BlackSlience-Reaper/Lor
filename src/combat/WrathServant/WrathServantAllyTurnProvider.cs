using LibraryOfRuina.encounters.WrathServant;
using LibraryOfRuina.powers.LittleRedMercenary;
using WrathServantMonster = LibraryOfRuina.monsters.WrathServant.WrathServant;

namespace LibraryOfRuina.combat.WrathServant;

internal sealed class WrathServantAllyTurnProvider : IAllyTurnProvider<WrathServantMonster>
{
    public string AllyId => "WrathServant";

    public AllyType AllyType => AllyType.Friendly;

    public AllyPersistence AllyPersistence => AllyPersistence.Encounter;

    public bool IsActiveEncounter(CombatStateLike combatState)
    {
        return WrathServantEncounterHelper.IsWrathServantEncounter(combatState);
    }

    public Creature? FindAlly(CombatStateLike combatState)
    {
        return WrathServantEncounterHelper.FindServant(combatState);
    }

    public bool HasFullAllyTurn => true;

    public bool CanTransferBlock => true;

    public void OnCombatReset(Creature? creature)
    {
        TransferredBlockPower.Clear(creature);
    }
}
