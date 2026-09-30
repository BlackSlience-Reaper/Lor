using LibraryOfRuina.framework.combat;

namespace LibraryOfRuina.content.abnormalities.LittleRedMercenary;

internal sealed class LittleRedAllyTurnProvider : IAllyTurnProvider<LittleRedRidingHoodedMercenary>
{
    public string AllyId => "LittleRed";

    public AllyType AllyType => AllyType.Friendly;

    public AllyPersistence AllyPersistence => AllyPersistence.Encounter;

    public AllyType ResolveAllyType(Creature ally) =>
        ally.Monster is LittleRedRidingHoodedMercenary littleRed
        && (littleRed.IsRaging || littleRed.IsUnrelievedAnger)
            ? AllyType.Hostile
            : AllyType.Friendly;

    public bool IsActiveEncounter(CombatStateLike combatState)
    {
        return LittleRedMercenaryEncounterHelper.IsLittleRedEncounter(combatState);
    }

    public Creature? FindAlly(CombatStateLike combatState)
    {
        return LittleRedMercenaryEncounterHelper.FindLittleRed(combatState);
    }

    public bool HasFullAllyTurn => true;

    public bool CanTransferBlock => true;

    public bool ShouldClearBlockBeforePlayerTurn => true;

    public void OnCombatReset(Creature? creature)
    {
        TransferredBlockPower.Clear(creature);
    }
}
