using System;

namespace LibraryOfRuina.content.liberation.Technology;

internal readonly record struct TechnologyFloorLiberationSettlementData(
    int KilledBossCount,
    bool CompleteLiberation,
    bool EndedByLethalDamage);

internal static class TechnologyFloorLiberationSettlementStore
{
    internal const int MinimumKilledBossCount = 2;

    private static TechnologyFloorLiberationSettlementData _current =
        new(MinimumKilledBossCount, false, false);

    public static TechnologyFloorLiberationSettlementData Current => _current;

    public static bool PendingSettlement { get; private set; }

    public static void Record(TechnologyFloorLiberationEncounter encounter)
    {
        int kills = Math.Max(0, encounter.KilledBossCount);
        _current = new TechnologyFloorLiberationSettlementData(
            kills,
            kills >= TechnologyFloorLiberationEncounter.MaxPhase,
            encounter.EndedByLethalDamage);
        PendingSettlement = encounter.SettlementTriggered
                            && kills >= MinimumKilledBossCount;
    }

    public static bool RequiresLegacySingleKillDefeatRecovery(
        TechnologyFloorLiberationEncounter encounter)
    {
        return encounter.SettlementTriggered
               && encounter.EndedByLethalDamage
               && encounter.KilledBossCount == MinimumKilledBossCount - 1;
    }

    public static void Consume()
    {
        PendingSettlement = false;
    }
}
