using System;
using LibraryOfRuina.infra.lifecycle;

namespace LibraryOfRuina.content.liberation.Literature;

internal readonly record struct LiteratureFloorLiberationSettlementData(
    int KilledBossCount,
    bool CompleteLiberation,
    bool EndedByLethalDamage);

internal static class LiteratureFloorLiberationSettlementStore
{
    internal const int MinimumKilledBossCount = 2;

    // 局级：离开本局时复位，上一局（或另一份存档）的击杀数与待结算标志不会带进这一局的结算。
    // 读档不靠这里保存：读档后回到终局奖励界面，继续时结算跳转会从读档恢复的遭遇状态重新记录。
    private static readonly RunScoped<LiteratureFloorLiberationSettlementData> CurrentState =
        new(static () => new(MinimumKilledBossCount, false, false));

    private static readonly RunScoped<bool> PendingState = new(static () => false);

    public static LiteratureFloorLiberationSettlementData Current => CurrentState.Value;

    public static bool PendingSettlement => PendingState.Value;

    public static void Record(LiteratureFloorLiberationEncounter encounter)
    {
        int kills = Math.Max(0, encounter.KilledBossCount);
        CurrentState.Value = new LiteratureFloorLiberationSettlementData(
            kills,
            kills >= LiteratureFloorLiberationEncounter.PlannedMaxPhase,
            encounter.EndedByLethalDamage);
        PendingState.Value = encounter.SettlementTriggered
                             && kills >= MinimumKilledBossCount;
    }

    public static void Consume()
    {
        PendingState.Value = false;
    }
}
