using LibraryOfRuina.encounters.NaturalFloorLiberation;

namespace LibraryOfRuina.events.NaturalFloorLiberation;

internal static class NaturalFloorLiberationSettlementStore
{
    public const int MinimumKilledBossCount = 1;

    public static int KilledBossCount { get; private set; }

    public static bool PendingSettlement { get; private set; }

    public static bool NihilCompleted { get; private set; }

    public static void Record(NaturalFloorLiberationEncounter encounter)
    {
        KilledBossCount = encounter.KilledBossCount;
        NihilCompleted = encounter.NihilCompleted;
        PendingSettlement = encounter.SettlementTriggered && KilledBossCount >= MinimumKilledBossCount;
    }

    public static void Consume() => PendingSettlement = false;

    internal static void Restore(int kills, bool nihilCompleted)
    {
        KilledBossCount = System.Math.Clamp(kills, 1, NaturalFloorLiberationEncounter.PlannedMaxPhase);
        NihilCompleted = nihilCompleted;
        PendingSettlement = false;
    }
}
