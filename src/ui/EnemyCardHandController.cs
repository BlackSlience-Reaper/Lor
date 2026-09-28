using System;
using System.Linq;
using Godot;
using HarmonyLib;
using LibraryOfRuina.helpers;
using LibraryOfRuina.intents;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.Fonts;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace LibraryOfRuina.ui;

[HarmonyPatch(typeof(NCombatRoom), "OnCombatSetUp")]
internal static class EnemyCardHandControllerPatch
{
    private const string ControllerNodeName = "LibraryOfRuinaEnemyCardHandController";

    private static void Postfix(NCombatRoom __instance, CombatState state)
    {
        try
        {
            if (__instance.Ui.GetNodeOrNull<EnemyCardHandController>(ControllerNodeName) is { } existing)
            {
                existing.Visible = false;
                existing.QueueFreeSafely();
            }

            EnemyCardHandController.Active = null;
        }
        catch (Exception exception)
        {
            PatchFailureLog.Warn(
                "EnemyCardHandController.OnCombatSetUp",
                exception);
        }
    }
}

internal interface IEnemyCardRuntimeOwner
{
    EnemyCardRuntime? EnemyCards { get; }

    IReadOnlyList<AbstractIntent> CreateIntentSequenceForCard(EnemyCardSpec card) =>
        [card.CreateIntentInstance()];
}

internal static class EnemyCardIntentRuntimePatch
{
    private static readonly HashSet<string> LoggedRuntimeIntentRefreshes = [];
    private static readonly HashSet<string> LoggedMoveIntentRefreshes = [];

    internal static void OnUpdateIntent(NCreature __instance, IEnumerable<Creature> targets)
    {
        try
        {
            Creature? creature = __instance.Entity;
            if (creature?.Monster == null)
            {
                return;
            }

            if (creature.Monster is IEnemyCardRuntimeOwner owner
                && owner.EnemyCards is { } runtime)
            {
                RenderRuntimeIntents(__instance, targets, creature, owner, runtime);
                return;
            }

            RenderMoveIntents(__instance, targets, creature);
        }
        catch (Exception ex)
        {
            Log.Warn("[LibraryOfRuina.EnemyCards] Enemy card intent refresh failed: " + ex);
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

        int firstVisiblePlanIndex = runtime.CurrentPlanIndex + (runtime.CardInUse == null ? 0 : 1);
        IReadOnlyList<EnemyCardSpec> displayCards = runtime.CurrentPlan.Count > 0
            ? runtime.CurrentPlan.Skip(firstVisiblePlanIndex).ToArray()
            : [];
        IReadOnlyList<AbstractIntent> displayIntents = runtime.CurrentPlan.Count > 0
            ? displayCards.SelectMany(owner.CreateIntentSequenceForCard).ToArray()
            : moveIntents.Where(static intent => intent is IEnemyCardIntent).ToArray();
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
        if (displayCards.Count > 0 && LoggedRuntimeIntentRefreshes.Add(displaySignature))
        {
            Log.Info("[LibraryOfRuina.EnemyCards] Attached enemy cards to runtime intent nodes: owner="
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
        if (LoggedMoveIntentRefreshes.Add(displaySignature))
        {
            Log.Info("[LibraryOfRuina.EnemyCards] Attached enemy cards to move intent nodes: owner="
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

    private readonly EnemyCardSpec _spec;
    private readonly AbstractIntent _intent;
    private readonly IReadOnlyList<Creature> _targets;
    private readonly Creature _owner;
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
            RemoveExisting(holder);
            ConfigureNativeIntentSlot(intentNode, holder);
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

    private static void RemoveExisting(Control holder)
    {
        foreach (Node child in holder.GetChildren().ToArray())
        {
            string name = child.Name.ToString();
            if (name.StartsWith(CardNodePrefix, StringComparison.Ordinal))
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
            NCreature? creatureNode = NCombatRoom.Instance?.GetCreatureNode(runtime.Owner.Creature);
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
            NCombatRoom.Instance?.GetCreatureNode(_owner)?.HideHoverTips();
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

            NCombatRoom.Instance?.GetCreatureNode(_owner)?.ShowHoverTips(hoverTips);
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

internal static class EnemyCardIntentHoverPatch
{
    internal static bool OnIntentHovered(
        AbstractIntent ____intent,
        IEnumerable<Creature> ____targets,
        Creature ____owner)
    {
        return true;
    }
}

internal partial class EnemyCardHandController : Control
{
    private const int HandRowLength = 10;
    private const float CardScale = 0.40f;
    private const float CardNativeWidth = 300f;
    private const float CardNativeHeight = 422f;
    private const float CardSpacing = (CardNativeWidth + 115f) * CardScale;
    private const float RowSpacing = CardNativeHeight * CardScale;
    private const float EnergyOrbNativeSize = 128f;
    private const float EnergyVisualScale = 0.46f;
    private const float EnergySize = EnergyOrbNativeSize * EnergyVisualScale;
    private const float EnergyFixedRightOffset = 198f;
    private const float EnergyFixedTopOffset = 190f;
    private const float DownfallHandHeightRatio = 0.56f;
    private const float DownfallHandRightBaseRatio = 0.85f;
    private const float EnergyGap = 10f;
    private const string EnergyDarkMaterialPath = "res://materials/ui/energy_orb_dark.tres";
    private const string LabelFontPath = "res://themes/kreon_bold_glyph_space_one.tres";
    private const string EnergyLabelFontPath = "res://themes/kreon_bold_shared.tres";
    private const string IroncladOrbLayer1Path = "res://images/ui/combat/energy_counters/ironclad/ironclad_orb_layer_1.png";
    private const string IroncladOrbLayer2Path = "res://images/ui/combat/energy_counters/ironclad/ironclad_orb_layer_2.png";
    private const string IroncladOrbLayer3Path = "res://images/ui/combat/energy_counters/ironclad/ironclad_orb_layer_3.png";
    private const string IroncladOrbLayer4Path = "res://images/ui/combat/energy_counters/ironclad/ironclad_orb_layer_4.png";
    private const string IroncladOrbLayer5Path = "res://images/ui/combat/energy_counters/ironclad/ironclad_orb_layer_5.png";

    private readonly Dictionary<EnemyCardRuntime, RuntimeView> _views = [];
    private int _frameCounter;

    public static EnemyCardHandController? Active { get; set; }

    public EnemyCardHandController()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        TopLevel = true;
        Visible = false;
        ZIndex = 30;
        SetAnchorsPreset(LayoutPreset.FullRect);
    }

    public static void RefreshFor(EnemyCardRuntime runtime)
    {
        if (Active != null)
        {
            Active.Visible = false;
        }
    }

    public static Vector2? GetCardGlobalCenter(EnemyCardRuntime runtime, int handIndex)
    {
        return null;
    }

    public override void _ExitTree()
    {
        if (ReferenceEquals(Active, this))
        {
            Active = null;
        }
    }

    public override void _Process(double delta)
    {
        Visible = false;
    }

    public void RefreshAll()
    {
        Visible = false;
    }

    public void Refresh(EnemyCardRuntime runtime)
    {
        Visible = false;
    }

    private RuntimeView GetOrCreateView(EnemyCardRuntime runtime)
    {
        if (_views.TryGetValue(runtime, out RuntimeView? view))
        {
            return view;
        }

        view = new RuntimeView(runtime);
        _views[runtime] = view;
        this.AddChildSafely(view.Root);
        runtime.Changed += OnRuntimeChanged;
        return view;
    }

    private void OnRuntimeChanged(EnemyCardRuntime runtime)
    {
        Refresh(runtime);
    }

    private void RefreshAllPositions()
    {
        foreach ((EnemyCardRuntime runtime, RuntimeView view) in _views.ToArray())
        {
            RefreshPosition(runtime, view);
        }
    }

    private void RefreshPosition(EnemyCardRuntime runtime, RuntimeView view)
    {
        NCombatRoom? room = NCombatRoom.Instance;
        view.Root.Visible = false;
        if (room != null)
        {
            view.SetEnergyFixedPosition(room.GetViewportRect().Size, visible: false);
        }
    }

    private void RemoveDeadViews()
    {
        foreach ((EnemyCardRuntime runtime, RuntimeView view) in _views.ToArray())
        {
            if (runtime.Owner.Creature.IsDead)
            {
                runtime.Changed -= OnRuntimeChanged;
                _views.Remove(runtime);
                view.Root.QueueFreeSafely();
            }
        }
    }

    private static bool IsInstanceValid(Creature? creature)
    {
        return creature != null;
    }

    private static Label CreateLabel(string text, int fontSize, Color fontColor)
    {
        var label = new Label
        {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
            LabelSettings = new LabelSettings
            {
                Font = ResourceLoader.Load<Font>(LabelFontPath),
                FontSize = fontSize,
                FontColor = fontColor,
                OutlineSize = 6,
                OutlineColor = new Color(0f, 0f, 0f, 0.82f),
                ShadowSize = 2,
                ShadowColor = new Color(0f, 0f, 0f, 0.55f)
            }
        };
        label.ApplyLocaleFontSubstitution(FontType.Regular, new StringName("font"));
        return label;
    }

    private sealed class RuntimeView
    {
        private readonly EnemyCardRuntime _runtime;
        private readonly Control _cards;
        private readonly EnemyEnergyOrbView _energyBadge;
        private readonly List<Control> _cardNodes = [];
        private string? _lastHandSignature;

        public RuntimeView(EnemyCardRuntime runtime)
        {
            _runtime = runtime;
            Root = BuildRoot(out _energyBadge, out _cards);
        }

        public Control Root { get; }

        public void Refresh()
        {
            _energyBadge.Refresh(_runtime.Energy, _runtime.MasterEnergy);
            RebuildCards();
            _lastHandSignature = BuildHandSignature();
            LayoutCards();
        }

        public Vector2? GetCardGlobalCenter(int handIndex)
        {
            if (handIndex < 0 || handIndex >= _cardNodes.Count)
            {
                return null;
            }

            Control card = _cardNodes[handIndex];
            Rect2 rect = card.GetGlobalRect();
            return rect.Position + rect.Size * 0.5f;
        }

        public void SetEnergyFixedPosition(Vector2 viewportSize, bool visible)
        {
            float uiScale = Math.Min(
                Math.Max(0.1f, viewportSize.X / 1920f),
                Math.Max(0.1f, viewportSize.Y / 1080f));
            Vector2 center = new(
                viewportSize.X - EnergyFixedRightOffset * uiScale,
                EnergyFixedTopOffset * uiScale);
            _energyBadge.GlobalPosition = center - _energyBadge.Size * 0.5f;
            _energyBadge.Visible = visible;
        }

        private static Control BuildRoot(
            out EnemyEnergyOrbView energyBadge,
            out Control cards)
        {
            var root = new Control
            {
                MouseFilter = MouseFilterEnum.Ignore
            };

            energyBadge = new EnemyEnergyOrbView();
            energyBadge.TopLevel = true;
            root.AddChildSafely(energyBadge);

            cards = new Control
            {
                MouseFilter = MouseFilterEnum.Ignore
            };
            root.AddChildSafely(cards);

            return root;
        }

        private void RebuildCards()
        {
            ClearCards();

            IReadOnlyList<EnemyCardSpec> hand = _runtime.Hand.Cards;
            for (int index = 0; index < hand.Count; index++)
            {
                EnemyCardSpec spec = hand[index];
                Control? card = BuildHandCard(spec, index == 0);
                if (card == null)
                {
                    continue;
                }

                _cards.AddChildSafely(card);
                _cardNodes.Add(card);
            }
        }

        private void ClearCards()
        {
            foreach (Control node in _cardNodes.ToArray())
            {
                node.GetParent()?.RemoveChildSafely(node);
                node.QueueFreeSafely();
            }

            _cardNodes.Clear();
        }

        private void LayoutCards()
        {
            int cardCount = _runtime.Hand.Count;
            int cardsInRow = Math.Min(Math.Max(cardCount, 1), HandRowLength);
            int rows = Math.Max(1, (cardCount + HandRowLength - 1) / HandRowLength);
            float cardWidth = NCard.defaultSize.X * CardScale;
            float cardHeight = NCard.defaultSize.Y * CardScale;
            float handWidth = cardCount == 0
                ? 1f
                : cardWidth + Math.Max(0, cardsInRow - 1) * CardSpacing;
            float handHeight = cardCount == 0
                ? 1f
                : cardHeight + Math.Max(0, rows - 1) * RowSpacing;

            Root.Size = new Vector2(handWidth, handHeight);
            Root.CustomMinimumSize = Root.Size;
            _cards.Position = Vector2.Zero;
            _cards.Size = new Vector2(handWidth, handHeight);
            _cards.Visible = cardCount > 0;

            for (int i = 0; i < _cardNodes.Count; i++)
            {
                int row = i / HandRowLength;
                int col = i % HandRowLength;
                _cardNodes[i].Position = new Vector2(col * CardSpacing, row * RowSpacing);
                _cardNodes[i].RotationDegrees = 0f;
            }
        }

        private string BuildHandSignature()
        {
            return string.Join("|", _runtime.Hand.Cards.Select(static card => card.Id))
                   + ";use=" + (_runtime.CardInUse?.Id ?? "");
        }

        private static Control? BuildHandCard(EnemyCardSpec spec, bool isNext)
        {
            try
            {
                CardModel model = spec.CreateDisplayCard();
                NCard? card = NCard.Create(model);
                if (card == null)
                {
                    return EnemyCardFallbackCardView.Create(spec, isNext);
                }

                card.MouseFilter = MouseFilterEnum.Ignore;
                card.Scale = Vector2.One * CardScale;
                card.Size = NCard.defaultSize;
                card.CustomMinimumSize = NCard.defaultSize;
                WireHandCardVisuals(card, spec.Id, isNext);
                return card;
            }
            catch (Exception ex)
            {
                Log.Warn("[LibraryOfRuina.EnemyCards] Enemy hand native card failed for " + spec.Id + ": " + ex);
                return EnemyCardFallbackCardView.Create(spec, isNext);
            }
        }

        private static void SafeSetCardHighlight(NCard card, bool isNext)
        {
            try
            {
                if (card.CardHighlight == null)
                {
                    return;
                }

                card.CardHighlight.Modulate = NCardHighlight.gold;
                if (isNext)
                {
                    card.CardHighlight.AnimShow();
                }
                else
                {
                    card.CardHighlight.AnimHideInstantly();
                }
            }
            catch (Exception ex)
            {
                Log.Warn("[LibraryOfRuina.EnemyCards] Enemy hand highlight skipped: " + ex);
            }
        }

        private static void WireHandCardVisuals(NCard card, string cardId, bool isNext)
        {
            if (card.IsNodeReady())
            {
                SafeUpdateCardVisuals(card, cardId);
                SafeSetCardHighlight(card, isNext);
                return;
            }

            card.Connect(Node.SignalName.Ready, Callable.From(() =>
            {
                SafeUpdateCardVisuals(card, cardId);
                SafeSetCardHighlight(card, isNext);
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
                Log.Warn("[LibraryOfRuina.EnemyCards] Enemy hand card visual fallback for " + cardId + ": " + ex);
            }
        }
    }

    private sealed partial class EnemyEnergyOrbView : Control
    {
        private readonly Label _label;
        private readonly Control _layers;
        private readonly Control _rotationLayers;
        private readonly TextureRect _flash;
        private readonly Control _visualRoot;
        private int _lastEnergy = -1;

        public EnemyEnergyOrbView()
        {
            Size = new Vector2(EnergySize, EnergySize);
            CustomMinimumSize = Size;
            MouseFilter = MouseFilterEnum.Ignore;

            _visualRoot = new Control
            {
                Size = new Vector2(EnergyOrbNativeSize, EnergyOrbNativeSize),
                CustomMinimumSize = new Vector2(EnergyOrbNativeSize, EnergyOrbNativeSize),
                Scale = Vector2.One * EnergyVisualScale,
                MouseFilter = MouseFilterEnum.Ignore
            };
            this.AddChildSafely(_visualRoot);

            _layers = BuildIroncladOrbLayers(out _rotationLayers, out _flash);
            _visualRoot.AddChildSafely(_layers);

            _label = CreateEnergyLabel("0/0");
            this.AddChildSafely(_label);
        }

        public override void _Process(double delta)
        {
            float speed = _lastEnergy == 0 ? 5f : 30f;
            for (int i = 0; i < _rotationLayers.GetChildCount(); i++)
            {
                if (_rotationLayers.GetChild(i) is Control child)
                {
                    child.RotationDegrees += (float)delta * speed * (i + 1);
                }
            }
        }

        public void Refresh(int energy, int masterEnergy)
        {
            _label.Text = $"{energy}/{masterEnergy}";
            if (_label.LabelSettings != null)
            {
                _label.LabelSettings.FontColor = energy == 0 ? StsColors.red : StsColors.cream;
                _label.LabelSettings.OutlineColor = energy == 0
                    ? StsColors.unplayableEnergyCostOutline
                    : new Color("801212FF");
            }

            Material? material = energy == 0 ? ResourceLoader.Load<Material>(EnergyDarkMaterialPath) : null;
            ApplyEnergyMaterial(_layers, material);
            _layers.Modulate = energy == 0 ? Colors.DarkGray : Colors.White;

            if (_lastEnergy >= 0 && energy > _lastEnergy)
            {
                PlayRechargeFlash();
            }

            _lastEnergy = energy;
        }

        private static Control BuildIroncladOrbLayers(out Control rotationLayers, out TextureRect flash)
        {
            var layers = new Control
            {
                Name = "Layers",
                Size = new Vector2(EnergyOrbNativeSize, EnergyOrbNativeSize),
                CustomMinimumSize = new Vector2(EnergyOrbNativeSize, EnergyOrbNativeSize),
                MouseFilter = MouseFilterEnum.Ignore
            };

            layers.AddChildSafely(CreateOrbLayer("Layer1", IroncladOrbLayer1Path));
            rotationLayers = new Control
            {
                Name = "RotationLayers",
                Size = new Vector2(EnergyOrbNativeSize, EnergyOrbNativeSize),
                CustomMinimumSize = new Vector2(EnergyOrbNativeSize, EnergyOrbNativeSize),
                MouseFilter = MouseFilterEnum.Ignore
            };
            rotationLayers.AddChildSafely(CreateOrbLayer("Layer2", IroncladOrbLayer2Path, rotates: true));
            rotationLayers.AddChildSafely(CreateOrbLayer("Layer3", IroncladOrbLayer3Path, rotates: true));
            layers.AddChildSafely(rotationLayers);
            layers.AddChildSafely(CreateOrbLayer("Layer4", IroncladOrbLayer4Path));
            layers.AddChildSafely(CreateOrbLayer("Layer5", IroncladOrbLayer5Path));

            flash = CreateOrbLayer("RechargeFlash", IroncladOrbLayer5Path);
            flash.Modulate = new Color(1f, 0.78f, 0.35f, 0f);
            layers.AddChildSafely(flash);
            return layers;
        }

        private static TextureRect CreateOrbLayer(string name, string texturePath, bool rotates = false)
        {
            var layer = new TextureRect
            {
                Name = name,
                Texture = ResourceLoader.Load<Texture2D>(texturePath),
                Size = new Vector2(EnergyOrbNativeSize, EnergyOrbNativeSize),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Scale,
                MouseFilter = MouseFilterEnum.Ignore
            };

            if (rotates)
            {
                layer.PivotOffset = layer.Size * 0.5f;
            }

            return layer;
        }

        private static Label CreateEnergyLabel(string text)
        {
            var label = new Label
            {
                Text = text,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Position = new Vector2(5f, 6f),
                Size = new Vector2(EnergySize, EnergySize),
                CustomMinimumSize = new Vector2(EnergySize, EnergySize),
                ZIndex = 10,
                MouseFilter = MouseFilterEnum.Ignore,
                LabelSettings = new LabelSettings
                {
                    Font = ResourceLoader.Load<Font>(EnergyLabelFontPath),
                    FontSize = 39,
                    FontColor = StsColors.cream,
                    OutlineSize = 9,
                    OutlineColor = new Color("801212FF"),
                    ShadowSize = 2,
                    ShadowColor = new Color(0f, 0f, 0f, 0.19f)
                }
            };
            label.AddThemeConstantOverride("shadow_offset_x", 3);
            label.AddThemeConstantOverride("shadow_offset_y", 2);
            label.ApplyLocaleFontSubstitution(FontType.Regular, new StringName("font"));
            return label;
        }

        private void PlayRechargeFlash()
        {
            _flash.Modulate = new Color(1f, 0.78f, 0.35f, 0.55f);
            Tween tween = _flash.CreateTween();
            tween.TweenProperty(_flash, "modulate:a", 0f, 0.45)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Cubic);
        }

        private static TextureRect BuildFallbackOrb()
        {
            var style = new GradientTexture2D
            {
                Width = 128,
                Height = 128,
                Fill = GradientTexture2D.FillEnum.Radial,
                Gradient = new Gradient
                {
                    Colors = new[] { new Color("B74D3CFF"), new Color("421018FF") },
                    Offsets = new[] { 0f, 1f }
                }
            };

            return new TextureRect
            {
                Size = new Vector2(EnergyOrbNativeSize, EnergyOrbNativeSize),
                Texture = style,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = MouseFilterEnum.Ignore
            };
        }

        private static void ApplyEnergyMaterial(Control? node, Material? material)
        {
            if (node == null)
            {
                return;
            }

            foreach (Control child in node.GetChildren().OfType<Control>())
            {
                child.Material = material;
                ApplyEnergyMaterial(child, material);
            }
        }
    }

    private sealed partial class EnemyCardFallbackCardView : Control
    {
        private static readonly Vector2 CardSize = NCard.defaultSize * CardScale;
        private static readonly Vector2 BaseCardSize = NCard.defaultSize;

        public static Control? Create(EnemyCardSpec spec, bool isNext)
        {
            try
            {
                CardModel model = spec.CreateDisplayCard();
                return new EnemyCardFallbackCardView(model, isNext);
            }
            catch (Exception ex)
            {
                Log.Warn("[LibraryOfRuina.EnemyCards] Enemy fallback card failed for " + spec.Id + ": " + ex);
                return null;
            }
        }

        private EnemyCardFallbackCardView(CardModel model, bool isNext)
        {
            Size = CardSize;
            CustomMinimumSize = CardSize;
            MouseFilter = MouseFilterEnum.Ignore;

            var body = new Control
            {
                Size = BaseCardSize,
                Scale = Vector2.One * CardScale,
                MouseFilter = MouseFilterEnum.Ignore
            };
            this.AddChildSafely(body);

            AddTexture(body, model.Frame, Vector2.Zero, BaseCardSize, model.FrameMaterial);
            AddTexture(body, model.Portrait, new Vector2(36f, 66f), new Vector2(228f, 146f), model.BannerMaterial);
            AddTexture(body, model.PortraitBorder, new Vector2(29f, 58f), new Vector2(242f, 164f), model.BannerMaterial);
            AddTexture(body, model.BannerTexture, new Vector2(25f, 17f), new Vector2(250f, 58f), model.BannerMaterial);
            AddTitle(body, model.Title);
            AddEnergy(body, model);
            AddDescription(body, model);

            if (isNext)
            {
                AddNextGlow();
            }
        }

        private static void AddTexture(Control parent, Texture2D? texture, Vector2 position, Vector2 size, Material? material = null)
        {
            if (texture == null)
            {
                return;
            }

            parent.AddChildSafely(new TextureRect
            {
                Texture = texture,
                Position = position,
                Size = size,
                Material = material,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Scale,
                MouseFilter = MouseFilterEnum.Ignore
            });
        }

        private static void AddTitle(Control parent, string title)
        {
            Label label = CreateLabel(title, 18, StsColors.cream);
            label.Position = new Vector2(46f, 25f);
            label.Size = new Vector2(208f, 32f);
            parent.AddChildSafely(label);
        }

        private static void AddEnergy(Control parent, CardModel model)
        {
            AddTexture(parent, model.EnergyIcon, new Vector2(-4f, -3f), new Vector2(58f, 58f));
            Label label = CreateLabel(
                model.EnergyCost.CostsX ? "X" : Math.Max(0, model.EnergyCost.GetWithModifiers(CostModifiers.None)).ToString(),
                23,
                StsColors.cream);
            label.Position = new Vector2(0f, 4f);
            label.Size = new Vector2(46f, 38f);
            parent.AddChildSafely(label);
        }

        private static void AddDescription(Control parent, CardModel model)
        {
            var text = new RichTextLabel
            {
                BbcodeEnabled = true,
                Text = "[center]" + model.GetDescriptionForPile(PileType.None) + "[/center]",
                FitContent = false,
                ScrollActive = false,
                Position = new Vector2(38f, 235f),
                Size = new Vector2(224f, 132f),
                MouseFilter = MouseFilterEnum.Ignore
            };
            text.AddThemeFontSizeOverride("normal_font_size", 17);
            text.AddThemeColorOverride("default_color", StsColors.cream);
            parent.AddChildSafely(text);
        }

        private void AddNextGlow()
        {
            var glow = new ColorRect
            {
                Color = new Color(1f, 0.78f, 0.22f, 0.20f),
                Size = CardSize,
                MouseFilter = MouseFilterEnum.Ignore
            };
            this.AddChildSafely(glow);
        }
    }
}
