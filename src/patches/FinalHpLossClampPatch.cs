using System;
using System.Linq;
using HarmonyLib;
using LibraryLib.Hooks;
using LibraryOfRuina.helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.patches;

internal static class FinalHpLossClamp
{
    internal static decimal Apply(
        IRunState runState,
        ICombatState? combatState,
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        ref IEnumerable<AbstractModel> modifiers)
    {
        List<AbstractModel>? finalModifiers = null;
        foreach (AbstractModel listener in runState.IterateHookListeners(combatState))
        {
            if (listener is not IFinalHpLossClamp clamp)
            {
                continue;
            }

            decimal previous = amount;
            amount = clamp.ClampFinalHpLoss(target, amount, props, dealer, cardSource);
            if (decimal.Truncate(previous) != decimal.Truncate(amount))
            {
                // 继续交给原版的实际结算回调处理闪光、次数等状态，预览仅计算。
                finalModifiers ??= modifiers.ToList();
                if (!finalModifiers.Contains(listener))
                {
                    finalModifiers.Add(listener);
                }
            }
        }

        if (finalModifiers != null)
        {
            modifiers = finalModifiers;
        }

        return amount;
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyHpLost))]
internal static class VanillaFinalHpLossClampPatch
{
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(
        IRunState runState,
        ICombatState? combatState,
        Creature target,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        HpLossHookPhase phases,
        ref IEnumerable<AbstractModel> modifiers,
        ref decimal __result)
    {
        // BeforeOsty 尚有伤害转移与后续减伤；All 预览和 AfterOsty 实战共用末尾锁血。
        if (phases.HasFlag(HpLossHookPhase.AfterOsty))
        {
            __result = FinalHpLossClamp.Apply(
                runState, combatState, target, __result, props, dealer, cardSource, ref modifiers);
        }
    }
}

[HarmonyPatch(typeof(LibraryHooks), nameof(LibraryHooks.ModifyHpLostAfterOsty))]
internal static class LibraryFinalHpLossClampPatch
{
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(
        IRunState runState,
        ICombatState combatState,
        Creature target,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        ref IEnumerable<AbstractModel> modifiers,
        ref decimal __result)
    {
        __result = FinalHpLossClamp.Apply(
            runState, combatState, target, __result, props, dealer, cardSource, ref modifiers);
    }
}
