using System.Threading.Tasks;
using LibraryOfRuina.core.settings;
using LibraryOfRuina.framework.encounters;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace LibraryOfRuina.infra.hooks;

/// <summary>
/// 本模组经 <c>ModHelper.SubscribeForRunStateHooks</c> 订阅的局级监听者，承接没有内容模型可以覆写、
/// 又不需要“最后说了算”的钩子。需要排在全部监听者与其他模组之后的钩子仍用 <c>Priority.Last</c> 后缀，
/// 逐条理由见 <c>snapshots/hook_patches.txt</c>。
/// <para>
/// 用的是 ModelDb 里的规范实例：不进 <c>RunState.Modifiers</c>，不存档、不显示，各局共用，覆写里不能写实例状态。
/// 继承 <see cref="ModifierModel"/> 只是借一个原版不会枚举的模型类别（自定义模式的修饰列表是原版写死的），
/// 类名就是模型 ID，不能改。
/// </para>
/// <para>
/// 在原版 <c>RunState.IterateHookListeners</c> 里的位置：战斗内排在牌组的牌与附魔之后、全部战斗监听者（能力、怪物、
/// 遗物、药水、充能球、战斗牌堆、战斗 Modifiers 快照、Badge、多人缩放模型）与战斗订阅者之前；战斗外排在 Modifiers、Badge、
/// 多人缩放模型之后。各模组的订阅按订阅 id 的序数排序，id 序靠前的其他模组订阅者排在它前面。
/// 只走 <c>CombatState</c> 的钩子（回合、能量、数值修正等）不经过局级订阅者，这里收不到。
/// </para>
/// <para>
/// 订阅委托在每次迭代监听者时调用，是否参与由本局设置（<see cref="LibraryRunSettingsModifier"/>）决定。
/// </para>
/// </summary>
public sealed class LibraryOfRuinaHookListener : ModifierModel
{
    private const string SubscriptionId = "LibraryOfRuina";

    private static AbstractModel[]? _listeners;

    internal static void Subscribe() =>
        ModHelper.SubscribeForRunStateHooks(SubscriptionId, static runState => ActiveListeners(runState));

    private static IEnumerable<AbstractModel> ActiveListeners(RunState runState)
    {
        // 关闭“启用废墟图书馆内容”的局里本模组不出现解放战等内容，整个监听者不参与。按本局设置判断，
        // 联机两端读到的是房主的同一个值。
        if (!LibraryRunSettings.IsMonsterExtensionEnabled(runState))
        {
            return [];
        }

        return _listeners ??= [ModelDb.Modifier<LibraryOfRuinaHookListener>()];
    }

    /// <summary>
    /// 楼层完全解放的进度在胜利时写入嘉宾载体。
    /// <list type="bullet">
    /// <item>胜利时遭遇模型不是监听者，阶段怪物已被移出战斗、玩家能力已清，所以由这里代为记录。</item>
    /// <item>用 Early：同一个 Hook 里其余监听者的胜利回调（含载体自己的剧情）都在它之后；原来这一步在 Hook 的前缀里，
    /// 现在牌组的牌与附魔、id 序靠前的其他模组订阅者的 Early 会先执行，它们在原版与本模组里都不读解放进度。</item>
    /// <item>载体可能正是在这里首次创建。战斗内的 Modifiers 是 <c>CombatRoom</c> 构造时的快照，新载体不会在这一次胜利里
    /// 收到回调，与原来在前缀里创建相同。</item>
    /// <item>整个 Hook 在 <c>SaveRun</c> 之前完成，进度随本次胜利的存档写入；联机两端各自执行一次，结果相同。</item>
    /// </list>
    /// </summary>
    public override Task AfterCombatVictoryEarly(CombatRoom room)
    {
        if (room.Encounter is IFloorLiberationEncounter { IsFullyLiberated: true } liberation)
        {
            FloorLiberationProgress.MarkFullyLiberated(room.CombatState.RunState, liberation.LiberationFloorId);
        }

        return Task.CompletedTask;
    }
}
