using System;
using System.Linq;
using Godot;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.intents.rendering;
using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace LibraryOfRuina.ui;

internal interface IEnemyCardRuntimeOwner
{
    EnemyCardRuntime? EnemyCards { get; }

    IReadOnlyList<AbstractIntent> CreateIntentSequenceForCard(EnemyCardSpec card) =>
        [card.CreateIntentInstance()];
}

internal static class EnemyCardIntentRuntimePatch
{
    internal static IntentDecoratorOutcome OnUpdateIntent(NCreature __instance, IEnumerable<Creature> targets)
    {
        try
        {
            Creature? creature = __instance.Entity;
            if (creature?.Monster == null)
            {
                return IntentDecoratorOutcome.Skipped;
            }

            if (creature.Monster is IEnemyCardRuntimeOwner owner
                && owner.EnemyCards is { } runtime)
            {
                RenderRuntimeIntents(__instance, targets, creature, owner, runtime);
                return IntentDecoratorOutcome.Applied;
            }

            RenderMoveIntents(__instance, targets, creature);
            return IntentDecoratorOutcome.Applied;
        }
        catch (Exception ex)
        {
            Log.Warn("[LibraryOfRuina.EnemyCards] Enemy card intent refresh failed: " + ex);
            return IntentDecoratorOutcome.Failed;
        }
    }

    private static void RenderRuntimeIntents(
        NCreature creatureNode,
        IEnumerable<Creature> targets,
        Creature creature,
        IEnemyCardRuntimeOwner owner,
        EnemyCardRuntime runtime)
    {
        IReadOnlyList<AbstractIntent> moveIntents = creature.Monster!.NextMove.Intents;
        if (moveIntents.All(static intent => intent is not IEnemyCardIntent))
        {
            // Stun and other native move replacements take precedence over the cached card plan.
            EnemyCardIntentVisualNode.ClearCardsOnly(creatureNode.IntentContainer);
            EnemyCardIntentVisualNode.ResetContainerLayout(creatureNode, moveIntents.Count);
            return;
        }

        (int firstVisiblePlanIndex, IReadOnlyList<EnemyCardSpec> displayCards, IReadOnlyList<AbstractIntent> displayIntents) =
            CollectRuntimeDisplay(owner, runtime, moveIntents);
        if (displayIntents.Count == 0)
        {
            EnemyCardIntentVisualNode.ClearContainer(creatureNode.IntentContainer);
            EnemyCardIntentVisualNode.ResetContainerLayout(creatureNode, visibleIntentCount: 0);
            return;
        }

        int attachedCount = RenderIntentNodes(creatureNode, displayIntents, targets, creature, rewriteIntentNodes: true);
        EnemyCardIntentVisualNode.ResetContainerLayout(creatureNode, displayIntents.Count);

        string displaySignature = runtime.GetHashCode()
                                  + ":"
                                  + firstVisiblePlanIndex
                                  + ":"
                                  + string.Join(",", displayCards.Select(static card => card.Id))
                                  + ":"
                                  + attachedCount
                                  + "/"
                                  + displayIntents.Count;
        // 布局日志只在 Debug 下按签名去重：签名含运行时实例与计划下标，每回合都会变，
        // 常开时每个敌人每回合一条，去重表也会一直增长。RenderMoveIntents 同理。
        if (displayCards.Count > 0 && LorLog.IsDebugEnabled && LorLog.FirstTime("EnemyCards.RuntimeIntents:" + displaySignature))
        {
            LorLog.Debug("[LibraryOfRuina.EnemyCards] Attached enemy cards to runtime intent nodes: owner="
                     + creature.Monster!.Id.Entry
                     + " cards="
                     + string.Join(",", displayCards.Select(static card => card.Id))
                     + " intentNodes="
                     + displayIntents.Count
                     + " attached="
                     + attachedCount
                     + " handLayer=hidden"
                     + BuildLayoutLogSuffix(creatureNode.IntentContainer));
        }
    }

    /// <summary>
    /// 运行时出牌计划里还没打出的牌，按出牌顺序展开成意图。两种画法共用：原版风格画法把这些意图再映射成原版意图，
    /// 同样把卡牌挂在意图上方。计划为空时退回招式里的敌方卡牌意图。
    /// </summary>
    internal static (int FirstVisiblePlanIndex, IReadOnlyList<EnemyCardSpec> Cards, IReadOnlyList<AbstractIntent> Intents)
        CollectRuntimeDisplay(
            IEnemyCardRuntimeOwner owner,
            EnemyCardRuntime runtime,
            IReadOnlyList<AbstractIntent> moveIntents)
    {
        int firstVisiblePlanIndex = runtime.CurrentPlanIndex + (runtime.CardInUse == null ? 0 : 1);
        IReadOnlyList<EnemyCardSpec> displayCards = runtime.CurrentPlan.Count > 0
            ? runtime.CurrentPlan.Skip(firstVisiblePlanIndex).ToArray()
            : [];
        IReadOnlyList<AbstractIntent> displayIntents = runtime.CurrentPlan.Count > 0
            ? displayCards.SelectMany(owner.CreateIntentSequenceForCard).ToArray()
            : moveIntents.Where(static intent => intent is IEnemyCardIntent).ToArray();
        return (firstVisiblePlanIndex, displayCards, displayIntents);
    }

    private static void RenderMoveIntents(
        NCreature creatureNode,
        IEnumerable<Creature> targets,
        Creature creature)
    {
        IReadOnlyList<AbstractIntent> displayIntents = creature.Monster!.NextMove.Intents;
        if (displayIntents.All(static intent => intent is not IEnemyCardIntent))
        {
            EnemyCardIntentVisualNode.ClearCardsOnly(creatureNode.IntentContainer);
            return;
        }

        int attachedCount = RenderIntentNodes(creatureNode, displayIntents, targets, creature, rewriteIntentNodes: false);

        string displaySignature = creature.GetHashCode()
                                  + ":"
                                  + creature.Monster.NextMove.StateId
                                  + ":"
                                  + string.Join(",", displayIntents.OfType<IEnemyCardIntent>().Select(static intent => intent.EnemyCard.Id))
                                  + ":"
                                  + attachedCount
                                  + "/"
                                  + displayIntents.Count;
        if (LorLog.IsDebugEnabled && LorLog.FirstTime("EnemyCards.MoveIntents:" + displaySignature))
        {
            LorLog.Debug("[LibraryOfRuina.EnemyCards] Attached enemy cards to move intent nodes: owner="
                     + creature.Monster.Id.Entry
                     + " move="
                     + creature.Monster.NextMove.StateId
                     + " cards="
                     + string.Join(",", displayIntents.OfType<IEnemyCardIntent>().Select(static intent => intent.EnemyCard.Id))
                     + " intentNodes="
                     + displayIntents.Count
                     + " attached="
                     + attachedCount
                     + BuildLayoutLogSuffix(creatureNode.IntentContainer));
        }
    }

    private static int RenderIntentNodes(
        NCreature creatureNode,
        IReadOnlyList<AbstractIntent> displayIntents,
        IEnumerable<Creature> targets,
        Creature creature,
        bool rewriteIntentNodes)
    {
        Control container = creatureNode.IntentContainer;
        int requiredCount = displayIntents.Count;
        float startOffset = creatureNode.GetHashCode() / 100f;
        IReadOnlyList<Creature> targetList = targets as IReadOnlyList<Creature> ?? targets.ToArray();

        for (int i = container.GetChildCount(); i < requiredCount; i++)
        {
            container.AddChildSafely(NIntent.Create(startOffset + i * 0.3f));
        }

        int attachedCount = 0;
        for (int i = 0; i < displayIntents.Count; i++)
        {
            NIntent child = container.GetChild<NIntent>(i);
            child.SetFrozen(isFrozen: false);
            if (rewriteIntentNodes)
            {
                child.UpdateIntent(displayIntents[i], targetList, creature);
            }

            if (displayIntents[i] is IEnemyCardIntent enemyCardIntent)
            {
                if (EnemyCardIntentVisualNode.Attach(
                    child,
                    enemyCardIntent.EnemyCard,
                    displayIntents[i],
                    targetList,
                    creature))
                {
                    attachedCount++;
                }
            }
            else
            {
                EnemyCardIntentVisualNode.ClearIntentNode(child);
            }
        }

        foreach (Node extra in container.GetChildren().TakeLast(container.GetChildCount() - requiredCount).ToArray())
        {
            container.RemoveChildSafely(extra);
            extra.QueueFreeSafely();
        }

        return attachedCount;
    }

    private static string BuildLayoutLogSuffix(Control container)
    {
        try
        {
            NIntent? firstIntent = container.GetChildren().OfType<NIntent>().FirstOrDefault();
            if (firstIntent == null || !firstIntent.HasNode("%IntentHolder"))
            {
                return "";
            }

            Control holder = firstIntent.GetNode<Control>("%IntentHolder");
            EnemyCardIntentVisualNode? card = holder.GetChildren().OfType<EnemyCardIntentVisualNode>().FirstOrDefault();
            return " containerGlobal="
                   + FormatVector(container.GlobalPosition)
                   + " containerSize="
                   + FormatVector(container.Size)
                   + " firstIntentGlobal="
                   + FormatVector(firstIntent.GlobalPosition)
                   + " firstCardGlobal="
                   + (card == null ? "none" : FormatVector(card.GlobalPosition));
        }
        catch (Exception ex)
        {
            return " layoutLogFailed=" + ex.GetType().Name;
        }
    }

    private static string FormatVector(Vector2 value)
    {
        return "(" + value.X.ToString("0.0") + "," + value.Y.ToString("0.0") + ")";
    }
}

internal sealed partial class EnemyCardIntentVisualNode : Control
{
    private const string CardNodePrefix = "LibraryOfRuinaEnemyCardIntentVisual";
    private const string CardScenePath = "res://scenes/cards/card.tscn";
    private const float IntentIconSize = 64f;
    private const float IntentSeparation = 64f;
    private const float CardScale = 0.38f;
    private const float CardHoverScale = CardScale * 0.7f / 0.45f;
    private const float CardIntentGap = -20f;
    private const float HoverHitboxHorizontalInset = 12f;
    private const float HoverHitboxTopInset = 8f;
    private const float HoverHitboxBottomInset = 12f;
    private const float HoverIntentClearance = 8f;
    private static readonly Vector2 DefaultIntentSize = new(IntentIconSize, IntentIconSize);
    private static readonly Vector2 CardSize = NCard.defaultSize * CardScale;

    public const float IntentSlotWidth = IntentIconSize;
    public const float IntentSlotHeight = IntentIconSize;

    public string CardId { get; }

    // 同一张牌的意图刷新时复用节点，只换绑定（见 Rebind），所以这几个不是只读
    private EnemyCardSpec _spec;
    private AbstractIntent _intent;
    private IReadOnlyList<Creature> _targets;
    private Creature _owner;
    private readonly NCard _card;
    private bool _isHovered;

    private EnemyCardIntentVisualNode(
        EnemyCardSpec spec,
        AbstractIntent intent,
        IEnumerable<Creature> targets,
        Creature owner)
    {
        _spec = spec;
        _intent = intent;
        _targets = targets as IReadOnlyList<Creature> ?? targets.ToArray();
        _owner = owner;
        CardId = spec.Id;
        Name = CardNodePrefix;
        Size = CardSize;
        CustomMinimumSize = CardSize;
        MouseFilter = MouseFilterEnum.Ignore;
        ZIndex = 10;
        ZAsRelative = true;
        ClipContents = false;

        _card = CreateUnpooledCard(spec, intent, _targets, owner);
        _card.Name = "EnemyIntentCard";
        _card.Scale = Vector2.One * CardScale;
        _card.Position = CardSize * 0.5f;
        _card.MouseFilter = MouseFilterEnum.Ignore;
        this.AddChildSafely(_card);
        WireCardVisuals(_card, spec.Id);
    }

    /// <summary>同一张牌的意图刷新：换上新的显示用卡牌数据并重画，节点和卡牌场景沿用。</summary>
    private void Rebind(EnemyCardSpec spec, AbstractIntent intent, IEnumerable<Creature> targets, Creature owner)
    {
        _spec = spec;
        _intent = intent;
        _targets = targets as IReadOnlyList<Creature> ?? targets.ToArray();
        _owner = owner;
        _card.Model = spec.CreateDisplayCardForIntent(intent, _targets, owner);
        WireCardVisuals(_card, spec.Id);
    }

    public override void _Process(double delta)
    {
        Viewport? viewport = GetViewport();
        Control? container = GetIntentContainer();
        EnemyCardIntentVisualNode? hoveredVisual = IsVisibleInTree() && viewport != null && container != null
            ? ResolveHoveredVisual(container, viewport.GetMousePosition())
            : null;
        bool hoveredNow = ReferenceEquals(hoveredVisual, this);
        if (hoveredNow == _isHovered)
        {
            return;
        }

        if (hoveredNow)
        {
            OnCardMouseEntered();
        }
        else
        {
            OnCardMouseExited();
        }
    }

    public static bool Attach(
        NIntent intentNode,
        EnemyCardSpec spec,
        AbstractIntent intent,
        IEnumerable<Creature> targets,
        Creature owner)
    {
        try
        {
            if (!intentNode.HasNode("%IntentHolder"))
            {
                return false;
            }

            Control holder = intentNode.GetNode<Control>("%IntentHolder");
            // 原版每次加减能力都会刷新意图：同一张牌只换卡牌数据（伤害数值可能变了），不再删掉重建整张卡牌场景
            EnemyCardIntentVisualNode? reusable = holder.GetChildren().OfType<EnemyCardIntentVisualNode>()
                .FirstOrDefault(visual => visual.CardId == spec.Id && IsInstanceValid(visual) && !visual.IsQueuedForDeletion());
            RemoveExisting(holder, keep: reusable);
            ConfigureNativeIntentSlot(intentNode, holder);
            if (reusable != null)
            {
                reusable.Rebind(spec, intent, targets, owner);
                reusable.Position = GetFloatingCardTopLeft();
                return true;
            }

            var cardVisual = new EnemyCardIntentVisualNode(spec, intent, targets, owner);
            cardVisual.Position = GetFloatingCardTopLeft();
            holder.AddChildSafely(cardVisual);
            return true;
        }
        catch (Exception ex)
        {
            Log.Warn("[LibraryOfRuina.EnemyCards] Enemy card intent visual failed for " + spec.Id + ": " + ex);
            return false;
        }
    }

    public static void ClearContainer(Control container)
    {
        foreach (NIntent intentNode in container.GetChildren().OfType<NIntent>())
        {
            Clear(intentNode);
        }

        ResetContainerLayout(container);
    }

    public static void ResetContainerLayout(Control container)
    {
        if (container is HBoxContainer hBox)
        {
            hBox.AddThemeConstantOverride("separation", (int)IntentSeparation);
            hBox.Alignment = BoxContainer.AlignmentMode.Center;
        }

        ApplyCardHoverLayout(container);
    }

    public static void ClearCardsOnly(Control container)
    {
        foreach (NIntent intentNode in container.GetChildren().OfType<NIntent>())
        {
            Clear(intentNode);
        }
    }

    public static void ClearIntentNode(NIntent intentNode)
    {
        Clear(intentNode);
    }

    public static void ResetContainerLayout(NCreature creatureNode, int visibleIntentCount)
    {
        Control container = creatureNode.IntentContainer;
        int normalizedCount = Math.Max(visibleIntentCount, 1);
        if (container is HBoxContainer hBox)
        {
            hBox.AddThemeConstantOverride("separation", (int)IntentSeparation);
            hBox.Alignment = BoxContainer.AlignmentMode.Begin;
        }

        Vector2 compactSize = new(
            IntentIconSize + Math.Max(0, normalizedCount - 1) * (IntentIconSize + IntentSeparation),
            IntentIconSize);
        container.Size = compactSize;
        container.CustomMinimumSize = compactSize;
        container.PivotOffset = compactSize * 0.5f;
        container.Position = creatureNode.Visuals.IntentPosition.Position
                             - new Vector2(compactSize.X * 0.5f, DefaultIntentSize.Y * 0.5f);

        ApplyCardHoverLayout(container);
    }

    private static void Clear(NIntent intentNode)
    {
        if (!intentNode.HasNode("%IntentHolder"))
        {
            return;
        }

        Control holder = intentNode.GetNode<Control>("%IntentHolder");
        RemoveExisting(holder);
        ResetNativeIntentSlot(intentNode, holder);
    }

    private static void ConfigureNativeIntentSlot(NIntent intentNode, Control holder)
    {
        intentNode.CustomMinimumSize = DefaultIntentSize;
        intentNode.Size = DefaultIntentSize;
        intentNode.PivotOffset = DefaultIntentSize * 0.5f;

        holder.CustomMinimumSize = DefaultIntentSize;
        holder.Size = DefaultIntentSize;
        holder.PivotOffset = DefaultIntentSize * 0.5f;
        holder.MouseFilter = MouseFilterEnum.Ignore;

        if (intentNode.GetNodeOrNull<Sprite2D>("%Intent") is { } sprite)
        {
            sprite.Visible = true;
            sprite.Position = new Vector2(30f, 31f);
            sprite.Scale = Vector2.One;
            sprite.ZIndex = 0;
            sprite.ZAsRelative = true;
        }

        if (intentNode.GetNodeOrNull<CpuParticles2D>("%IntentParticle") is { } particle)
        {
            particle.Position = new Vector2(32f, 32f);
            particle.Scale = Vector2.One;
            particle.ZIndex = 0;
            particle.ZAsRelative = true;
        }

        if (intentNode.GetNodeOrNull<RichTextLabel>("%Value") is { } value)
        {
            value.Visible = true;
            value.AnchorLeft = 0f;
            value.AnchorTop = 0f;
            value.AnchorRight = 1f;
            value.AnchorBottom = 1f;
            value.OffsetLeft = 2f;
            value.OffsetTop = 40f;
            value.OffsetRight = 0f;
            value.OffsetBottom = -1f;
            value.AddThemeFontSizeOverride("normal_font_size", 22);
            value.AddThemeConstantOverride("outline_size", 12);
            value.ZIndex = 1;
            value.ZAsRelative = true;
        }
    }

    private static void ResetNativeIntentSlot(NIntent intentNode, Control holder)
    {
        intentNode.CustomMinimumSize = DefaultIntentSize;
        intentNode.Size = DefaultIntentSize;
        intentNode.PivotOffset = DefaultIntentSize * 0.5f;

        holder.CustomMinimumSize = DefaultIntentSize;
        holder.Size = DefaultIntentSize;
        holder.PivotOffset = DefaultIntentSize * 0.5f;

        if (intentNode.GetNodeOrNull<Sprite2D>("%Intent") is { } sprite)
        {
            sprite.Visible = true;
            sprite.Position = new Vector2(30f, 31f);
            sprite.Scale = Vector2.One;
            sprite.ZIndex = 0;
            sprite.ZAsRelative = true;
        }

        if (intentNode.GetNodeOrNull<CpuParticles2D>("%IntentParticle") is { } particle)
        {
            particle.Position = new Vector2(32f, 32f);
            particle.Scale = Vector2.One;
            particle.ZIndex = 0;
            particle.ZAsRelative = true;
        }

        if (intentNode.GetNodeOrNull<RichTextLabel>("%Value") is { } value)
        {
            value.Visible = true;
            value.AnchorLeft = 0f;
            value.AnchorTop = 0f;
            value.AnchorRight = 1f;
            value.AnchorBottom = 1f;
            value.OffsetLeft = 2f;
            value.OffsetTop = 40f;
            value.OffsetRight = 0f;
            value.OffsetBottom = -1f;
            value.AddThemeFontSizeOverride("normal_font_size", 22);
            value.AddThemeConstantOverride("outline_size", 12);
            value.ZIndex = 0;
            value.ZAsRelative = true;
        }
    }

    private static void RemoveExisting(Control holder, Node? keep = null)
    {
        foreach (Node child in holder.GetChildren().ToArray())
        {
            string name = child.Name.ToString();
            if (!ReferenceEquals(child, keep) && name.StartsWith(CardNodePrefix, StringComparison.Ordinal))
            {
                holder.RemoveChildSafely(child);
                child.QueueFreeSafely();
            }
        }
    }

    public static Vector2? GetNextCardGlobalCenter(EnemyCardRuntime runtime, EnemyCardSpec? nextCard = null)
    {
        try
        {
            NCreature? creatureNode = CombatQueries.CreatureNodeOf(runtime.Owner);
            if (creatureNode == null)
            {
                return null;
            }

            foreach (NIntent intentNode in creatureNode.IntentContainer.GetChildren().OfType<NIntent>())
            {
                if (nextCard != null
                    && !IntentNodeHasCard(intentNode, nextCard))
                {
                    continue;
                }

                if (!intentNode.HasNode("%IntentHolder"))
                {
                    continue;
                }

                Control holder = intentNode.GetNode<Control>("%IntentHolder");
                EnemyCardIntentVisualNode? visual = holder.GetChildren()
                    .OfType<EnemyCardIntentVisualNode>()
                    .FirstOrDefault();
                if (visual != null)
                {
                    return visual.GlobalPosition + visual.Size * 0.5f;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warn("[LibraryOfRuina.EnemyCards] Enemy intent-card play anchor lookup failed: " + ex);
        }

        return null;
    }

    private static bool IntentNodeHasCard(NIntent intentNode, EnemyCardSpec nextCard)
    {
        if (!intentNode.HasNode("%IntentHolder"))
        {
            return false;
        }

        Control holder = intentNode.GetNode<Control>("%IntentHolder");
        return holder.GetChildren()
            .OfType<EnemyCardIntentVisualNode>()
            .Any(visual => string.Equals(visual.CardId, nextCard.Id, StringComparison.Ordinal));
    }

    private void OnCardMouseEntered()
    {
        Control? container = GetIntentContainer();
        if (container != null)
        {
            foreach (EnemyCardIntentVisualNode visual in GetContainerVisuals(container))
            {
                visual._isHovered = ReferenceEquals(visual, this);
            }

            ApplyCardHoverLayout(container);
        }

        ShowCardHoverTips();
    }

    private void OnCardMouseExited()
    {
        _isHovered = false;
        Control? container = GetIntentContainer();
        if (container != null)
        {
            ApplyCardHoverLayout(container);
        }

        if (container == null || !GetContainerVisuals(container).Any(static visual => visual._isHovered))
        {
            CombatQueries.CreatureNodeOf(_owner)?.HideHoverTips();
        }
    }

    private void ShowCardHoverTips()
    {
        try
        {
            CardModel card = _spec.CreateDisplayCardForIntent(_intent, _targets, _owner);
            List<IHoverTip> hoverTips = [];
            if (_intent.HasIntentTip)
            {
                hoverTips.Add(_intent.GetHoverTip(_targets, _owner));
            }

            hoverTips.Add(HoverTipFactory.FromCard(card));
            hoverTips.AddRange(card.HoverTips);
            hoverTips = IHoverTip.RemoveDupes(hoverTips).ToList();

            CombatQueries.CreatureNodeOf(_owner)?.ShowHoverTips(hoverTips);
        }
        catch (Exception ex)
        {
            Log.Warn("[LibraryOfRuina.EnemyCards] Enemy intent card hover tips failed for " + CardId + ": " + ex);
        }
    }

    private Control? GetIntentContainer()
    {
        return GetParent()?.GetParent()?.GetParent() as Control;
    }

    private static IEnumerable<EnemyCardIntentVisualNode> GetContainerVisuals(Control container)
    {
        foreach (NIntent intentNode in container.GetChildren().OfType<NIntent>())
        {
            if (!intentNode.HasNode("%IntentHolder"))
            {
                continue;
            }

            Control holder = intentNode.GetNode<Control>("%IntentHolder");
            EnemyCardIntentVisualNode? visual = holder.GetChildren()
                .OfType<EnemyCardIntentVisualNode>()
                .FirstOrDefault();
            if (visual != null)
            {
                yield return visual;
            }
        }
    }

    private static void ApplyCardHoverLayout(Control container)
    {
        List<EnemyCardIntentVisualNode> visuals = GetContainerVisuals(container).ToList();
        int hoverIndex = visuals.FindIndex(static visual => visual._isHovered);
        float basePushDistance = CalculateBaseHoverPushDistance(visuals.Count);

        for (int index = 0; index < visuals.Count; index++)
        {
            EnemyCardIntentVisualNode visual = visuals[index];
            bool isHovered = index == hoverIndex;
            float horizontalOffset = 0f;
            if (hoverIndex >= 0 && !isHovered)
            {
                int distance = Math.Abs(index - hoverIndex);
                float direction = index > hoverIndex ? 1f : -1f;
                horizontalOffset = direction * basePushDistance * MathF.Pow(0.25f, distance - 1);
            }

            visual.ApplyVisualState(isHovered ? CardHoverScale : CardScale, horizontalOffset, isHovered);
        }
    }

    private static float CalculateBaseHoverPushDistance(int visualCount)
    {
        float hoverWidth = NCard.defaultSize.X * CardHoverScale;
        float baseWidth = NCard.defaultSize.X * CardScale;
        float intentCenterSpacing = IntentIconSize + IntentSeparation;
        float collisionPush = Math.Max(0f, (hoverWidth + baseWidth) * 0.5f - intentCenterSpacing + 8f);
        float downfallPushFactor = visualCount switch
        {
            2 => 0.20f,
            3 or 4 => 0.27f,
            _ => 0.40f
        };
        float downfallPush = NCard.defaultSize.X * downfallPushFactor * (CardScale / 0.75f);
        return Math.Max(collisionPush, downfallPush);
    }

    private void ApplyVisualState(float cardScale, float horizontalOffset, bool isHovered)
    {
        Vector2 scaledSize = NCard.defaultSize * cardScale;
        Size = scaledSize;
        CustomMinimumSize = scaledSize;
        PivotOffset = scaledSize * 0.5f;
        Position = GetFloatingCardTopLeft(scaledSize) + Vector2.Right * horizontalOffset;
        ZIndex = isHovered ? 80 : 10;
        _card.Scale = Vector2.One * cardScale;
        _card.Position = scaledSize * 0.5f;
        _card.Size = NCard.defaultSize;
        _card.CustomMinimumSize = NCard.defaultSize;
    }

    private Rect2 GetCardAcquireRect()
    {
        return GetCardHoverRect(CardScale);
    }

    private Rect2 GetCardRetainRect()
    {
        return GetCardHoverRect(Math.Max(CardScale, _card.Scale.X));
    }

    private Rect2 GetCardHoverRect(float hitboxScale)
    {
        Rect2 rect = GetCardFrameRect(hitboxScale);
        rect.Position += new Vector2(HoverHitboxHorizontalInset, HoverHitboxTopInset);
        rect.Size -= new Vector2(HoverHitboxHorizontalInset * 2f, HoverHitboxTopInset + HoverHitboxBottomInset);

        if (GetParent() is Control intentHolder)
        {
            float bottom = Math.Min(rect.Position.Y + rect.Size.Y, intentHolder.GlobalPosition.Y - HoverIntentClearance);
            rect.Size = new Vector2(rect.Size.X, Math.Max(0f, bottom - rect.Position.Y));
        }

        return rect;
    }

    private Rect2 GetCardFrameRect(float hitboxScale)
    {
        Vector2 scaledCardSize = NCard.defaultSize * hitboxScale;
        return new Rect2(_card.GlobalPosition - scaledCardSize * 0.5f, scaledCardSize);
    }

    private static EnemyCardIntentVisualNode? ResolveHoveredVisual(Control container, Vector2 mousePosition)
    {
        List<EnemyCardIntentVisualNode> visuals = GetContainerVisuals(container).ToList();
        EnemyCardIntentVisualNode? acquired = visuals
            .Where(visual => visual.GetCardAcquireRect().HasPoint(mousePosition))
            .OrderBy(visual => Math.Abs((visual.GlobalPosition.X + visual.Size.X * 0.5f) - mousePosition.X))
            .FirstOrDefault();
        if (acquired != null)
        {
            return acquired;
        }

        EnemyCardIntentVisualNode? current = visuals.FirstOrDefault(static visual => visual._isHovered);
        return current != null && current.GetCardRetainRect().HasPoint(mousePosition)
            ? current
            : null;
    }

    private static Vector2 GetFloatingCardTopLeft()
    {
        return new Vector2((IntentIconSize - CardSize.X) * 0.5f, -(CardSize.Y + CardIntentGap));
    }

    private static Vector2 GetFloatingCardTopLeft(Vector2 cardSize)
    {
        return new Vector2((IntentIconSize - cardSize.X) * 0.5f, -(cardSize.Y + CardIntentGap));
    }

    private static NCard CreateUnpooledCard(
        EnemyCardSpec spec,
        AbstractIntent intent,
        IEnumerable<Creature> targets,
        Creature owner)
    {
        CardModel card = spec.CreateDisplayCardForIntent(intent, targets, owner);
        PackedScene scene;
        try
        {
            scene = PreloadManager.Cache.GetScene(CardScenePath);
        }
        catch
        {
            scene = ResourceLoader.Load<PackedScene>(CardScenePath)
                    ?? throw new InvalidOperationException("Unable to load " + CardScenePath);
        }

        NCard cardNode = scene.Instantiate<NCard>();
        cardNode.Model = card;
        cardNode.Visibility = ModelVisibility.Visible;
        cardNode.Size = NCard.defaultSize;
        cardNode.CustomMinimumSize = NCard.defaultSize;
        return cardNode;
    }

    private static void WireCardVisuals(NCard card, string cardId)
    {
        if (card.IsNodeReady())
        {
            SafeUpdateCardVisuals(card, cardId);
            return;
        }

        card.Connect(Node.SignalName.Ready, Callable.From(() =>
        {
            SafeUpdateCardVisuals(card, cardId);
        }), flags: (uint)ConnectFlags.OneShot);
    }

    private static void SafeUpdateCardVisuals(NCard card, string cardId)
    {
        try
        {
            card.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
        }
        catch (Exception ex)
        {
            Log.Warn("[LibraryOfRuina.EnemyCards] Enemy intent card visual fallback for " + cardId + ": " + ex);
        }
    }
}
