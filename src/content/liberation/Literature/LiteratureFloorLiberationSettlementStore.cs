using System;

namespace LibraryOfRuina.content.liberation.Literature;

internal readonly record struct LiteratureFloorLiberationSettlementData(
    int KilledBossCount,
    bool CompleteLiberation,
    bool EndedByLethalDamage);

internal static class LiteratureFloorLiberationSettlementStore
{
    internal const int MinimumKilledBossCount = 2;

    private static LiteratureFloorLiberationSettlementData _current =
        new(MinimumKilledBossCount, false, false);

    public static LiteratureFloorLiberationSettlementData Current => _current;

    public static bool PendingSettlement { get; private set; }

    public static void Record(LiteratureFloorLiberationEncounter encounter)
    {
        int kills = Math.Max(0, encounter.KilledBossCount);
        _current = new LiteratureFloorLiberationSettlementData(
            kills,
            kills >= LiteratureFloorLiberationEncounter.PlannedMaxPhase,
            encounter.EndedByLethalDamage);
        PendingSettlement = encounter.SettlementTriggered
                            && kills >= MinimumKilledBossCount;
    }

    public static void Consume()
    {
        PendingSettlement = false;
    }
}
