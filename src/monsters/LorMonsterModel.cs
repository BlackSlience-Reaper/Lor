using System.Threading.Tasks;
using LibraryLib.Models;
using LibraryOfRuina.encounters;

namespace LibraryOfRuina.monsters;

/// <summary>
/// 本模组怪物的公共基类，收拢各怪物复制的流程。抽象类不会被 ModelDb 登记，不产生模型 ID。
/// <list type="bullet">
/// <item>离开房间时从遭遇 BGM 的登记里移除（<see cref="EncounterBgmController.UnregisterMonster"/> 只移出集合、
/// 解绑死亡事件，没登记过的怪物是空操作）。登记（<c>RegisterMonster</c>）仍由各怪物在 <c>AfterAddedToRoom</c> 里自己调用：
/// 它会立即按遭遇当前状态（阶段、背景层）选曲，时机因怪物而异。</item>
/// </list>
/// </summary>
public abstract class LorMonsterModel : LibraryMonsterModel
{
    public override void BeforeRemovedFromRoom()
    {
        EncounterBgmController.UnregisterMonster(Creature);
        base.BeforeRemovedFromRoom();
    }
}
