using System;
using System.Linq;
using Godot;
using HarmonyLib;
using LibraryOfRuina.helpers;
using LibraryOfRuina.monsters.TechnologyFloorLiberation;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.patches.TechnologyFloorLiberation;

/// <summary>
/// Harmony postfix on <see cref="NCreature.UpdateIntent"/> that dims the second and third
/// attack intents of the Chord EGO move when the player's block is insufficient.
/// <para>
/// Logic: HitA is always shown at full opacity. HitB is unlocked (full opacity) when any
/// alive player's block &gt;= HitA's final damage. HitC is unlocked when HitB is already
/// unlocked AND any alive player's block &gt;= HitB's final damage.
/// </para>
/// </summary>
internal static class ChordEgoIntentDimPatch
{
    private static readonly Color DimColor = new(1f, 1f, 1f, 0.5f);

    internal static void OnUpdateIntent(NCreature __instance)
    {
        try
        {
            ApplyChordEgoDimming(__instance);
        }
        catch (Exception exception)
        {
            PatchFailureLog.Warn(
                "ChordEgoIntentDim.UpdateIntent",
                exception);
        }
    }

    private static void ApplyChordEgoDimming(NCreature creatureNode)
    {
        if (creatureNode.Entity?.Monster is not TechnologyFloorChordBoss boss)
        {
            return;
        }

        MoveState? nextMove = boss.NextMove;
        if (nextMove?.StateId != "CHORD_EGO")
        {
            return;
        }

        IReadOnlyList<AbstractIntent> intents = nextMove.Intents;

        // Collect the attack intents in order (should be HitA, HitB, HitC)
        var attackIntents = new List<AttackIntent>(3);
        foreach (AbstractIntent intent in intents)
        {
            if (intent is AttackIntent atk)
            {
                attackIntents.Add(atk);
            }
        }

        if (attackIntents.Count < 3)
        {
            return;
        }

        // Compute final damage for each segment
        Creature owner = creatureNode.Entity;
        IReadOnlyList<Creature> targets = owner.CombatState?.Players
            .Select(static p => p.Creature)
            .Where(static c => c.IsAlive)
            .ToArray() ?? [];

        int hitADamage = attackIntents[0].GetSingleDamage(targets, owner);
        int hitBDamage = attackIntents[1].GetSingleDamage(targets, owner);

        // Check max block among all alive players
        int maxPlayerBlock = 0;
        foreach (Creature target in targets)
        {
            if (target.Block > maxPlayerBlock)
            {
                maxPlayerBlock = target.Block;
            }
        }

        // HitB unlocked when any player's block >= HitA damage
        bool hitBUnlocked = maxPlayerBlock >= hitADamage;
        // HitC unlocked when HitB is unlocked AND any player's block >= HitB damage
        bool hitCUnlocked = hitBUnlocked && maxPlayerBlock >= hitBDamage;

        // Map attack intent index to NIntent child index
        // Intent order: [BuffIntent, PlayCardAttackIntent(HitA), SingleAttack(HitB), SingleAttack(HitC), BuffIntent]
        // NIntent children follow the same order
        int attackIndex = 0;
        int childIndex = 0;
        foreach (var child in creatureNode.IntentContainer.GetChildren())
        {
            if (child is NIntent intentNode)
            {
                // Check if this NIntent corresponds to one of our attack intents
                if (childIndex < intents.Count && intents[childIndex] is AttackIntent)
                {
                    switch (attackIndex)
                    {
                        case 0: // HitA - always full opacity
                            break;
                        case 1: // HitB
                            intentNode.Modulate = hitBUnlocked ? Colors.White : DimColor;
                            break;
                        case 2: // HitC
                            intentNode.Modulate = hitCUnlocked ? Colors.White : DimColor;
                            break;
                    }

                    attackIndex++;
                }

                childIndex++;
            }
        }
    }
}
