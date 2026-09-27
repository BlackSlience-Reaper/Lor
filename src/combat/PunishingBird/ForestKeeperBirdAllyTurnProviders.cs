using System.Linq;
using LibraryOfRuina.encounters.PunishingBird;
using LibraryOfRuina.monsters.PunishingBird;
using LibraryOfRuina.powers.LittleRedMercenary;

namespace LibraryOfRuina.combat.PunishingBird;

internal abstract class ForestKeeperBirdAllyTurnProvider<TKeeper> : IAllyTurnProvider<TKeeper>
    where TKeeper : ForestKeeperBirdBase
{
    public string AllyId => typeof(TKeeper).Name;

    public AllyType AllyType => AllyType.Friendly;

    public AllyPersistence AllyPersistence => AllyPersistence.Encounter;

    public bool CanTransferBlock => true;

    public bool IsActiveEncounter(CombatStateLike combatState) =>
        PunishingBirdEncounterHelper.IsEncounter(combatState);

    public Creature? FindAlly(CombatStateLike combatState) =>
        combatState.Creatures.FirstOrDefault(static creature => creature.Monster is TKeeper);

    public void OnCombatReset(Creature? creature) => TransferredBlockPower.Clear(creature);
}

internal sealed class ForestKeeperBirdLeftAllyTurnProvider
    : ForestKeeperBirdAllyTurnProvider<ForestKeeperBirdLeft>;

internal sealed class ForestKeeperBirdRightAllyTurnProvider
    : ForestKeeperBirdAllyTurnProvider<ForestKeeperBirdRight>;
