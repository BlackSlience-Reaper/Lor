using LibraryOfRuina.content.abnormalities.LittleRedMercenary;
using LibraryOfRuina.encounters.NaturalFloorLiberation;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.monsters.NaturalFloorLiberation;

namespace LibraryOfRuina.combat.NaturalFloorLiberation;

internal abstract class NaturalFloorMagicalGirlAllyTurnProvider<TGirl> : IAllyTurnProvider<TGirl>
    where TGirl : NaturalFloorMagicalGirl
{
    protected abstract NaturalFloorGirlKind Kind { get; }

    public string AllyId => typeof(TGirl).Name;

    public AllyType AllyType => AllyType.Friendly;

    public AllyPersistence AllyPersistence => AllyPersistence.Encounter;

    public int TurnOrder => (int)Kind;

    public bool CanTransferBlock => true;

    public bool IsActiveEncounter(CombatStateLike state) => state.Encounter is NaturalFloorLiberationEncounter { CurrentPhase: 5, SettlementTriggered: false };

    public Creature? FindAlly(CombatStateLike state) => (state.Encounter as NaturalFloorLiberationEncounter)?.FindNihilGirl(Kind)?.Creature;

    public void OnCombatReset(Creature? creature) => TransferredBlockPower.Clear(creature);
}

internal sealed class NaturalFloorLoveAllyTurnProvider : NaturalFloorMagicalGirlAllyTurnProvider<NaturalFloorLoveGirl>
{
    protected override NaturalFloorGirlKind Kind => NaturalFloorGirlKind.Love;
}

internal sealed class NaturalFloorJusticeAllyTurnProvider : NaturalFloorMagicalGirlAllyTurnProvider<NaturalFloorJusticeGirl>
{
    protected override NaturalFloorGirlKind Kind => NaturalFloorGirlKind.Justice;
}

internal sealed class NaturalFloorHappinessAllyTurnProvider : NaturalFloorMagicalGirlAllyTurnProvider<NaturalFloorHappinessGirl>
{
    protected override NaturalFloorGirlKind Kind => NaturalFloorGirlKind.Happiness;
}

internal sealed class NaturalFloorCourageAllyTurnProvider : NaturalFloorMagicalGirlAllyTurnProvider<NaturalFloorCourageGirl>
{
    protected override NaturalFloorGirlKind Kind => NaturalFloorGirlKind.Courage;
}
