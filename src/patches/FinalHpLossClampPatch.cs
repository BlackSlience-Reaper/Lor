using System;
using System.Linq;
using HarmonyLib;
using LibraryLib.Hooks;
using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.infra.patching;
using LibraryOfRuina.powers.PhilosophyFloorLiberation;
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

// 锁血是最终裁决：若改成各实现者覆写 ModifyHpLostAfterOstyLate，结果取决于监听者顺序，以玩家为目标时锁血能力会先于
// Late 遗物执行，遗物看到的是钳制后的数值。同目标上没有其他 Last 后缀，暂不需要 HarmonyAfter。
[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyHpLost))]
[LibraryPatch(Reason = "原版没有“所有监听者与模组都修正完之后”的扩展点；只对实现 IFinalHpLossClamp 的本模组模型在 AfterOsty 阶段钳制，并把它们补进 modifiers。")]
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
            // 锁血自己遍历监听者，所以暮光审判的“剔除能力”作用域在这里自己建立：基础库解析前缀跳过原方法时，
            // PhilosophyFloorTwilightJudgmentModifyHpLostPatch 的前缀会被连带跳过，但这个后缀照常执行。
            // 前缀已经建立时这里只是嵌套一层。
            using IDisposable? judgmentScope = PhilosophyFloorTwilightJudgmentPowerBypassContext.EnterPowerModifierHook(dealer);
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
