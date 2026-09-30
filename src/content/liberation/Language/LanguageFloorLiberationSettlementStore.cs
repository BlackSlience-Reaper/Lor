using System;

namespace LibraryOfRuina.content.liberation.Language;

internal readonly record struct LanguageFloorLiberationSettlementData(
    int KilledBossCount,
    bool CompleteLiberation,
    bool EndedByLethalDamage);

internal static class LanguageFloorLiberationSettlementStore
{
    private static LanguageFloorLiberationSettlementData _current =
        new(2, false, false);

    public static LanguageFloorLiberationSettlementData Current => _current;

    public static bool PendingSettlement { get; private set; }

    public static void Record(LanguageFloorLiberationEncounter encounter)
    {
        int kills = Math.Max(0, encounter.KilledBossCount);
        _current = new LanguageFloorLiberationSettlementData(
            kills,
            kills >= LanguageFloorLiberationEncounter.MaxPhase,
            encounter.EndedByLethalDamage);
        PendingSettlement = encounter.SettlementTriggered && kills >= 2;
    }

    public static void Consume()
    {
        PendingSettlement = false;
    }
}
