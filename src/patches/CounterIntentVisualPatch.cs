using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Godot;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.intents.rendering;
using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace LibraryOfRuina.patches;

internal static class CounterIntentVisualPatch
{
    private static readonly ConditionalWeakTable<NIntent, AnimationState> States = new();

    internal static IntentDecoratorOutcome RefreshAnimation(
        NIntent intentNode,
        AbstractIntent intent,
        IEnumerable<Creature> targets,
        Creature owner)
    {
        if (intent is not ICounterIntentVisual counterIntent)
        {
            States.Remove(intentNode);
            return IntentDecoratorOutcome.Skipped;
        }

        AnimationState state = States.GetValue(intentNode, static _ => new AnimationState());
        state.Animation = intent is CounterAttackIntent attack
            ? attack.GetCounterAnimation(targets, owner)
            : counterIntent.CounterAnimation;
        state.Frame = null;
        return IntentDecoratorOutcome.Applied;
    }

    public static void RefreshCounterIntentDisplay(Creature owner)
    {
        NCreature? creatureNode = NCombatRoom.Instance?.GetCreatureNode(owner);
        if (creatureNode == null)
        {
            return;
        }

        TaskHelper.RunSafely(creatureNode.RefreshIntents());
    }

    internal static IntentDecoratorOutcome OnIntentProcess(
        NIntent __instance,
        int? ____animationFrame)
    {
        try
        {
            if (!States.TryGetValue(__instance, out AnimationState? state)
                || !____animationFrame.HasValue)
            {
                return IntentDecoratorOutcome.Skipped;
            }

            if (state.Frame == ____animationFrame)
            {
                return IntentDecoratorOutcome.Unchanged;
            }

            if (!__instance.HasNode("%Intent"))
            {
                return IntentDecoratorOutcome.Skipped;
            }

            if (!CounterIntentAnimData.TryGetAnimationFrame(state.Animation, ____animationFrame.Value, out string path))
            {
                return IntentDecoratorOutcome.Skipped;
            }

            Sprite2D sprite = __instance.GetNode<Sprite2D>("%Intent");
            GodotTextureSafety.TrySetTexture(sprite, PreloadManager.Cache.GetTexture2D(path));
            state.Frame = ____animationFrame;
            return IntentDecoratorOutcome.Applied;
        }
        catch (Exception exception)
        {
            LorLog.PatchFailure(
                "CounterIntentVisual.UpdateIntent",
                exception);
            return IntentDecoratorOutcome.Failed;
        }
    }

    private sealed class AnimationState
    {
        public string Animation = string.Empty;

        public int? Frame;
    }
}

internal static class CounterIntentAppendPatch
{
    internal static IntentDecoratorOutcome OnUpdateIntent(NCreature __instance, IEnumerable<Creature> targets)
    {
        try
        {
            if (__instance.Entity is not { IsEnemy: true, Monster: ICounterIntentQueueOwner owner })
            {
                return IntentDecoratorOutcome.Skipped;
            }

            Control container = __instance.IntentContainer;
            Creature creature = __instance.Entity;
            if (creature.Monster is CounterIntentMonsterModel counterMonster)
            {
                counterMonster.SyncCounterIntentsWithCurrentMove(refresh: false);
            }

            IReadOnlyList<Creature> targetList = targets as IReadOnlyList<Creature> ?? targets.ToArray();
            Creature? viewer = ResolveCounterViewer(creature, targetList);
            IReadOnlyList<AbstractIntent> displayIntents = creature.Monster.NextMove.Intents
                .Where(intent => intent is not ICounterIntent counterIntent || owner.CounterIntentQueue.ShouldDisplay(counterIntent, viewer))
                .ToArray();
            displayIntents = CombinedIntentDisplayPatch.SimplifyForDisplay(displayIntents);
            int requiredCount = displayIntents.Count;
            float startOffset = __instance.GetHashCode() / 100f;

            for (int i = container.GetChildCount(); i < requiredCount; i++)
            {
                NIntent nIntent = NIntent.Create(startOffset + i * 0.3f);
                container.AddChildSafely(nIntent);
            }

            for (int i = 0; i < displayIntents.Count; i++)
            {
                NIntent child = container.GetChild<NIntent>(i);
                child.SetFrozen(isFrozen: false);
                child.UpdateIntent(displayIntents[i], ResolveIntentTargets(displayIntents[i], targetList, viewer), creature);
            }

            foreach (Node extra in container.GetChildren().TakeLast(container.GetChildCount() - requiredCount).ToArray())
            {
                container.RemoveChildSafely(extra);
                extra.QueueFreeSafely();
            }

            return IntentDecoratorOutcome.Applied;
        }
        catch (Exception exception)
        {
            LorLog.PatchFailure(
                "CounterIntentAppend.UpdateIntent",
                exception);
            return IntentDecoratorOutcome.Failed;
        }
    }

    private static Creature? ResolveCounterViewer(Creature owner, IReadOnlyList<Creature> fallbackTargets)
    {
        if (LocalContext.NetId.HasValue && owner.CombatState != null)
        {
            ulong localPlayerId = LocalContext.NetId.Value;
            Creature? localCreature = owner.CombatState.PlayerCreatures
                .FirstOrDefault(creature => creature.Player?.NetId == localPlayerId);
            if (localCreature != null)
            {
                return localCreature;
            }
        }

        return fallbackTargets.FirstOrDefault(static target => target.IsPlayer);
    }

    private static IEnumerable<Creature> ResolveIntentTargets(
        AbstractIntent intent,
        IReadOnlyList<Creature> fallbackTargets,
        Creature? viewer)
    {
        return intent is ICounterIntent && viewer != null
            ? [viewer]
            : fallbackTargets;
    }
}
