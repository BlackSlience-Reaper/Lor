using System;
using LibraryOfRuina.encounters.HistoryFloorLiberation;

namespace LibraryOfRuina.events.HistoryFloorLiberation;
internal readonly record struct HistoryFloorLiberationSettlementData(
    int KilledBossCount,
    bool CompleteLiberation,
    bool EndedByLethalDamage);

internal static class HistoryFloorLiberationSettlementStore
{
    private static HistoryFloorLiberationSettlementData _current = new(2, false, false);

    public static HistoryFloorLiberationSettlementData Current => _current;

    public static bool PendingSettlement { get; private set; }

    public static void Record(HistoryFloorLiberationEncounter encounter)
    {
        int kills = Math.Max(0, encounter.KilledBossCount);
        _current = new HistoryFloorLiberationSettlementData(
            kills,
            kills >= HistoryFloorLiberationEncounter.MaxPhase,
            encounter.EndedByLethalDamage);
        PendingSettlement = encounter.SettlementTriggered && kills >= 2;
    }

    public static void Consume()
    {
        PendingSettlement = false;
    }
}
