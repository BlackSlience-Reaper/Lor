using System.Threading.Tasks;

namespace LibraryOfRuina.framework.encounters;

/// <summary>
/// Hosts that can spawn worker bees when a player's
/// <see cref="powers.HistoryFloorLiberation.HistoryFloorWaspSporePower"/>
/// collapses to zero. The History Floor liberation battle and the Queen Bee
/// elite each enforce their own worker cap and slots.
/// </summary>
internal interface ISporeWorkerSpawner
{
    bool CanSpawnSporeWorkers { get; }

    Task TrySpawnSporeWorker(CombatStateLike combatState);
}
