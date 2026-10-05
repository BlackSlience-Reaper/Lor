using HarmonyLib;
using LibraryLib.Models;
using LibraryOfRuina.content.abnormalities.Ozma;
using LibraryOfRuina.content.liberation.Philosophy;
using LibraryOfRuina.framework.combat;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.patches.dispatch;

/// <summary>
/// <c>CombatState.IterateHookListeners</c> 上本模组默认优先级的过滤，依次执行原来三个后缀的逻辑：
/// 暮光“大眼”暂停增益、奥兹玛“失忆”暂停增益、友方盟友屏蔽侵蚀破坏能力。
/// <para>
/// 战斗里没有这些本模组状态时直接返回，不碰原版结果。原版迭代器在逐个产出时才做 <c>Contains</c> 存活检查，
/// Hook 遍历中途 await 后脱离战斗的监听者（例如缠绕移除时清掉的纠缠苦难，其 Card 已为 null）会被跳过；
/// 若在这里 <c>ToArray</c> 拍快照，检查就提前到调用时，脱离的监听者仍被产出，
/// <c>HookPlayerChoiceContext.GetOwner</c> 空引用，回合循环终止、战斗卡死。
/// </para>
/// <para>
/// 需要过滤时同样保持惰性：审判作用域和“大眼”是否激活在调用时求值（与原来一致），逐个能力的条件在产出时求值，
/// 与原版存活检查的时点相同。
/// </para>
/// <c>DetachedAfflictionHookListenerPatch</c> 在 <c>Priority.Last</c>，不在这里合并，以免改变它与其他模组后缀的相对顺序。
/// </summary>
[HarmonyPatch(typeof(CombatState), nameof(CombatState.IterateHookListeners))]
internal static class HookListenerFilterPatch
{
    /// <summary>其他模组的侵蚀破坏能力 ID；在友方盟友身上时屏蔽。</summary>
    private const string ErosionDestructionPowerId = "CENSORED_EGO_POWER_EROSION_DESTRUCTION_POWER";

    [HarmonyPostfix]
    private static void Postfix(CombatState __instance, ref IEnumerable<AbstractModel> __result)
    {
        bool bypassPowers = PhilosophyFloorTwilightJudgmentPowerBypassContext.ShouldBypassPowerModifiers;
        bool bigEyesActive = false;
        bool hasFilteredState = bypassPowers;
        foreach (Creature creature in __instance.Creatures)
        {
            if (creature.Monster is PhilosophyFloorTwilight boss
                && creature.IsAlive
                && boss.IsEggActive(PhilosophyFloorTwilightEgg.BigEyes))
            {
                bigEyesActive = true;
                hasFilteredState = true;
            }

            if (creature.GetPower<OzmaLostMemoryPower>() != null
                || HasErosionDestructionOnFriendlyAlly(creature))
            {
                hasFilteredState = true;
            }
        }

        if (!hasFilteredState)
        {
            return;
        }

        __result = FilterListeners(__result, bypassPowers, bigEyesActive);
    }

    private static IEnumerable<AbstractModel> FilterListeners(
        IEnumerable<AbstractModel> listeners,
        bool bypassPowers,
        bool bigEyesActive)
    {
        foreach (AbstractModel model in listeners)
        {
            PowerModel? power = model is LibraryPowerModeModel mode ? mode.SourcePower : model as PowerModel;
            if (power == null || ShouldKeepPower(power, bypassPowers, bigEyesActive))
            {
                yield return model;
            }
        }
    }

    private static bool ShouldKeepPower(PowerModel power, bool bypassPowers, bool bigEyesActive)
    {
        if (bypassPowers)
        {
            return false;
        }

        if (bigEyesActive && PhilosophyFloorTwilightBigEyesHookSuspension.ShouldSuspendPower(power))
        {
            return false;
        }

        if (power.TypeForCurrentAmount == PowerType.Buff
            && power.Owner.GetPower<OzmaLostMemoryPower>() != null)
        {
            return false;
        }

        return power.Id.Entry != ErosionDestructionPowerId
               || !AllyTurnRegistry.IsFriendlyAlly(power.Owner);
    }

    private static bool HasErosionDestructionOnFriendlyAlly(Creature creature)
    {
        foreach (PowerModel power in creature.Powers)
        {
            if (power.Id.Entry == ErosionDestructionPowerId)
            {
                return AllyTurnRegistry.IsFriendlyAlly(creature);
            }
        }

        return false;
    }
}
