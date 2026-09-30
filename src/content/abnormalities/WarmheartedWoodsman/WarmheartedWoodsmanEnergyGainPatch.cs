using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HarmonyLib;
using LibraryOfRuina.infra.patching;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using System;

namespace LibraryOfRuina.content.abnormalities.WarmheartedWoodsman;

[HarmonyPatch(typeof(PlayerCmd), nameof(PlayerCmd.GainEnergy), typeof(decimal), typeof(Player))]
internal static class WarmheartedWoodsmanEnergyGainPatch
{
    // 正在执行 Hook.AfterEnergyReset 的玩家。按玩家记：联机时一名玩家的回合开始可能停在选择上，其他玩家
    // 这时获得的能量仍要计入。弱引用表：Task 永不完成（例如在选择中退局）时，记录随旧对局的 Player 一起回收。
    private static readonly ConditionalWeakTable<Player, StrongBox<int>> EnergyResetDepth = new();

    internal static bool IsEnergyResetHookActive(Player player) =>
        EnergyResetDepth.TryGetValue(player, out StrongBox<int>? depth) && depth.Value > 0;

    /// <summary>记一层重置；只有拿到令牌的调用才能释放，令牌可重复 Dispose。</summary>
    internal static IDisposable BeginEnergyResetHook(Player player)
    {
        EnergyResetDepth.GetOrCreateValue(player).Value++;
        return new EnergyResetToken(player);
    }

    private sealed class EnergyResetToken(Player player) : IDisposable
    {
        private bool _released;

        public void Dispose()
        {
            if (_released)
            {
                return;
            }

            _released = true;
            if (EnergyResetDepth.TryGetValue(player, out StrongBox<int>? depth) && --depth.Value <= 0)
            {
                EnergyResetDepth.Remove(player);
            }
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

        await WarmheartedWoodsman.ApplyWantAHeartFromEnergyGain(player, actualGain);
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterEnergyReset))]
[LibraryPatch(Reason = "“想要一颗心”只算回合内获得的能量，回合开始由 AfterEnergyReset 监听者发的不算；要括住整个钩子（普通与 Late 两遍），原版没有前置扩展点，伐木工作为敌方监听者也排在玩家侧监听者之后。前后缀只记录正在重置的玩家，不改参数和返回值。")]
internal static class WarmheartedWoodsmanEnergyResetHookPatch
{
    // 前缀带引用类型参数，排在前面的前缀跳过原方法时它会被 Harmony 连带跳过，后缀和 Finalizer 却照常执行；
    // 所以计数只经 __state 令牌释放，没取得令牌的调用什么也不做，不会提前清掉外层调用的计数。
    private static void Prefix(Player player, out IDisposable? __state)
    {
        __state = WarmheartedWoodsmanEnergyGainPatch.BeginEnergyResetHook(player);
    }

    private static void Postfix(IDisposable? __state, ref Task __result)
    {
        if (__state != null)
        {
            __result = ReleaseWhenComplete(__result, __state);
        }
    }

    // 原方法或之前的补丁同步抛异常时后缀不执行，由这里释放。
    private static Exception? Finalizer(Exception? __exception, IDisposable? __state)
    {
        if (__exception != null)
        {
            __state?.Dispose();
        }

        return __exception;
    }

    private static async Task ReleaseWhenComplete(Task result, IDisposable token)
    {
        try
        {
            await result;
        }
        finally
        {
            token.Dispose();
        }
    }
}
