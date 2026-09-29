using LibraryOfRuina.encounters.WarmheartedWoodsman;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.monsters.WarmheartedWoodsman;
using LibraryOfRuina.powers.LittleRedMercenary;

namespace LibraryOfRuina.combat.WarmheartedWoodsman;

internal sealed class WoodsmanTreeAllyTurnProvider : IAllyTurnProvider<WoodsmanTree>
{
    public string AllyId => "WoodsmanTree";

    public AllyType AllyType => AllyType.Neutral;

    public AllyPersistence AllyPersistence => AllyPersistence.Encounter;

    public bool IsActiveEncounter(CombatStateLike combatState)
    {
        return WarmheartedWoodsmanEncounterHelper.IsWarmheartedWoodsmanEncounter(combatState);
    }

    public Creature? FindAlly(CombatStateLike combatState)
    {
        return WarmheartedWoodsmanEncounterHelper.FindTree(combatState);
    }

    public bool HasFullAllyTurn => false;

    public bool CanTransferBlock => true;

    public bool ShouldClearBlockBeforePlayerTurn => true;

    public void OnCombatReset(Creature? creature)
    {
        TransferredBlockPower.Clear(creature);
    }
}
