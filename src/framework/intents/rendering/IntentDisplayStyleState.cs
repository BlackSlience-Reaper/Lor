using System;
using System.Linq;
using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.patches;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace LibraryOfRuina.framework.intents.rendering;

/// <summary>
/// 当前的意图画法（模组设置“战斗界面 → 意图显示”，<c>LibraryOfRuinaSettings.IntentDisplayStyle</c> 读写这里）。
/// 只影响本机的意图节点：显示意图不进模型层、不参与联机比对，两端设置不同也不会分叉。
/// </summary>
internal static class IntentDisplayStyleState
{
    private static IntentDisplayStyle _current = IntentDisplayStyle.Default;

    internal static IntentDisplayStyle Current => _current;

    /// <summary>
    /// 写入新画法。设置框架没有逐项的变更回调（下拉框直接调属性 setter，读配置、恢复默认也走 setter），
    /// 所以在这里刷新：战斗中切换时立刻重画场上本模组怪物的意图，不在战斗里时什么都不做。
    /// </summary>
    internal static void Set(IntentDisplayStyle value)
    {
        if (_current == value)
        {
            return;
        }

        _current = value;
        RefreshCombatIntents();
    }

    /// <summary>
    /// 原版风格画法的适用对象：与默认画法的复合简化同一判定（本模组的怪物与接待怪物），不看阵营，友方单位也在内。
    /// 原版怪物和其他模组的怪物照旧走默认顺序表，等于不受影响。
    /// </summary>
    internal static bool Covers(Creature? owner) =>
        owner?.Monster is { } monster && CombinedIntentDisplayPatch.IsLibraryOfRuinaModMonster(monster);

    internal static bool UsesVanilla(Creature? owner) =>
        _current == IntentDisplayStyle.Vanilla && Covers(owner);

    // 重画走原版 NCreature.UpdateIntent（意图刷新的全部补丁都在它的后缀里），目标与原版 RefreshIntents 相同：全部玩家。
    // 不调用 RefreshIntents：它还会把意图容器淡入，敌方回合中途切换时会让本该隐藏的意图重新出现。
    private static void RefreshCombatIntents()
    {
        if (NCombatRoom.Instance is not { } room)
        {
            return;
        }

        foreach (NCreature creatureNode in room.CreatureNodes.ToArray())
        {
            Creature? creature = creatureNode.Entity;
            if (creature is not { IsAlive: true, Monster: not null } || !Covers(creature) || creature.CombatState == null)
            {
                continue;
            }

            try
            {
                _ = creatureNode.UpdateIntent(creature.CombatState.Players.Select(static player => player.Creature).ToArray());
            }
            catch (Exception exception)
            {
                LorLog.PatchFailure("IntentDisplayStyle.Refresh", exception);
            }
        }
    }
}
