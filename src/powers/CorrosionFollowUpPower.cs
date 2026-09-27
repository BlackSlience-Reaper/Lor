using System;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.powers;

/// <summary>
/// 腐蚀类能力：所属方回合结束时受到等同层数的伤害并减少层数；
/// 被强化攻击命中（含被格挡的部分）后追加受到等同层数的伤害。
/// 实际结算、我方受伤预览与敌人受伤预览共用本契约与 <see cref="CorrosionFollowUpRules"/>。
/// </summary>
internal interface ICorrosionFollowUpPower
{
    /// <summary>能力所属生物。</summary>
    Creature Owner { get; }

    /// <summary>当前层数，同时是回合结束伤害与命中追加伤害的数值。</summary>
    int Amount { get; }

    /// <summary>所属方回合结束伤害的来源。</summary>
    Creature? GetTurnEndDamageDealer();

    /// <summary>命中追加伤害的来源；<paramref name="attacker"/> 为触发本次追加的攻击者。</summary>
    Creature? GetHitFollowUpDealer(Creature? attacker);

    /// <summary>腐蚀伤害结算（含预览计算）期间保持的来源上下文。</summary>
    IDisposable EnterDamageSourceScope();
}

internal static class CorrosionFollowUpRules
{
    // 腐蚀：回合结束伤害与命中追加伤害均为无强化伤害，可被格挡。
    internal const ValueProp DamageProps = ValueProp.Unpowered;

    // 腐蚀：所属方回合结束结算伤害后减少的层数，实际结算为一次 PowerCmd.Decrement。
    internal const int TurnEndStackLoss = 1;

    /// <summary>
    /// 本次命中是否追加腐蚀伤害：需要仍有层数、命中造成的格挡与生命伤害合计大于 0，且为强化攻击。
    /// </summary>
    internal static bool TriggersOnHit(int stacks, int totalDamage, ValueProp props)
    {
        return stacks > 0
            && totalDamage > 0
            && ValuePropCompat.IsPoweredAttack(props);
    }

    /// <summary>所属方回合结束结算后剩余的层数。</summary>
    internal static int StacksAfterTurnEnd(int stacks)
    {
        return Math.Max(0, stacks - TurnEndStackLoss);
    }

    internal static IDisposable NoSourceScope { get; } = new EmptyScope();

    private sealed class EmptyScope : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
