using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;
using LibraryOfRuina.framework.combat;

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

        if (CombatQueries.CreatureNodeOf(boss.Creature)
            is not NCreature node)
        {
            return;
        }

        await node.RefreshIntents();
        node.IntentContainer.Modulate = Colors.White;
        node.ToggleIsInteractable(on: false);
    }
}
