using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Godot;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.intents.rendering;
using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.ui;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;

namespace LibraryOfRuina.patches;

/// <summary>
/// 原版风格画法在怪物层的处理函数，由 <see cref="IntentRenderPipeline"/> 的原版风格顺序表调用（入口在 IntentVisualDispatch）。
/// <para>
/// 原版 <c>NCreature.UpdateIntent</c> 先按 <c>NextMove.Intents</c> 一一建好节点，这里在后缀里把节点重画成映射后的显示意图，
/// 做法与默认画法的复合简化相同（复用、补足、删掉多余的 <c>NIntent</c>）。显示意图只挂在本地节点上。
/// </para>
/// </summary>
internal static class VanillaIntentDisplayPatch
{
    // 以意图容器为弱键：第一次刷新意图时记下的原生布局。默认画法的运行时出牌（卡莉、魔弹）会改容器间距与尺寸，
    // 局内切到原版风格画法时据此还原。只在 CreatureLayout 入口最前面记录一次，记录本身不改节点。
    private static readonly ConditionalWeakTable<Control, NativeContainerLayout> NativeLayouts = new();

    /// <summary>CreatureLayout 入口：两种画法都在装饰器之前调用，只读。</summary>
    internal static void RememberNativeLayout(NCreature creatureNode)
    {
        try
        {
            Control container = creatureNode.IntentContainer;
            if (!NativeLayouts.TryGetValue(container, out _))
            {
                NativeLayouts.Add(container, NativeContainerLayout.Capture(container));
            }
        }
        catch (Exception exception)
        {
            LorLog.PatchFailure("VanillaIntentDisplay.RememberLayout", exception);
        }
    }

    /// <summary>
    /// CreatureLayout：映射并重画意图节点。反击队列的拥有者先经过 <see cref="CounterIntentAppendPatch.TryCollectVisibleIntents"/>，
    /// 与默认画法同一时机同步反击队列、按本地玩家过滤；反击意图的目标照旧换成观察者本人。
    /// </summary>
    internal static IntentDecoratorOutcome OnUpdateIntent(NCreature creatureNode, IEnumerable<Creature> targets)
    {
        try
        {
            Creature? creature = creatureNode.Entity;
            if (creature?.Monster == null)
            {
                return IntentDecoratorOutcome.Skipped;
            }

            IReadOnlyList<AbstractIntent> moveIntents = creature.Monster.NextMove.Intents;
            bool counterOwner = CounterIntentAppendPatch.TryCollectVisibleIntents(
                creatureNode,
                targets,
                out IReadOnlyList<AbstractIntent> sourceIntents,
                out IReadOnlyList<Creature> targetList,
                out Creature? viewer);
            if (!counterOwner)
            {
                sourceIntents = moveIntents;
                targetList = targets as IReadOnlyList<Creature> ?? targets.ToArray();
            }

            IReadOnlyList<VanillaIntentPart> parts = VanillaIntentMapper.Map(sourceIntents, moveIntents);
            if (!counterOwner
                && VanillaIntentMapper.IsIdentity(parts, moveIntents)
                && creatureNode.IntentContainer.GetChildCount() == parts.Count)
            {
                // 全是原版意图：原版 UpdateIntent 已经画好。
                return IntentDecoratorOutcome.Unchanged;
            }

            Render(creatureNode, parts, targetList, counterOwner ? viewer : null);
            return IntentDecoratorOutcome.Applied;
        }
        catch (Exception exception)
        {
            LorLog.PatchFailure("VanillaIntentDisplay.UpdateIntent", exception);
            return IntentDecoratorOutcome.Failed;
        }
    }

    /// <summary>
    /// CreatureDecorate：敌方卡牌。运行时出牌计划（卡莉、魔弹）不在 <c>NextMove.Intents</c> 里，与默认画法一样在这一步按计划重画，
    /// 每张牌的意图按出牌顺序并排，卡牌照默认画法挂在意图上方（见 <see cref="Render"/>）。
    /// 招式里直接带的敌方卡牌在布局这一步已经挂好；没有敌方卡牌时清掉默认画法留下的卡牌并还原容器布局。
    /// </summary>
    internal static IntentDecoratorOutcome OnDecorate(NCreature creatureNode, IEnumerable<Creature> targets)
    {
        try
        {
            Creature? creature = creatureNode.Entity;
            if (creature?.Monster == null)
            {
                return IntentDecoratorOutcome.Skipped;
            }

            IReadOnlyList<AbstractIntent> moveIntents = creature.Monster.NextMove.Intents;
            bool cardMove = moveIntents.Any(static intent => intent is IEnemyCardIntent);
            if (cardMove
                && creature.Monster is IEnemyCardRuntimeOwner owner
                && owner.EnemyCards is { } runtime)
            {
                IReadOnlyList<AbstractIntent> planIntents = EnemyCardIntentRuntimePatch
                    .CollectRuntimeDisplay(owner, runtime, moveIntents)
                    .Intents;
                if (planIntents.Count > 0)
                {
                    Render(
                        creatureNode,
                        VanillaIntentMapper.Map(planIntents, moveIntents),
                        targets as IReadOnlyList<Creature> ?? targets.ToArray(),
                        viewer: null);
                    return IntentDecoratorOutcome.Applied;
                }
            }
            else if (cardMove)
            {
                return IntentDecoratorOutcome.Applied;
            }

            EnemyCardIntentVisualNode.ClearCardsOnly(creatureNode.IntentContainer);
            RestoreNativeLayout(creatureNode.IntentContainer);
            return IntentDecoratorOutcome.Applied;
        }
        catch (Exception exception)
        {
            LorLog.PatchFailure("VanillaIntentDisplay.Decorate", exception);
            return IntentDecoratorOutcome.Failed;
        }
    }

    /// <summary>
    /// 重画意图节点。敌方卡牌照默认画法挂在意图上方（悬停放大由卡牌节点自己处理），一张牌拆成几个图标时只挂在第一个上；
    /// 挂了卡牌就用默认画法的卡牌布局（加宽间距给卡牌留位），否则还原原生布局。
    /// </summary>
    private static void Render(
        NCreature creatureNode,
        IReadOnlyList<VanillaIntentPart> parts,
        IReadOnlyList<Creature> targets,
        Creature? viewer)
    {
        Control container = creatureNode.IntentContainer;
        float startOffset = creatureNode.GetHashCode() / 100f;

        for (int i = container.GetChildCount(); i < parts.Count; i++)
        {
            container.AddChildSafely(NIntent.Create(startOffset + i * 0.3f));
        }

        var cardSources = new HashSet<AbstractIntent>(ReferenceEqualityComparer.Instance);
        int attachedCards = 0;
        for (int i = 0; i < parts.Count; i++)
        {
            NIntent child = container.GetChild<NIntent>(i);
            child.Modulate = Colors.White;
            child.SetFrozen(isFrozen: false);
            IEnumerable<Creature> intentTargets = viewer != null
                ? CounterIntentAppendPatch.ResolveIntentTargets(parts[i].Source, targets, viewer)
                : targets;
            child.UpdateIntent(parts[i].Display, intentTargets, creatureNode.Entity);

            AbstractIntent source = parts[i].Source;
            if (source is IEnemyCardIntent enemyCard
                && cardSources.Add(source)
                && EnemyCardIntentVisualNode.Attach(child, enemyCard.EnemyCard, source, intentTargets, creatureNode.Entity!))
            {
                attachedCards++;
            }
            else
            {
                EnemyCardIntentVisualNode.ClearIntentNode(child);
            }
        }

        foreach (Node extra in container.GetChildren().TakeLast(container.GetChildCount() - parts.Count).ToArray())
        {
            container.RemoveChildSafely(extra);
            extra.QueueFreeSafely();
        }

        if (attachedCards > 0)
        {
            EnemyCardIntentVisualNode.ResetContainerLayout(creatureNode, parts.Count);
        }
        else
        {
            RestoreNativeLayout(container);
        }
    }

    private static void RestoreNativeLayout(Control container)
    {
        if (NativeLayouts.TryGetValue(container, out NativeContainerLayout? native) && !native.Matches(container))
        {
            native.Apply(container);
        }
    }

    private sealed class NativeContainerLayout
    {
        private const string SeparationKey = "separation";

        private bool _hasSeparation;
        private int _separation;
        private BoxContainer.AlignmentMode? _alignment;
        private Vector2 _minimumSize;
        private Vector2 _size;
        private Vector2 _pivot;
        private Vector2 _position;

        internal static NativeContainerLayout Capture(Control container)
        {
            var layout = new NativeContainerLayout
            {
                _minimumSize = container.CustomMinimumSize,
                _size = container.Size,
                _pivot = container.PivotOffset,
                _position = container.Position
            };
            if (container is BoxContainer box)
            {
                layout._hasSeparation = box.HasThemeConstantOverride(SeparationKey);
                layout._separation = layout._hasSeparation ? box.GetThemeConstant(SeparationKey) : 0;
                layout._alignment = box.Alignment;
            }

            return layout;
        }

        // 只比较默认画法的运行时出牌会改的布局项；尺寸与位置由原版按子节点与包围盒维护，相同时不去动它。
        internal bool Matches(Control container)
        {
            if (container.CustomMinimumSize != _minimumSize)
            {
                return false;
            }

            if (container is not BoxContainer box)
            {
                return true;
            }

            bool hasSeparation = box.HasThemeConstantOverride(SeparationKey);
            return hasSeparation == _hasSeparation
                   && (!hasSeparation || box.GetThemeConstant(SeparationKey) == _separation)
                   && box.Alignment == _alignment;
        }

        internal void Apply(Control container)
        {
            if (container is BoxContainer box)
            {
                if (_hasSeparation)
                {
                    box.AddThemeConstantOverride(SeparationKey, _separation);
                }
                else
                {
                    box.RemoveThemeConstantOverride(SeparationKey);
                }

                if (_alignment.HasValue)
                {
                    box.Alignment = _alignment.Value;
                }
            }

            container.CustomMinimumSize = _minimumSize;
            container.Size = _size;
            container.PivotOffset = _pivot;
            container.Position = _position;
        }
    }
}
