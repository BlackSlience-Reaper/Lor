using System.Collections.Generic;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.framework.visuals;

/// <summary>
/// 解放战 Boss 用原版分层立绘生成的 Spine 身体（tools/spine_from_layers/boss_*.json，生成方式见该目录 README）：
/// 每个原版动作一组身体、头、头发骨头，武器单独成层的挂武器骨头。每个触发一段动画：攻击先在待机姿势里蓄力
/// 再换原版姿势，受击后退泛红，防御小幅后坐，E.G.O 技能换技能姿势，原来的逐帧序列按顺序换姿势；
/// 姿势停留时长照原来逐帧外观的换图时长。死亡原地低头下沉。
/// </summary>
internal static class LayeredBossSpine
{
    /// <summary>死亡动画时长，秒。</summary>
    internal const float DeathSeconds = 1.2f;

    /// <param name="directory">骨骼文件所在目录（相对 images/monsters，不带斜杠）。</param>
    /// <param name="name">骨骼文件名 boss_&lt;name&gt;.atlas / .spine-json。</param>
    /// <param name="attackAnimation">"Attack" 触发播的动画。</param>
    /// <param name="extraTriggers">其他触发对应的动画。</param>
    internal static RuntimeSpineBody.Spec Create(
        string directory,
        string name,
        string attackAnimation,
        IReadOnlyDictionary<string, string> extraTriggers) =>
        new(
            $"res://images/monsters/{directory}/boss_{name}.atlas",
            $"res://images/monsters/{directory}/boss_{name}.spine-json",
            IdleAnimation: "idle",
            AttackAnimation: attackAnimation,
            HurtAnimation: "hurt",
            DeathAnimation: "die",
            DefaultMix: 0.12f,
            HurtHoldSeconds: 0.1f,
            Ghosts: null,
            ExtraTriggers: extraTriggers,
            // 连击每段重发攻击触发：拉回换上攻击姿势之后，段数再多也停在攻击姿势
            AttackHoldSeconds: 0.3f);

    /// <summary>
    /// 给 <c>DeathAnimLengthOverride</c> 用：只有死后会被移出战斗时才等死亡动画。
    /// 解放战转阶段的假死不移除节点，死亡补丁不播动画；原版却会对任何死亡按这个时长等待，
    /// 返回 0 让转阶段保持原来的节奏。
    /// </summary>
    internal static float DeathLength(MonsterModel monster) =>
        monster.Creature is { CombatState: { } combatState } creature
        && Hook.ShouldCreatureBeRemovedFromCombatAfterDeath(combatState, creature)
            ? DeathSeconds
            : 0f;
}
