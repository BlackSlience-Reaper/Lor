using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using LibraryOfRuina.helpers;
using LibraryOfRuina.intents;
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

    internal static void RefreshAnimation(
        NIntent intentNode,
        AbstractIntent intent,
        IEnumerable<Creature> targets,
        Creature owner)
    {
        if (intent is not ICounterIntentVisual counterIntent)
        {
            States.Remove(intentNode);
            return;
        }

        AnimationState state = States.GetValue(intentNode, static _ => new AnimationState());
        state.Animation = intent is CounterAttackIntent attack
            ? attack.GetCounterAnimation(targets, owner)
            : counterIntent.CounterAnimation;
        state.Frame = null;
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

    internal static void OnIntentProcess(
        NIntent __instance,
        int? ____animationFrame)
    {
        try
        {
            if (!States.TryGetValue(__instance, out AnimationState? state)
                || !____animationFrame.HasValue
                || state.Frame == ____animationFrame)
            {
                return;
            }

            if (!__instance.HasNode("%Intent"))
            {
                return;
            }

            if (!CounterIntentAnimData.TryGetAnimationFrame(state.Animation, ____animationFrame.Value, out string path))
            {
                return;
            }

            Sprite2D sprite = __instance.GetNode<Sprite2D>("%Intent");
            GodotTextureSafety.TrySetTexture(sprite, PreloadManager.Cache.GetTexture2D(path));
            state.Frame = ____animationFrame;
        }
        catch (Exception exception)
        {
            PatchFailureLog.Warn(
                "CounterIntentVisual.UpdateIntent",
                exception);
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
    internal static void OnUpdateIntent(NCreature __instance, IEnumerable<Creature> targets)
    {
        try
        {
            if (__instance.Entity is not { IsEnemy: true, Monster: ICounterIntentQueueOwner owner })
            {
                return;
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
        }
        catch (Exception exception)
        {
            PatchFailureLog.Warn(
                "CounterIntentAppend.UpdateIntent",
                exception);
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
