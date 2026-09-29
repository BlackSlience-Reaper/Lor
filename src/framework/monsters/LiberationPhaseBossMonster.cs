using System.Threading.Tasks;
using LibraryOfRuina.encounters;
using LibraryOfRuina.framework.encounters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace LibraryOfRuina.framework.monsters;

/// <summary>
/// 解放战里用“复活并强化”（<see cref="ReviveAndEmpowerMoveId"/>）转阶段的主阶段 Boss。抽象类不产生模型 ID。
/// <list type="bullet">
/// <item>流程：Boss 死亡后遭遇经 <see cref="LiberationPhaseTransition.ShowAsync"/> 调
/// <see cref="TriggerReviveAndEmpowerState"/>（不播动画的恢复路径调 <see cref="ForceReviveAndEmpowerState"/>），
/// 把下一步行动强制设为转阶段行动；敌方回合由原版 <c>PerformMove</c> 执行它：已死亡则回到 1 血、播放表现，
/// 最后通知本层遭遇生成下一阶段（<see cref="CompleteLiberationPhaseTransition"/>）。</item>
/// <item>子类在 <c>GenerateMoveStateMachine</c> 里调用 <see cref="CreateReviveAndEmpowerState"/>，自己接
/// <c>FollowUpState</c> 并放进状态列表。转阶段状态只是运行期引用，不是 SavedProperty，放在基类不影响 net-id 与存档。</item>
/// <item>规范模型上原版 <c>GetIntents</c> 也会生成状态机，<c>ToMutable</c> 的浅拷贝会把这个引用带到可变副本；
/// 要在 <c>DeepCloneFields</c> 里断开的子类调用 <see cref="ClearReviveAndEmpowerState"/>。</item>
/// </list>
/// </summary>
public abstract class LiberationPhaseBossMonster : LorMonsterModel, ILiberationPrimaryPhaseBoss
{
    /// <summary>状态 ID 也是本地化键 <c>moves.REVIVE_AND_EMPOWER</c> 的一部分。</summary>
    public const string ReviveAndEmpowerMoveId = LiberationPhaseBossMoves.ReviveAndEmpowerMoveId;

    public abstract int LiberationPhase { get; }

    protected MoveState? ReviveAndEmpowerState { get; private set; }

    /// <summary>触发转阶段时，有生物节点就先播受击动画。</summary>
    protected virtual bool ReviveTriggerPlaysHitAnimation => false;

    /// <summary>转阶段行动里回血之后播放的动画（等待 0.6 秒）；null 表示不播，只等待。</summary>
    protected virtual string? ReviveAndEmpowerAnimation => null;

    public async Task TriggerReviveAndEmpowerState()
    {
        if (ReviveTriggerPlaysHitAnimation)
        {
            await LiberationPhaseBossMoves.TriggerHitAnimationIfVisible(Creature);
        }

        ForceReviveAndEmpowerState();
    }

    public void ForceReviveAndEmpowerState()
    {
        LiberationPhaseBossMoves.ForceState(this, ReviveAndEmpowerState);
    }

    protected MoveState CreateReviveAndEmpowerState()
    {
        MoveState state = LiberationPhaseBossMoves.CreateState(ReviveAndEmpowerMoveId, ReviveAndEmpowerMove);
        ReviveAndEmpowerState = state;
        return state;
    }

    protected void ClearReviveAndEmpowerState()
    {
        ReviveAndEmpowerState = null;
    }

    /// <summary>
    /// 调用本层遭遇的 <c>CompletePhaseTransition(this)</c>；只认本层的遭遇类型，Boss 出现在别的遭遇里时什么都不做。
    /// 是否真的生成下一阶段由遭遇按自己的阶段与转阶段标志判断。
    /// </summary>
    protected abstract Task CompleteLiberationPhaseTransition();

    protected virtual async Task PlayReviveAndEmpowerAnimation()
    {
        if (ReviveAndEmpowerAnimation is { } animation)
        {
            await CreatureCmd.TriggerAnim(Creature, animation, 0.6f);
        }

        await Cmd.CustomScaledWait(0.3f, 0.6f);
    }

    private async Task ReviveAndEmpowerMove(IReadOnlyList<Creature> targets)
    {
        if (Creature.IsDead)
        {
            await CreatureCmd.SetCurrentHp(Creature, 1m);
        }

        await PlayReviveAndEmpowerAnimation();
        await CompleteLiberationPhaseTransition();
    }
}
