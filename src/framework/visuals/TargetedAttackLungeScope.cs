using System;
using Godot;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.intents;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace LibraryOfRuina.framework.visuals;

internal sealed class TargetedAttackLungeScope : IDisposable
{
    private const float TargetStopOffsetX = 200f;

    private readonly ITargetedAttackLungeVisuals? _targetedVisuals;
    private bool _isApplied;

    public TargetedAttackLungeScope(MonsterModel attacker, IReadOnlyList<Creature>? targets = null)
    {
        NCombatRoom? combatRoom = NCombatRoom.Instance;
        if (combatRoom == null)
        {
            return;
        }

        NCreature? attackerNode = combatRoom.GetCreatureNode(attacker.Creature);
        if (attackerNode?.Visuals is not ITargetedAttackLungeVisuals targetedVisuals)
        {
            return;
        }

        IReadOnlyList<Creature> resolvedTargets = TargetedMonsterAttackHelper.GetTargetList(attacker, targets);
        if (!TryGetLeftMostPlayerX(combatRoom, resolvedTargets, out float targetX))
        {
            return;
        }

        float stopX = targetX + TargetStopOffsetX;
        float attackerX = attackerNode.GlobalPosition.X;
        targetedVisuals.SetNextAttackLungeOffset(new Vector2(stopX - attackerX, 0f));

        _targetedVisuals = targetedVisuals;
        _isApplied = true;
    }

    public void Dispose()
    {
        if (!_isApplied)
        {
            return;
        }

        _isApplied = false;
        _targetedVisuals?.ClearNextAttackLungeOffset();
    }

    private static bool TryGetLeftMostPlayerX(NCombatRoom combatRoom, IReadOnlyList<Creature> targets, out float leftMostX)
    {
        leftMostX = 0f;
        bool hasTarget = false;

        for (int i = 0; i < targets.Count; i++)
        {
            Creature target = targets[i];
            if (!target.IsPlayer || target.IsDead)
            {
                continue;
            }

            NCreature? targetNode = combatRoom.GetCreatureNode(target);
            if (targetNode == null)
            {
                continue;
            }

            float targetX = targetNode.GlobalPosition.X;
            if (!hasTarget || targetX < leftMostX)
            {
                leftMostX = targetX;
                hasTarget = true;
            }
        }

        return hasTarget;
    }
}
