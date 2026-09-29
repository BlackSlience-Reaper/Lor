using System;
using System.Runtime.CompilerServices;
using Godot;
using LibraryOfRuina.helpers;
using LibraryOfRuina.intents;
using LibraryOfRuina.intents.rendering;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.patches;

/// <summary>
/// Replaces the NIntent sprite with the baked STS1-style combined intent frames.
/// The intent's GetAnimation returns a vanilla 30-frame animation so the vanilla
/// frame counter loops 0..29; we remap each frame to our own PNG sequence.
/// </summary>
internal static class CombinedIntentVisualPatch
{
    private static readonly ConditionalWeakTable<NIntent, AnimationState> States = new();

    internal static IntentDecoratorOutcome RefreshAnimation(
        NIntent intentNode,
        AbstractIntent intent,
        IEnumerable<Creature> targets,
        Creature owner)
    {
        if (intent is not ICombinedIntentVisual combined)
        {
            States.Remove(intentNode);
            return IntentDecoratorOutcome.Skipped;
        }

        // 伤害档位仅随原版意图刷新重新计算，逐帧播放不再遍历全部战斗 Hook。
        AnimationState state = States.GetValue(intentNode, static _ => new AnimationState());
        state.Animation = combined.GetCombinedAnimation(targets, owner);
        state.Frame = null;
        return IntentDecoratorOutcome.Applied;
    }

    internal static IntentDecoratorOutcome OnIntentProcess(NIntent __instance, int? ____animationFrame)
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

            if (!CombinedIntentAnimData.TryGetAnimationFrame(
                    state.Animation,
                    ____animationFrame.Value,
                    out string path))
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
            PatchFailureLog.Warn(
                "CombinedIntentVisual.UpdateIntent",
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
