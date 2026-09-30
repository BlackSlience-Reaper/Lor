using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace LibraryOfRuina.framework.encounters;

internal interface ILiberationPhaseBoss
{
    int LiberationPhase { get; }

    Creature Creature { get; }

    Task TriggerReviveAndEmpowerState();

    void ForceReviveAndEmpowerState();
}

internal interface ILiberationPrimaryPhaseBoss : ILiberationPhaseBoss
{
}

internal static class LiberationPhaseTransition
{
    public static async Task ShowAsync(
        ILiberationPhaseBoss boss,
        bool triggerAnimation)
    {
        if (triggerAnimation)
        {
            await boss.TriggerReviveAndEmpowerState();
        }
        else
        {
            boss.ForceReviveAndEmpowerState();
        }

        // 保持 ?. 短路：没有战斗房间时不读 boss.Creature（原版 MonsterModel.Creature 未绑定时会抛异常）。
        // ILiberationPhaseBoss 不是 MonsterModel，用不上 CombatQueries 的模型重载。
        if (NCombatRoom.Instance?.GetCreatureNode(boss.Creature)
            is not NCreature node)
        {
            return;
        }

        await node.RefreshIntents();
        node.IntentContainer.Modulate = Colors.White;
        node.ToggleIsInteractable(on: false);
    }
}
