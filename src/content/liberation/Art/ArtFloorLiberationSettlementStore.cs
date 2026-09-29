using System;

namespace LibraryOfRuina.content.liberation.Art;

internal readonly record struct ArtFloorLiberationSettlementData(
    int KilledBossCount,
    bool CompleteLiberation,
    bool EndedByLethalDamage);

internal static class ArtFloorLiberationSettlementStore
{
    private static ArtFloorLiberationSettlementData _current = new(2, false, false);

    public static ArtFloorLiberationSettlementData Current => _current;

    public static bool PendingSettlement { get; private set; }

    public static void Record(ArtFloorLiberationEncounter encounter)
    {
        int kills = Math.Max(0, encounter.KilledBossCount);
        _current = new ArtFloorLiberationSettlementData(
            kills,
            kills >= ArtFloorLiberationEncounter.MaxPhase,
            encounter.EndedByLethalDamage);
        PendingSettlement = encounter.SettlementTriggered && kills >= 2;
    }

    public static void Consume()
    {
        PendingSettlement = false;
    }
}
