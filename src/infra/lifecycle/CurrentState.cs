using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.infra.lifecycle;

/// <summary>
/// 当前本地局的唯一取法。原版只把私有的 <c>RunManager.State</c> 通过标注“仅测试用”的
/// <see cref="RunManager.DebugOnlyGetState"/> 暴露出来；本模组的原版补丁（<c>__instance</c> 是管理器、界面节点、
/// 章节或遭遇的规范模型）、BGM 控制器、控制台命令等入口手里没有 <c>Player.RunState</c> 之类的上下文，只能从这里取。
/// 手里已有玩家、生物、房间或钩子参数时，优先用它们带的局，不要来这里取。
/// <para>
/// 联机：一个进程里只有一个本地局，两端各自取到的是自己那一份局，内容由原版同步保证一致，不会读到对方的状态。
/// 按局对象做身份比对（<c>ReferenceEquals(CurrentRun.State, captured)</c>）用来判断异步回调时局是否已经换掉，
/// 这种检查必须取全局，不能换成上下文里的局。
/// </para>
/// <para>
/// 返回 null：主菜单、开局在 <c>SetUpNew*</c>、读档 <c>SetUpSaved*</c> 赋值之前，以及
/// <c>RunManager.CleanUp</c> 的 finally 把 <c>State</c> 置空之后。调用方都要处理 null。
/// </para>
/// </summary>
internal static class CurrentRun
{
    internal static RunState? State => RunManager.Instance.DebugOnlyGetState();

    /// <summary>原版补丁拿到的 <c>RunManager __instance</c>；原版只有一个静态单例，与 <see cref="State"/> 等价。</summary>
    internal static RunState? Of(RunManager manager) => manager.DebugOnlyGetState();
}

/// <summary>
/// 当前本地战斗的唯一取法，理由与联机约定同 <see cref="CurrentRun"/>。原版 <see cref="CombatManager.DebugOnlyGetState"/>
/// 返回 <c>_turnState?.State</c>：<c>SetUpCombat</c> 之前与 <c>Reset</c>（<c>CombatRoom.Exit</c>、
/// <c>RunManager.CleanUp</c>）清空之后为 null，战斗胜利后到离开房间前仍是刚结束的那场。手里有 <c>Creature.CombatState</c>、<c>CombatRoom.CombatState</c> 时优先用它们。
/// </summary>
internal static class CurrentCombat
{
    internal static CombatState? State => CombatManager.Instance.DebugOnlyGetState();

    /// <summary>原版补丁拿到的 <c>CombatManager __instance</c>；原版只有一个静态单例，与 <see cref="State"/> 等价。</summary>
    internal static CombatState? Of(CombatManager manager) => manager.DebugOnlyGetState();
}
