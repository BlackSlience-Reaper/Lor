using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.features.moontext;

/// <summary>
/// 怪物背景月字循环的常用写法。行键是 <c>monsters</c> 表的键，经原版 <c>MonsterModel.L10NMonsterLookup</c> 解析；
/// 解析发生在调用 <see cref="MoonTextService"/> 之前，月字设置关闭时也照样解析，与原来的内联写法一致。
/// 月字只是表现，不进存档、不参与联机同步。
/// </summary>
internal static class MonsterMoonTextLoop
{
    /// <summary>不绑定说话者的全局循环，等价于 <c>MoonTextService.StartRandomLoop(keys.Select(L10NMonsterLookup).ToArray(), …)</c>。</summary>
    public static void Start(IEnumerable<string> lineKeys, float intervalSeconds, Rect2 spawnArea) =>
        MoonTextService.StartRandomLoop(
            lineKeys.Select(MonsterModel.L10NMonsterLookup).ToArray(),
            intervalSeconds,
            spawnArea);

    /// <summary>
    /// 绑定怪物生物与作用域的循环，每只怪物只开一次：<paramref name="started"/> 已置位或生物已死亡时什么都不做。
    /// <paramref name="started"/> 是怪物自己的字段；停止循环时要不要复位由各怪物决定（多数不复位，所以同一场战斗里不会重开）。
    /// 先查标记、后读 <c>Creature</c>，与原写法的求值顺序相同。
    /// </summary>
    public static void StartOnce(
        MonsterModel monster,
        ref bool started,
        string scope,
        IEnumerable<string> lineKeys,
        float intervalSeconds,
        Rect2 spawnArea)
    {
        if (started || monster.Creature.IsDead)
        {
            return;
        }

        started = true;
        MoonTextService.StartRandomLoop(
            monster.Creature,
            scope,
            lineKeys.Select(MonsterModel.L10NMonsterLookup).ToArray(),
            intervalSeconds,
            spawnArea);
    }

    /// <summary>
    /// 停止 <see cref="StartOnce"/> 开的循环；没有月字控制器时什么都不做。
    /// 生物尚未设置时读 <c>Creature</c> 会抛异常，原来各处的 <c>Creature != null</c> 判断也拦不住这一点，这里不再保留。
    /// </summary>
    public static void Stop(MonsterModel monster, string scope) =>
        MoonTextService.StopRandomLoop(monster.Creature, scope);
}
