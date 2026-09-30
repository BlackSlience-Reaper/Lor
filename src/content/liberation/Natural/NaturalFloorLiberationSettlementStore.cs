using LibraryOfRuina.infra.lifecycle;

namespace LibraryOfRuina.content.liberation.Natural;

internal static class NaturalFloorLiberationSettlementStore
{
    public const int MinimumKilledBossCount = 1;

    // 局级：离开本局时复位，上一局（或另一份存档）的值不会带进这一局的结算。
    // 结算事件房间的存读档由 NaturalFloorSpecialRewardSavePatch / LoadPatch 写入并恢复这里的击杀数与虚无完成标志。
    private static readonly RunScoped<int> KilledBossCountState = new(static () => 0);

    private static readonly RunScoped<bool> PendingState = new(static () => false);

    private static readonly RunScoped<bool> NihilCompletedState = new(static () => false);

    public static int KilledBossCount => KilledBossCountState.Value;

    public static bool PendingSettlement => PendingState.Value;

    public static bool NihilCompleted => NihilCompletedState.Value;

    public static void Record(NaturalFloorLiberationEncounter encounter)
    {
        KilledBossCountState.Value = encounter.KilledBossCount;
        NihilCompletedState.Value = encounter.NihilCompleted;
        PendingState.Value = encounter.SettlementTriggered && KilledBossCount >= MinimumKilledBossCount;
    }

    public static void Consume() => PendingState.Value = false;

    internal static void Restore(int kills, bool nihilCompleted)
    {
        KilledBossCountState.Value = System.Math.Clamp(kills, 1, NaturalFloorLiberationEncounter.PlannedMaxPhase);
        NihilCompletedState.Value = nihilCompleted;
        PendingState.Value = false;
    }
}
