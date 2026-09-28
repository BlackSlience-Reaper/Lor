using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HarmonyLib;
using LibraryOfRuina.infra.patching;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;

namespace LibraryOfRuina.patches.WarmheartedWoodsman;

[HarmonyPatch(typeof(PlayerCmd), nameof(PlayerCmd.GainEnergy), typeof(decimal), typeof(Player))]
internal static class WarmheartedWoodsmanEnergyGainPatch
{
    // 正在执行 Hook.AfterEnergyReset 的玩家。按玩家记：联机时一名玩家的回合开始可能停在选择上，其他玩家
    // 这时获得的能量仍要计入。弱引用表：Task 永不完成（例如在选择中退局）时，记录随旧对局的 Player 一起回收。
    private static readonly ConditionalWeakTable<Player, StrongBox<int>> EnergyResetDepth = new();

    internal static bool IsEnergyResetHookActive(Player player) =>
        EnergyResetDepth.TryGetValue(player, out StrongBox<int>? depth) && depth.Value > 0;

    internal static void BeginEnergyResetHook(Player player)
    {
        EnergyResetDepth.GetOrCreateValue(player).Value++;
    }

    internal static void EndEnergyResetHook(Player player)
    {
        if (EnergyResetDepth.TryGetValue(player, out StrongBox<int>? depth) && --depth.Value <= 0)
        {
            EnergyResetDepth.Remove(player);
        }
    }

    private static void Prefix(decimal amount, Player player, out int __state)
    {
        __state = -1;
        if (amount <= 0m || player?.PlayerCombatState == null || !CombatManager.Instance.IsInProgress)
        {
            return;
        }

        __state = player.PlayerCombatState.Energy;
    }

    private static void Postfix(Player player, int __state, ref Task __result)
    {
        if (__state < 0
            || player?.PlayerCombatState == null
            || IsEnergyResetHookActive(player)
            || !CombatManager.Instance.IsInProgress)
        {
            return;
        }

        __result = ApplyWantAHeartWhenEnergyGainCompletes(__result, player, __state);
    }

    private static async Task ApplyWantAHeartWhenEnergyGainCompletes(Task result, Player player, int previousEnergy)
    {
        await result;

        if (player.PlayerCombatState == null || !CombatManager.Instance.IsInProgress)
        {
            return;
        }

        int actualGain = player.PlayerCombatState.Energy - previousEnergy;
        if (actualGain <= 0)
        {
            return;
        }

        await monsters.WarmheartedWoodsman.WarmheartedWoodsman.ApplyWantAHeartFromEnergyGain(player, actualGain);
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterEnergyReset))]
[LibraryPatch(Reason = "“想要一颗心”只算回合内获得的能量，回合开始由 AfterEnergyReset 监听者发的不算；要括住整个钩子（普通与 Late 两遍），原版没有前置扩展点，伐木工作为敌方监听者也排在玩家侧监听者之后。前后缀只记录正在重置的玩家，不改参数和返回值。")]
internal static class WarmheartedWoodsmanEnergyResetHookPatch
{
    private static void Prefix(Player player)
    {
        WarmheartedWoodsmanEnergyGainPatch.BeginEnergyResetHook(player);
    }

    private static void Postfix(Player player, ref Task __result)
    {
        __result = EndEnergyResetHookWhenComplete(__result, player);
    }

    private static async Task EndEnergyResetHookWhenComplete(Task result, Player player)
    {
        try
        {
            await result;
        }
        finally
        {
            WarmheartedWoodsmanEnergyGainPatch.EndEnergyResetHook(player);
        }
    }
}
