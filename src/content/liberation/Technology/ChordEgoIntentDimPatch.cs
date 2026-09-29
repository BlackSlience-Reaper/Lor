using System;
using System.Linq;
using Godot;
using LibraryOfRuina.framework.intents.rendering;
using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.content.liberation.Technology;

/// <summary>
/// Runs after <c>NIntent.UpdateVisuals</c> (IntentRenderPipeline, IntentVisuals stage) and dims the second and third
/// attack intents of the Chord EGO move when the player's block is insufficient.
/// <para>
/// Logic: HitA does not receive additional dimming. HitB is unlocked when any
/// alive player's block &gt;= HitA's final damage. HitC is unlocked when HitB is already
/// unlocked AND any alive player's block &gt;= HitB's final damage.
/// The seal decorator resets the previous color before this decorator runs; unlocked
/// attacks retain that color so sealing and conditional attacks can both remain dimmed.
/// </para>
/// </summary>
internal static class ChordEgoIntentDimPatch
{
    private static readonly Color DimColor = new(1f, 1f, 1f, 0.5f);

    internal static IntentDecoratorOutcome OnUpdateVisuals(NIntent intentNode, AbstractIntent intent, Creature owner)
    {
        try
        {
            return ApplyChordEgoDimming(intentNode, intent, owner)
                ? IntentDecoratorOutcome.Applied
                : IntentDecoratorOutcome.Skipped;
        }
        catch (Exception exception)
        {
            PatchFailureLog.Warn(
                "ChordEgoIntentDim.UpdateVisuals",
                exception);
            return IntentDecoratorOutcome.Failed;
        }
    }

    private static bool ApplyChordEgoDimming(NIntent intentNode, AbstractIntent currentIntent, Creature owner)
    {
        if (owner.Monster is not TechnologyFloorChordBoss boss || currentIntent is not AttackIntent)
        {
            return false;
        }

        MoveState? nextMove = boss.NextMove;
        if (nextMove?.StateId != "CHORD_EGO")
        {
            return false;
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
            return false;
        }

        // 只处理当前刷新的 B/C 段，避免每个 NIntent 的状态回调重复重绘整个意图容器。
        bool isHitB = ReferenceEquals(currentIntent, attackIntents[1]);
        bool isHitC = ReferenceEquals(currentIntent, attackIntents[2]);
        if (!isHitB && !isHitC)
        {
            return false;
        }

        // Compute final damage for each segment
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

        if ((isHitB && !hitBUnlocked) || (isHitC && !hitCUnlocked))
        {
            intentNode.Modulate = DimColor;
        }

        return true;
    }
}
