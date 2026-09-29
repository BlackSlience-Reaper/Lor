using LibraryOfRuina.content.abnormalities.LittleRedMercenary;
using LibraryOfRuina.framework.combat;
using WrathServantMonster = LibraryOfRuina.content.abnormalities.WrathServant.WrathServant;

namespace LibraryOfRuina.content.abnormalities.WrathServant;

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
