using System.Collections.Generic;
using LibraryOfRuina.framework.visuals;

namespace LibraryOfRuina.content.guests;

/// <summary>
/// 来宾小人的 Spine 身体（tools/spine_from_sprite/guest_*.json 生成）：整张待机图配身体、头两根骨头，待机起伏、点头；
/// 打击、突刺、斩击蓄力后换成原攻击图（按人物本身对齐：打击往前冲，突刺、斩击人物原地不动），受击换原受击图，
/// 死亡原地低头垮下、不倒地。没有攻击图的芬恩与不莱梅乐队三人只用骨骼冲刺。
/// </summary>
internal static class GuestSpine
{
    /// <summary>攻击动画冲出到位（命中）的时刻，秒，与原版怪物攻击的默认等待相同。</summary>
    internal const float AttackImpactSeconds = 0.3f;

    /// <summary>死亡动画时长，秒；原版等它播完再做溶解消失。</summary>
    internal const float DeathSeconds = 1.2f;

    /// <summary>原版逐帧外观 <c>Lunge(...).Cycle()</c> 的默认轮换顺序。</summary>
    internal static readonly IReadOnlyList<string> StrikeThrustSlash = ["strike", "thrust", "slash"];

    internal static readonly IReadOnlyList<string> ThrustStrikeSlash = ["thrust", "strike", "slash"];

    /// <param name="directory">骨骼文件所在目录（相对 images/monsters，带结尾斜杠或为空）。</param>
    /// <param name="name">来宾名，对应 guest_&lt;name&gt;.atlas / .spine-json。</param>
    /// <param name="attackCycle">"Attack" 触发每次招式轮流播的动画；为 null 时不轮换（纱世按各自的触发播）。</param>
    /// <param name="extraTriggers">其他触发对应的动画。</param>
    internal static RuntimeSpineBody.Spec Create(
        string directory,
        string name,
        IReadOnlyList<string>? attackCycle,
        IReadOnlyDictionary<string, string>? extraTriggers = null) =>
        new(
            $"res://images/monsters/{directory}guest_{name}.atlas",
            $"res://images/monsters/{directory}guest_{name}.spine-json",
            IdleAnimation: "idle",
            AttackAnimation: attackCycle?[0] ?? "strike",
            HurtAnimation: "hurt",
            DeathAnimation: "die",
            DefaultMix: 0.12f,
            HurtHoldSeconds: 0.1f,
            Ghosts: null,
            ExtraTriggers: extraTriggers,
            AttackCycle: attackCycle,
            // 原版连击每段都重发攻击触发：拉回命中帧，段数再多也停在攻击姿势
            AttackHoldSeconds: AttackImpactSeconds);
}
