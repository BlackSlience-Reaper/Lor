using System;
using LibraryOfRuina.infra.lifecycle;

namespace LibraryOfRuina.content.liberation.Art;

internal readonly record struct ArtFloorLiberationSettlementData(
    int KilledBossCount,
    bool CompleteLiberation,
    bool EndedByLethalDamage);

internal static class ArtFloorLiberationSettlementStore
{
    // 局级：离开本局时复位，上一局（或另一份存档）的击杀数与待结算标志不会带进这一局的结算。
    // 读档不靠这里保存：读档后回到终局奖励界面，继续时结算跳转会从读档恢复的遭遇状态重新记录。
    private static readonly RunScoped<ArtFloorLiberationSettlementData> CurrentState =
        new(static () => new(2, false, false));

    private static readonly RunScoped<bool> PendingState = new(static () => false);

    public static ArtFloorLiberationSettlementData Current => CurrentState.Value;

    public static bool PendingSettlement => PendingState.Value;

    public static void Record(ArtFloorLiberationEncounter encounter)
    {
        int kills = Math.Max(0, encounter.KilledBossCount);
        CurrentState.Value = new ArtFloorLiberationSettlementData(
            kills,
            kills >= ArtFloorLiberationEncounter.MaxPhase,
            encounter.EndedByLethalDamage);
        PendingState.Value = encounter.SettlementTriggered && kills >= 2;
    }

    public static void Consume()
    {
        PendingState.Value = false;
    }
}
