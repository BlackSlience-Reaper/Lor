using System.Linq;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.encounters;

/// <summary>
/// 分阶段的楼层解放遭遇战的公共基类，收拢各楼层复制的流程。抽象类不会被 ModelDb 登记，不产生模型 ID；
/// 原版按“<c>AbstractModel</c> 的直接子类”推导模型类别，中间多一层不改变遭遇的类别与 ID。
/// <list type="bullet">
/// <item>阶段推进后刷新 BGM（<see cref="RefreshLiberationPhaseBgm"/>）。基类不实现 <see cref="ILiberationPhaseBgmSource"/>：
/// 按阶段选曲的遭遇自己声明这个接口，基类的方法作为它的实现。</item>
/// <item>最后一名玩家受致死伤害时的结算：末位存活判断（<see cref="IsLastAlivePlayer"/>）、以胜利结束战斗
/// （<see cref="EndCombatAsLiberationVictory"/>）与延迟胜负复核（<see cref="ScheduleDeferredWinConditionCheck"/>）。</item>
/// </list>
/// 阶段、击杀数等状态的字段与存读档仍由各楼层自己声明：各楼层的键集合、写入顺序、缺省值与读旧档的兼容分支都不同，
/// 解析交给 <see cref="EncounterStateBag"/>。
/// 哲学层只有一个阶段、不按阶段选曲也不走致死结算，不继承本类。
/// </summary>
public abstract class LiberationEncounterBase : EncounterModel
{
    /// <summary>
    /// 阶段写入后调用：让遭遇 BGM 按新阶段重新选曲。只影响本地音频表现，曲目只会前进不会回退。
    /// </summary>
    public virtual void RefreshLiberationPhaseBgm()
    {
        EncounterBgmController.RefreshCurrentEncounterTrack();
    }

    /// <summary>除 <paramref name="creature"/> 以外没有存活的玩家；不在战斗中时返回 false。</summary>
    protected static bool IsLastAlivePlayer(Creature creature)
    {
        if (creature.CombatState is not { } combatState)
        {
            return false;
        }

        return !combatState.PlayerCreatures.Any(player => player != creature && player.IsAlive);
    }

    /// <summary>
    /// 最后一名玩家受到致死伤害、结算已记录后调用：强制击杀所有存活的敌人，再让原版复核胜负以正常胜利结束战斗。
    /// 战斗已停止进行时直接返回，避免挂起的死亡回调重新进入击杀与胜负复核。
    /// </summary>
    /// <param name="deferRecheckIfNotEnded">
    /// 为 true 时，这次复核没有结束战斗，就用 <see cref="ScheduleDeferredWinConditionCheck"/> 在下一帧再复核一次。
    /// </param>
    protected static async Task EndCombatAsLiberationVictory(
        CombatStateLike? combatState,
        bool deferRecheckIfNotEnded = false)
    {
        if (combatState == null || !CombatManager.Instance.IsInProgress)
        {
            return;
        }

        foreach (Creature enemy in combatState.Enemies.ToArray())
        {
            if (enemy.IsAlive)
            {
                await CreatureCmd.Kill(enemy, force: true);
            }
        }

        if (CombatManager.Instance.IsInProgress)
        {
            bool ended = await CombatManager.Instance.CheckWinCondition();
            if (!ended && deferRecheckIfNotEnded)
            {
                ScheduleDeferredWinConditionCheck();
            }
        }
    }

    /// <summary>下一帧（<c>CallDeferred</c>）战斗仍在进行时再复核一次胜负。</summary>
    protected static void ScheduleDeferredWinConditionCheck()
    {
        Callable.From(() =>
        {
            if (CombatManager.Instance.IsInProgress)
            {
                _ = TaskHelper.RunSafely(
                    CombatManager.Instance.CheckWinCondition());
            }
        }).CallDeferred();
    }
}
