using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.compat;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace LibraryOfRuina.encounters;

internal static class LiberationPhaseCleanup
{
    public static async Task<IReadOnlyList<Creature>>
        RestoreMissingLiveCreatureNodes(CombatStateLike combatState)
    {
        NCombatRoom? room = NCombatRoom.Instance;
        if (room == null)
        {
            return [];
        }

        var restored = new List<Creature>();
        foreach (Creature creature in combatState.Enemies
                     .Where(static enemy => enemy.IsAlive)
                     .ToArray())
        {
            if (room.GetCreatureNode(creature) != null)
            {
                continue;
            }

            foreach (NCreature staleNode in room.RemovingCreatureNodes
                         .Where(node => node.Entity == creature)
                         .ToArray())
            {
                NCreatureCompat.AnimHideIntent(staleNode);
                staleNode.ToggleIsInteractable(on: false);
                staleNode.Visible = false;
                staleNode.QueueFreeSafely();
            }

            room.AddCreature(creature);
            if (room.GetCreatureNode(creature) is not NCreature restoredNode)
            {
                continue;
            }

            await restoredNode.RefreshIntents();
            restored.Add(creature);
        }

        return restored;
    }

    public static async Task RemovePhaseCreatures(
        CombatStateLike combatState,
        Creature? except = null,
        bool includeDeadStateCreatures = true,
        Action<Creature>? beforeRemove = null)
    {
        foreach (Creature creature in CollectPhaseCreatures(combatState, except, includeDeadStateCreatures))
        {
            await RemoveTransitionCreature(creature, combatState, beforeRemove);
        }
    }

    public static async Task RemoveTransitionCreature(
        Creature creature,
        CombatStateLike? combatState = null,
        Action<Creature>? beforeRemove = null)
    {
        beforeRemove?.Invoke(creature);
        bool removedNode = RemoveCreatureNode(creature);

        CombatStateLike? resolvedCombatState = combatState ?? creature.CombatState;
        if (resolvedCombatState?.Enemies.Contains(creature) == true)
        {
            CombatManager.Instance.RemoveCreature(creature);
            resolvedCombatState.RemoveCreature(creature);
        }
        else if (removedNode)
        {
            CombatManager.Instance.RemoveCreature(creature);
        }

        await Cmd.CustomScaledWait(0.15f, 0.375f);
    }

    private static IReadOnlyList<Creature> CollectPhaseCreatures(
        CombatStateLike combatState,
        Creature? except,
        bool includeDeadStateCreatures)
    {
        var creatures = new List<Creature>();

        foreach (Creature enemy in combatState.Enemies.ToArray())
        {
            if (ShouldRemove(enemy, combatState, except, includeDeadStateCreatures))
            {
                AddUnique(creatures, enemy);
            }
        }

        if (NCombatRoom.Instance == null)
        {
            return creatures;
        }

        foreach (NCreature node in NCombatRoom.Instance.CreatureNodes
                     .Concat(NCombatRoom.Instance.RemovingCreatureNodes)
                     .ToArray())
        {
            Creature creature = node.Entity;
            if (creature.IsEnemy
                && ShouldRemove(creature, combatState, except, includeDeadStateCreatures))
            {
                AddUnique(creatures, creature);
            }
        }

        return creatures;
    }

    private static bool ShouldRemove(
        Creature creature,
        CombatStateLike combatState,
        Creature? except,
        bool includeDeadStateCreatures)
    {
        if (creature == except || !creature.IsEnemy)
        {
            return false;
        }

        bool isStillInState = combatState.Enemies.Contains(creature);
        return includeDeadStateCreatures
            || creature.IsAlive
            || !isStillInState;
    }

    private static void AddUnique(List<Creature> creatures, Creature creature)
    {
        if (!creatures.Contains(creature))
        {
            creatures.Add(creature);
        }
    }

    private static bool RemoveCreatureNode(Creature creature)
    {
        NCombatRoom? room = NCombatRoom.Instance;
        if (room == null)
        {
            return false;
        }

        NCreature? node = room.CreatureNodes.FirstOrDefault(activeNode => activeNode.Entity == creature);
        bool isActiveNode = node != null;
        node ??= room.RemovingCreatureNodes.FirstOrDefault(removingNode => removingNode.Entity == creature);

        if (node == null || !GodotObject.IsInstanceValid(node))
        {
            return false;
        }

        NCreatureCompat.AnimHideIntent(node);
        if (isActiveNode)
        {
            room.RemoveCreatureNode(node);
        }

        node.ToggleIsInteractable(on: false);
        node.Visible = false;
        node.QueueFreeSafely();
        return true;
    }
}
