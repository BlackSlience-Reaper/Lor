using System.Threading.Tasks;
using LibraryLib.Models;
using LibraryOfRuina.encounters;

namespace LibraryOfRuina.monsters;

/// <summary>
/// 本模组怪物的公共基类，收拢各怪物复制的流程。抽象类不会被 ModelDb 登记，不产生模型 ID。
/// <list type="bullet">
/// <item>离开房间时从遭遇 BGM 的登记里移除（<see cref="EncounterBgmController.UnregisterMonster"/> 只移出集合、
/// 解绑死亡事件，没登记过的怪物是空操作）。登记（<c>RegisterMonster</c>）仍由各怪物在 <c>AfterAddedToRoom</c> 里自己调用：
/// 它会立即按遭遇当前状态（阶段、背景层）选曲，时机因怪物而异。离场时还要清理表现层的子类先调用 base：
/// 清理抛异常时注销已经完成，不会留下死亡事件订阅。</item>
/// <item>按回合缓存的伤害骰（<see cref="EnsureDamageRoll"/>、<see cref="GetOrRollDamage"/>）。</item>
/// </list>
/// </summary>
public abstract class LorMonsterModel : LibraryMonsterModel
{
    public override void BeforeRemovedFromRoom()
    {
        EncounterBgmController.UnregisterMonster(Creature);
        base.BeforeRemovedFromRoom();
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

    /// <summary>只读缓存，不掷骰；没有缓存时返回上限（意图显示用）。</summary>
    protected int GetOrRollDamage(ref int? cachedRoll, int minInclusive, int maxInclusive)
    {
        if (!IsMutable)
        {
            return maxInclusive;
        }

        return cachedRoll ?? maxInclusive;
    }
}
