using System;
using System.Linq;
using HarmonyLib;
using LibraryOfRuina.combat;
using LibraryOfRuina.monsters.PhilosophyFloorLiberation;
using LibraryOfRuina.powers.Ozma;
using LibraryOfRuina.powers.PhilosophyFloorLiberation;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace LibraryOfRuina.patches.dispatch;

/// <summary>
/// <c>CombatState.IterateHookListeners</c> 上本模组默认优先级的过滤，依次执行原来三个后缀的逻辑：
/// 暮光“大眼”暂停增益、奥兹玛“失忆”暂停增益、友方盟友屏蔽侵蚀破坏能力。
/// <para>
/// 原来暮光与奥兹玛两个后缀每次都 <c>ToArray</c>，在调用时就对监听者和过滤条件拍了快照。这里保留快照：
/// 入口一律物化成本方法自己的数组（上游补丁可能返回它仍持有、之后会增删的 List）；两个过滤条件在调用时
/// 立即求值，只在确实要剔除时才另分配数组。友方盟友的过滤原来就是惰性的，保持惰性。
/// </para>
/// <c>DetachedAfflictionHookListenerPatch</c> 在 <c>Priority.Last</c>，不在这里合并，以免改变它与其他模组后缀的相对顺序。
/// </summary>
[HarmonyPatch(typeof(CombatState), nameof(CombatState.IterateHookListeners))]
internal static class HookListenerFilterPatch
{
    [HarmonyPostfix]
    private static void Postfix(ref IEnumerable<AbstractModel> __result)
    {
        IReadOnlyCollection<AbstractModel> models = __result.ToArray();

        if (PhilosophyFloorTwilightJudgmentPowerBypassContext.ShouldBypassPowerModifiers)
        {
            models = KeepWhere(models, static model => model is not PowerModel);
        }

        bool bigEyesActive = models
            .OfType<PhilosophyFloorTwilight>()
            .Any(static boss => boss.Creature.IsAlive && boss.IsEggActive(PhilosophyFloorTwilightEgg.BigEyes));
        if (bigEyesActive)
        {
            models = KeepWhere(models, static model =>
                model is not PowerModel power || !PhilosophyFloorTwilightBigEyesHookSuspension.ShouldSuspendPower(power));
        }

        models = KeepWhere(models, static model =>
            model is not PowerModel power
            || power.TypeForCurrentAmount != PowerType.Buff
            || power.Owner.GetPower<OzmaLostMemoryPower>() == null);

        __result = models.Where(static model =>
            model is not PowerModel power
            || power.Id.Entry != "CENSORED_EGO_POWER_EROSION_DESTRUCTION_POWER"
            || !AllyTurnRegistry.IsFriendlyAlly(power.Owner));
    }

    /// <summary>立即求值的过滤；没有元素被剔除时返回原集合，不分配。</summary>
    private static IReadOnlyCollection<AbstractModel> KeepWhere(
        IReadOnlyCollection<AbstractModel> models,
        Func<AbstractModel, bool> keep)
    {
        foreach (AbstractModel model in models)
        {
            if (!keep(model))
            {
                return models.Where(keep).ToArray();
            }
        }

        return models;
    }
}
