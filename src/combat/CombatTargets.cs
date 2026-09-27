using System.Linq;

namespace LibraryOfRuina.combat;

internal static class CombatTargets
{
    public static IReadOnlyList<Creature> DeterministicLiving(
        IEnumerable<Creature?>? candidates,
        Creature? exclude = null) =>
        candidates?
            .Where(creature =>
                creature is { IsAlive: true }
                && creature != exclude)
            .Select(static creature => creature!)
            .Distinct()
            .OrderBy(static creature => creature.CombatId)
            .ToArray()
        ?? [];
}
