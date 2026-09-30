using System.Linq;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace LibraryOfRuina.framework.combat;

/// <summary>
/// 战斗里反复出现的只读查询。每个方法与它替代的内联写法逐字等价，调用处可以直接互换。
/// </summary>
internal static class CombatQueries
{
    /// <summary>
    /// 等价于 <c>NCombatRoom.Instance?.GetCreatureNode(creature)</c>：没有战斗房间或找不到节点时返回 null。
    /// 与原版 <c>Creature.GetCreatureNode()</c> 不同：这里不看 <c>TestMode</c>，也不回退到图鉴界面的节点。
    /// 表现层代码拿到 null 就跳过，不要改成抛异常。
    /// </summary>
    public static NCreature? CreatureNodeOf(Creature? creature) =>
        NCombatRoom.Instance?.GetCreatureNode(creature);

    /// <summary>
    /// 等价于怪物内部的 <c>NCombatRoom.Instance?.GetCreatureNode(Creature)</c>。
    /// 传入模型而不是生物：原写法在没有战斗房间时不会读 <c>Creature</c>（生物尚未设置时该属性会抛异常），这里保持同样的求值顺序。
    /// </summary>
    public static NCreature? CreatureNodeOf(MonsterModel monster) =>
        NCombatRoom.Instance?.GetCreatureNode(monster.Creature);

    /// <summary>
    /// 等价于能力内部的 <c>NCombatRoom.Instance?.GetCreatureNode(Owner)</c>。
    /// 原因同上：原写法在没有战斗房间时不会读 <c>Owner</c>（规范模型上该属性会抛异常）。
    /// </summary>
    public static NCreature? CreatureNodeOf(PowerModel power) =>
        NCombatRoom.Instance?.GetCreatureNode(power.Owner);

    /// <summary>
    /// 等价于 <c>state.PlayerCreatures.Where(c =&gt; c.IsAlive)</c>：按原版列表顺序、延迟求值。
    /// 可以接在 <c>?.</c> 后面，空状态的写法保持不变。
    /// </summary>
    public static IEnumerable<Creature> LivingPlayerCreatures(this ICombatState state) =>
        state.PlayerCreatures.Where(static creature => creature.IsAlive);

    /// <summary>等价于 <c>state.Players.Where(p =&gt; p.Creature.IsAlive)</c>。</summary>
    public static IEnumerable<Player> LivingPlayers(this ICombatState state) =>
        state.Players.Where(static player => player.Creature.IsAlive);

    /// <summary>
    /// 等价于 <c>state.Enemies.Where(c =&gt; c.IsAlive)</c>。敌方一侧包含本模组的友方单位与召唤物，
    /// 需要排除它们的地方仍按原写法单独过滤。
    /// </summary>
    public static IEnumerable<Creature> LivingEnemies(this ICombatState state) =>
        state.Enemies.Where(static creature => creature.IsAlive);

    /// <summary>等价于 <c>state.HittableEnemies.Where(c =&gt; c.IsAlive)</c>。</summary>
    public static IEnumerable<Creature> LivingHittableEnemies(this ICombatState state) =>
        state.HittableEnemies.Where(static creature => creature.IsAlive);

    /// <summary>等价于 <c>state.Creatures.Where(c =&gt; c.IsAlive)</c>（先友方一侧、后敌方一侧）。</summary>
    public static IEnumerable<Creature> LivingCreatures(this ICombatState state) =>
        state.Creatures.Where(static creature => creature.IsAlive);
}
