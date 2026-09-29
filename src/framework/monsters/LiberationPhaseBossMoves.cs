using System;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace LibraryOfRuina.framework.monsters;

/// <summary>
/// 解放战阶段 Boss 转阶段行动的共用片段。<see cref="LiberationPhaseBossMonster"/> 用它们实现整套流程；
/// 直接基类不是 <see cref="LorMonsterModel"/> 的阶段 Boss（钴蓝伤痕继承 <c>CounterIntentMonsterModel</c>，
/// 历史层占位 Boss 继承原版 <c>MonsterModel</c>）和转阶段行动不是“复活并强化”的拟态直接调用。
/// </summary>
internal static class LiberationPhaseBossMoves
{
    public const string ReviveAndEmpowerMoveId = "REVIVE_AND_EMPOWER";

    /// <summary>
    /// 转阶段行动：治疗 + 强化意图，必须执行一次才能离开。<see cref="LibraryPhaseTransitionMoveState"/> 让它越过混乱锁、
    /// 不被普通击晕覆盖，基础库按这个类型识别转阶段行动。
    /// </summary>
    public static LibraryPhaseTransitionMoveState CreateState(
        string moveId,
        Func<IReadOnlyList<Creature>, Task> move)
    {
        return new LibraryPhaseTransitionMoveState(
            moveId,
            move,
            new HealIntent(),
            new BuffIntent())
        {
            MustPerformOnceBeforeTransitioning = true
        };
    }

    /// <summary>状态机生成前 <paramref name="state"/> 为 null，此时什么都不做。</summary>
    public static void ForceState(MonsterModel monster, MoveState? state)
    {
        if (state != null)
        {
            monster.SetMoveImmediate(state, forceTransition: true);
        }
    }

    /// <summary>
    /// 有生物节点时播受击动画。等待时间为 0，原版 <c>TriggerAnim</c> 同步完成；
    /// 没有节点时不调用它，避免战斗进行中报“节点不存在”的错误日志。
    /// </summary>
    public static async Task TriggerHitAnimationIfVisible(Creature creature)
    {
        if (NCombatRoom.Instance?.GetCreatureNode(creature) != null)
        {
            await CreatureCmd.TriggerAnim(creature, "Hit", 0f);
        }
    }
}
