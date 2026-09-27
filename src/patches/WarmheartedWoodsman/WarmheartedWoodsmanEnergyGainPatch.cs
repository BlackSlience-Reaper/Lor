using System;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;

namespace LibraryOfRuina.patches.WarmheartedWoodsman;

[HarmonyPatch(typeof(PlayerCmd), nameof(PlayerCmd.GainEnergy), typeof(decimal), typeof(Player))]
internal static class WarmheartedWoodsmanEnergyGainPatch
{
    private static int _energyResetDepth;

    internal static bool IsEnergyResetHookActive => _energyResetDepth > 0;

    internal static void BeginEnergyResetHook()
    {
        _energyResetDepth++;
    }

    internal static void EndEnergyResetHook()
    {
        _energyResetDepth = Math.Max(0, _energyResetDepth - 1);
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
            || IsEnergyResetHookActive
            || player?.PlayerCombatState == null
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
internal static class WarmheartedWoodsmanEnergyResetHookPatch
{
    private static void Prefix()
    {
        WarmheartedWoodsmanEnergyGainPatch.BeginEnergyResetHook();
    }

    private static void Postfix(ref Task __result)
    {
        __result = EndEnergyResetHookWhenComplete(__result);
    }

    private static async Task EndEnergyResetHookWhenComplete(Task result)
    {
        try
        {
            await result;
        }
        finally
        {
            WarmheartedWoodsmanEnergyGainPatch.EndEnergyResetHook();
        }
    }
}
