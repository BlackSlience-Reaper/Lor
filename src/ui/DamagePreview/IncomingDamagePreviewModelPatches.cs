using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using LibraryOfRuina.content.abnormalities.QueenOfHatred;
using LibraryOfRuina.content.liberation.History;
using LibraryOfRuina.infra.patching;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.ui.DamagePreview;

/// <summary>
/// 受伤预览按命中顺序推进有次数或按回合累计上限的受伤修正。补丁只在
/// <see cref="IncomingDamageSimulation.Current"/> 存在的同步预览期间改用模拟进度，实际结算原样执行。
/// </summary>
internal static class IncomingDamagePreviewModelPatches
{
    /// <summary>
    /// 有触发次数的修正在模拟中剩余的次数；返回 null 表示不限次数。
    /// 缓冲每层抵消一次；火柴印记的足迹模式只触发一次；憎恶之页按本回合剩余次数。
    /// </summary>
    private static int? GetTriggerBudget(AbstractModel model) => model switch
    {
        BufferPower buffer => buffer.Amount,
        MatchMarkRelic { Mode: MatchMarkMode.Footsteps } => 1,
        QueenOfHatredPageRelic { Mode: QueenOfHatredPageMode.Hatred } queen => queen.HatredTriggersRemainingThisTurn,
        _ => null
    };

    private static bool IsExhausted(AbstractModel model)
    {
        IncomingDamageSimulation? simulation = IncomingDamageSimulation.Current;
        return simulation != null
            && GetTriggerBudget(model) is { } budget
            && simulation.GetTriggerCount(model) >= budget;
    }

    [HarmonyPatch]
    [LibraryPatch(Reason = "缓冲等按次触发的效果无预览标志；仅在本模组受伤预览的同步模拟作用域内按模拟次数截断，正式结算原样执行。")]
    private static class LimitedTriggerPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.DeclaredMethod(typeof(BufferPower), nameof(BufferPower.ModifyHpLostAfterOstyLate));
            yield return AccessTools.DeclaredMethod(typeof(MatchMarkRelic), nameof(MatchMarkRelic.ModifyHpLostAfterOstyLate));
            yield return AccessTools.DeclaredMethod(typeof(QueenOfHatredPageRelic), nameof(QueenOfHatredPageRelic.ModifyHpLostAfterOstyLate));
        }

        // 次数已在模拟中用尽的修正不再改变数值。
        private static bool Prefix(AbstractModel __instance, decimal amount, ref decimal __result)
        {
            if (!IsExhausted(__instance))
            {
                return true;
            }

            __result = amount;
            return false;
        }
    }

    /// <summary>坚硬外壳按一方回合累计失去生命值；敌方回合开始时清零，玩家回合结束阶段沿用当前累计。</summary>
    [HarmonyPatch(typeof(HardenedShellPower), nameof(HardenedShellPower.ModifyHpLostBeforeOstyLate))]
    [LibraryPatch(Reason = "坚硬外壳按实际累计失血计算上限且无预览参数；仅在本模组受伤预览的同步模拟作用域内改用模拟累计，正式结算原样执行。")]
    private static class HardenedShellPatch
    {
        private static bool Prefix(
            HardenedShellPower __instance,
            Creature target,
            decimal amount,
            ref decimal __result)
        {
            IncomingDamageSimulation? simulation = IncomingDamageSimulation.Current;
            if (simulation == null
                || target != __instance.Owner
                || amount == 0m
                || !simulation.TryGetState(target, out IncomingDamageTargetState? state))
            {
                return true;
            }

            int capAtPhaseStart = simulation.IsEnemyTurn ? __instance.Amount : __instance.DisplayAmount;
            __result = Math.Min(amount, Math.Max(0, capAtPhaseStart - state!.HpLostThisSide));
            return false;
        }
    }

    /// <summary>跳动残骸在拥有者回合开始时清零，敌方回合继续累计玩家回合内已失去的生命值。</summary>
    [HarmonyPatch(typeof(BeatingRemnant), nameof(BeatingRemnant.ModifyHpLostAfterOsty))]
    [LibraryPatch(Reason = "跳动残骸按实际本回合失血计算上限且无预览参数；仅在本模组受伤预览的同步模拟作用域内叠加模拟失血，正式结算原样执行。")]
    private static class BeatingRemnantPatch
    {
        private static bool Prefix(
            BeatingRemnant __instance,
            Creature target,
            decimal amount,
            ref decimal __result)
        {
            IncomingDamageSimulation? simulation = IncomingDamageSimulation.Current;
            if (simulation == null
                || target != __instance.Owner.Creature
                || !simulation.TryGetState(target, out IncomingDamageTargetState? state))
            {
                return true;
            }

            decimal received = VanillaPrivate.BeatingRemnantDamageReceivedThisTurn.Get(__instance) + state!.HpLostSinceOwnerTurnStart;
            __result = Math.Min(amount, __instance.DynamicVars["MaxHpLoss"].BaseValue - received);
            return false;
        }
    }
}
