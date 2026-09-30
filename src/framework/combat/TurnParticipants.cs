using System.Linq;
using MegaCrit.Sts2.Core.Combat;

namespace LibraryOfRuina.framework.combat;

/// <summary>
/// 回合钩子（<c>Before/AfterSideTurnStart</c>、<c>Before/AfterSideTurnEnd</c> 及其 Early/Late 变体）里判断“这次回合算不算”。
/// <para>原版契约（0.111.0 <c>CombatManager.StartTurn</c>、<c>EndPlayerTurnPhaseOne/TwoInternal</c>）：</para>
/// <list type="bullet">
/// <item>正常玩家回合：开始时 participants 是玩家一侧的全部生物（含宠物），结束时只有各玩家本体。</item>
/// <item>额外回合（原版 <c>PaelsEye</c>、<c>AmbergrisPower</c>，本模组大鸟书页，都经 <c>Hook.ShouldTakeExtraTurn</c> 给出）：
/// 紧跟在正常回合之后，开始与结束都只带取得额外回合的玩家本体；回合号不变，敌人不重新准备意图。</item>
/// <item>敌方回合：participants 是敌方全部生物，没有额外回合。</item>
/// </list>
/// participants 与 <c>PlayersTakingExtraTurn</c> 都由原版在两端按同一流程算出，据此过滤不会让联机两端分叉。
/// <para>
/// 放在静态类里而不是基类：用到它的有书页遗物、普通遗物、附魔、原版 <c>PowerModel</c> 与基础库能力的子类、怪物，
/// 分属五条以上的继承链，其中两条的根在原版和基础库里。原版自己的写法是 <c>participants.Contains(Owner.Creature)</c>
/// （玩家持有的遗物、能力）与 <c>PlayersTakingExtraTurn.Count &gt; 0</c> 时跳过（<c>RampartPower</c>，敌方按轮的效果）。
/// </para>
/// </summary>
internal static class TurnParticipants
{
    /// <summary>
    /// 当前是否是额外玩家回合。只在玩家一侧的回合钩子里有意义：正常回合的开始与结束钩子里为假，
    /// 额外回合的开始与结束钩子里为真。
    /// </summary>
    public static bool IsExtraPlayerTurn => CombatManager.Instance.PlayersTakingExtraTurn.Count > 0;

    /// <summary>
    /// 绑定持有者的“你的回合”效果：<paramref name="creature"/> 所在一侧正在进行回合，而且在玩家一侧时它的玩家在本次参与者里。
    /// 宠物跟随主人；玩家一侧没有主人的非玩家生物只在正常回合里成立。敌方一侧只看阵营，与不看 participants 时相同。
    /// </summary>
    public static bool IsOwnTurn(Creature? creature, CombatSide side, IEnumerable<Creature> participants)
    {
        if (creature == null || creature.Side != side)
        {
            return false;
        }

        if (side != CombatSide.Player)
        {
            return true;
        }

        Creature anchor = creature.PetOwner?.Creature ?? creature;
        return anchor.IsPlayer
            ? participants.Contains(anchor)
            : !IsExtraPlayerTurn;
    }

    /// <summary>
    /// 玩家一侧回合钩子里、挂在 <paramref name="creature"/> 身上的“每个玩家回合”效果是否生效。
    /// 挂在玩家一侧时按 <see cref="IsOwnTurn"/>：队友的额外回合不算持有者的回合。
    /// 挂在敌方时按轮：额外回合不算（见 <see cref="IsRoundPlayerTurn"/>）。
    /// </summary>
    public static bool IsPlayerTurnFor(Creature? creature, CombatSide side, IEnumerable<Creature> participants)
    {
        if (creature == null || side != CombatSide.Player)
        {
            return false;
        }

        return creature.Side == CombatSide.Player
            ? IsOwnTurn(creature, side, participants)
            : !IsExtraPlayerTurn;
    }

    /// <summary>
    /// 敌方、遭遇或全局修改器以“轮”为单位、挂在玩家一侧回合钩子上的逻辑（意图规划、倒计时、计数、随机数、每轮增益）：
    /// 只在正常玩家回合成立。额外回合里敌人不行动也不重新准备意图，这些逻辑跟着跑一次会多推进一轮、多消耗随机数。
    /// </summary>
    public static bool IsRoundPlayerTurn(CombatSide side) =>
        side == CombatSide.Player && !IsExtraPlayerTurn;
}
