using System;
using LibraryOfRuina.infra.lifecycle;

namespace LibraryOfRuina.content.liberation.Technology;

internal readonly record struct TechnologyFloorLiberationSettlementData(
    int KilledBossCount,
    bool CompleteLiberation,
    bool EndedByLethalDamage);

internal static class TechnologyFloorLiberationSettlementStore
{
    internal const int MinimumKilledBossCount = 2;

    // 局级：离开本局时复位，上一局（或另一份存档）的击杀数与待结算标志不会带进这一局的结算。
    // 读档不靠这里保存：读档后回到终局奖励界面，继续时结算跳转会从读档恢复的遭遇状态重新记录。
    private static readonly RunScoped<TechnologyFloorLiberationSettlementData> CurrentState =
        new(static () => new(MinimumKilledBossCount, false, false));

    private static readonly RunScoped<bool> PendingState = new(static () => false);

    public static TechnologyFloorLiberationSettlementData Current => CurrentState.Value;

    public static bool PendingSettlement => PendingState.Value;

    public static void Record(TechnologyFloorLiberationEncounter encounter)
    {
        int kills = Math.Max(0, encounter.KilledBossCount);
        CurrentState.Value = new TechnologyFloorLiberationSettlementData(
            kills,
            kills >= TechnologyFloorLiberationEncounter.MaxPhase,
            encounter.EndedByLethalDamage);
        PendingState.Value = encounter.SettlementTriggered
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
        PendingState.Value = false;
    }
}
