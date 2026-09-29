using LibraryOfRuina.framework.combat;
using LibraryOfRuina.monsters.ScaredyCat;

namespace LibraryOfRuina.combat.ScaredyCat;

internal sealed class ScaredyCatCompanionAllyTurnProvider : IAllyTurnProvider<ScaredyCatCompanion>
{
    public string AllyId => "ScaredyCatCompanion";

    public AllyType AllyType => AllyType.Friendly;

    public AllyPersistence AllyPersistence => AllyPersistence.Player;

    public bool IsActiveEncounter(CombatStateLike combatState)
    {
        return true;
    }

    public Creature? FindAlly(CombatStateLike combatState)
    {
        foreach (var creature in combatState.Creatures)
        {
            if (creature.Monster is ScaredyCatCompanion)
            {
                return creature;
            }
        }
        return null;
    }

    public bool CanTransferBlock => false;

    public void OnCombatReset(Creature? creature)
    {
    }
}
