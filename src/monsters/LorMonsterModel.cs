using System.Threading.Tasks;
using LibraryLib.Models;
using LibraryOfRuina.combat;
using LibraryOfRuina.encounters;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.monsters;

/// <summary>
/// 本模组怪物的公共基类，收拢各怪物复制的流程。抽象类不会被 ModelDb 登记，不产生模型 ID。
/// <list type="bullet">
/// <item>离开房间时从遭遇 BGM 的登记里移除（<see cref="EncounterBgmController.UnregisterMonster"/> 只移出集合、
/// 解绑死亡事件，没登记过的怪物是空操作）。登记（<c>RegisterMonster</c>）仍由各怪物在 <c>AfterAddedToRoom</c> 里自己调用：
/// 它会立即按遭遇当前状态（阶段、背景层）选曲，时机因怪物而异。离场时还要清理表现层的子类先调用 base：
/// 清理抛异常时注销已经完成，不会留下死亡事件订阅。</item>
/// <item>按回合缓存的伤害骰（<see cref="EnsureDamageRoll"/>、<see cref="GetOrRollDamage"/>）。</item>
/// <item>盟友规则：本模组盟友都是 <see cref="LorMonsterModel"/>（<see cref="IAllyTurnProvider"/> 的契约），
/// 由 <see cref="AllyTurnRegistry"/> 按遭遇判断谁是盟友，所以怪物本身在不在盟友状态要在运行时判断。
/// 子类覆写 <see cref="ShouldClearBlock"/>、<see cref="ModifyPowerAmountGivenMultiplicative"/> 时要保留 base 的结果。</item>
/// </list>
/// </summary>
public abstract class LorMonsterModel : LibraryMonsterModel
{
    public override void BeforeRemovedFromRoom()
    {
        EncounterBgmController.UnregisterMonster(Creature);
        base.BeforeRemovedFromRoom();
    }

    /// <summary>
    /// 盟友在敌方一侧，原版敌方回合开始时会清它的格挡。本回合已在盟友回合行动过的盟友，以及格挡转移的搭档保留格挡，
    /// 由本怪物作为 preventer；盟友的格挡在玩家回合开始时由 <c>AllyClearBlockAtPlayerTurnStartPatch</c> 清除。
    /// 同一生物的能力排在怪物之前询问，它们自己阻止清除时仍是 preventer。
    /// </summary>
    public override bool ShouldClearBlock(Creature creature)
    {
        if (creature != Creature)
        {
            return base.ShouldClearBlock(creature);
        }

        return !AllyTurnRegistry.ShouldPreventVanillaBlockClearing(creature)
            && !BlockTransferEncounterTargetHelper.IsBlockTransferPartner(creature.CombatState, creature)
            && base.ShouldClearBlock(creature);
    }

    /// <summary>
    /// 玩家方用敌方范围的卡给本怪物施加能力、而本怪物此时是友方盟友时，数值作废（强化、标记等非减益也一样）。
    /// 原版的给予修正先加后乘，乘法一轮是连乘，×0 与其他监听者的先后无关。
    /// </summary>
    public override decimal ModifyPowerAmountGivenMultiplicative(
        PowerModel power,
        Creature giver,
        decimal amount,
        Creature? target,
        CardModel? cardSource)
    {
        if (target == Creature
            && cardSource?.TargetType is TargetType.AnyEnemy or TargetType.AllEnemies or TargetType.RandomEnemy
            && AllyTurnRegistry.IsPlayerAlignedForTargeting(giver)
            && AllyTurnRegistry.IsFriendlyAlly(target))
        {
            return 0m;
        }

        return base.ModifyPowerAmountGivenMultiplicative(power, giver, amount, target, cardSource);
    }

    /// <summary>缓存里没有就用 MonsterAi 随机数掷一次（两端同步）；规范模型上返回上限，供卡面预览。</summary>
    protected int EnsureDamageRoll(ref int? cachedRoll, int minInclusive, int maxInclusive)
    {
        if (!IsMutable)
        {
            return maxInclusive;
        }

        cachedRoll ??= RunRng.MonsterAi.NextInt(minInclusive, maxInclusive + 1);
        return cachedRoll.Value;
    }

    /// <summary>
    /// 只读缓存，不掷骰；没有缓存时返回上限（意图显示用）。意图标签、悬停提示、预览会在本地渲染路径里调用伤害
    /// 表达式，各端的时机和次数不同，在这里掷骰会让 MonsterAi 随机数流分叉，出招时各端的缓存值不同。
    /// </summary>
    protected int GetOrRollDamage(ref int? cachedRoll, int minInclusive, int maxInclusive)
    {
        if (!IsMutable)
        {
            return maxInclusive;
        }

        return cachedRoll ?? maxInclusive;
    }
}
