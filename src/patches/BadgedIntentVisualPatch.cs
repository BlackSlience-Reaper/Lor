using System;
using System.Linq;
using Godot;
using LibraryOfRuina.content.liberation.Natural;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.framework.intents.rendering;
using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.Fonts;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.HoverTips;

namespace LibraryOfRuina.patches;

internal static class BadgedIntentVisualPatch
{
    private static ShaderMaterial? _allyCombinedAttackMaterial;

    private const string DetailNodePrefix = "LibraryOfRuinaDetailedIntent";
    private const string MainBadgeNodePrefix = "LibraryOfRuinaMainIntentBadge";
    private const string VisualHashMetaKey = "LibraryOfRuina_VisualHash";
    private const float IntentIconSize = 64f;
    private const float EffectIconSize = 46f;
    private const float MainBadgeSlotSize = 40f;
    private const float PowerBadgeSizeMultiplier = 0.5f;
    private const float MainBadgeGap = 4f;
    private const int MaxBadgesPerRow = 2;
    private const int MainBadgeZIndex = 0;
    private const int DetailZIndex = 0;
    private const int TargetMarkerZIndex = 0;
    private const float SmallCardScale = 0.155f;
    private const float SmallCardHoverScale = 0.48f;
    private const float SmallCardWidth = 300f * SmallCardScale;
    private const float SmallCardHeight = 422f * SmallCardScale;
    private const float FloatingCardScale = 0.38f;
    private const float FloatingCardHoverScale = 0.60f;
    private const float FloatingCardVerticalOffset = 28f;
    private const float FloatingCardStackOffset = 12f;
    private const float RowHeight = 58f;
    private const float RowGap = 0f;
    private const float DetailIntentGap = 4.8f;
    private const float NumberWidth = 30f;
    private const float RightNumberWidth = 30f;
    private const float RightIconSize = 42f;
    private const float ScopeHeight = 32f;
    private const float ScopeTextMinWidth = 42f;
    private const float ScopeTextMaxWidth = 160f;
    private const float ScopeTextApproxCharWidth = 14f;
    private const float TargetBadgeSize = 46.8f;
    private const float TargetTextWidth = 88f;
    private const string LabelFontPath = "res://themes/fonts/zhs/noto_sans_mono_cjksc_regular_shared.tres";
    private const string FallbackLabelFontPath = "res://themes/kreon_bold_glyph_space_one.tres";
    private const int PreviewCardHoverZIndex = 0;
    private const int PileIconZIndex = DetailZIndex;

    internal static IntentDecoratorOutcome OnUpdateVisuals(
        NIntent __instance,
        AbstractIntent ____intent,
        IEnumerable<Creature> ____targets,
        Creature ____owner)
    {
        if (!__instance.HasNode("%IntentHolder"))
        {
            return IntentDecoratorOutcome.Skipped;
        }

        Control holder = __instance.GetNode<Control>("%IntentHolder");
        Color intentColor = holder.Modulate;
        holder.Modulate = new Color(1f, 1f, 1f, intentColor.A);
        ApplyAllyAttackColor(__instance, ____intent, ____owner);

        DetailedIntentVisualState visualState = GetVisualState(____intent, ____targets, ____owner);
        string newHash = ComputeVisualHash(____intent, visualState, ____owner);

        if (holder.HasMeta(VisualHashMetaKey)
            && (string)holder.GetMeta(VisualHashMetaKey) == newHash)
        {
            return IntentDecoratorOutcome.Unchanged;
        }

        holder.SetMeta(VisualHashMetaKey, newHash);
        RemoveDetailNodes(holder);

        IReadOnlyList<IntentBadge> intentEffects = IntentEffectCollection.Get(____intent);
        if (intentEffects.Count > 0)
        {
            IntentBadge[] mainBadges = intentEffects
                .Where(static badge => badge.IsVisible && IsMainOverlayBadge(badge))
                .ToArray();
            if (mainBadges.Length > 0)
            {
                AddMainBadgeNodes(holder, mainBadges);
            }
        }

        if (visualState.Effects.Count == 0 && visualState.SingleTarget == null)
        {
            return IntentDecoratorOutcome.Applied;
        }

        IReadOnlyList<DetailedIntentVisualEffect> effectRows = visualState.Effects
            .Where(static effect =>
                !effect.IsDecorativeOnly
                && effect.Placement != DetailedIntentEffectPlacement.FloatingCardAboveIntent)
            .OrderBy(static effect => effect.Placement == DetailedIntentEffectPlacement.BelowMain ? 1 : 0)
            .ToArray();
        IReadOnlyList<DetailedIntentVisualEffect> floatingCards = visualState.Effects
            .Where(static effect =>
                !effect.IsDecorativeOnly
                && effect.Placement == DetailedIntentEffectPlacement.FloatingCardAboveIntent)
            .ToArray();

        for (int i = 0; i < effectRows.Count; i++)
        {
            Control row = BuildEffectRow(effectRows[i], $"Above{i}");
            int rowsAboveMain = effectRows.Count - i - 1;
            row.Position = new Vector2(GetRowX(effectRows[i]), GetEffectRowY(effectRows[i], rowsAboveMain));
            holder.AddChild(row);
        }

        for (int i = 0; i < floatingCards.Count; i++)
        {
            Control floatingCard = BuildFloatingCardNode(floatingCards[i], $"AboveCard{i}");
            float verticalOffset = floatingCards[i].FloatingPreviewVerticalOffset ?? FloatingCardVerticalOffset;
            float stackOffset = floatingCards[i].FloatingPreviewStackOffset ?? FloatingCardStackOffset;
            floatingCard.Position = new Vector2(
                (IntentIconSize - floatingCard.Size.X) * 0.5f,
                -(floatingCard.Size.Y + verticalOffset + i * stackOffset));
            holder.AddChild(floatingCard);
        }

        if (visualState.SingleTarget != null && ShouldShowTargetMarker(____intent, visualState.SingleTarget, ____owner, ____targets))
        {
            Control target = BuildTargetMarker(visualState.SingleTarget);
            target.Position = new Vector2(-6f, -2f);
            holder.AddChild(target);
        }

        return IntentDecoratorOutcome.Applied;
    }

    private static void ApplyAllyAttackColor(NIntent intentNode, AbstractIntent intent, Creature owner)
    {
        Sprite2D intentSprite = intentNode.GetNode<Sprite2D>("%Intent");
        CanvasItem valueLabel = intentNode.GetNode<CanvasItem>("%Value");
        bool isAllyAttack = AllyTurnRegistry.IsAllyCreature(owner) && intent is AttackIntent;

        valueLabel.Modulate = isAllyAttack ? Colors.Green : Colors.White;

        if (!isAllyAttack)
        {
            intentSprite.Modulate = Colors.White;
            intentSprite.Material = null;
            return;
        }

        if (intent is ICombinedIntentVisual)
        {
            intentSprite.Modulate = Colors.White;
            intentSprite.Material = GetAllyCombinedAttackMaterial();
            return;
        }

        intentSprite.Material = null;
        intentSprite.Modulate = Colors.Green;
    }

    private static ShaderMaterial GetAllyCombinedAttackMaterial()
    {
        if (_allyCombinedAttackMaterial != null)
        {
            return _allyCombinedAttackMaterial;
        }

        var shader = new Shader
        {
            Code = """
                shader_type canvas_item;

                void fragment() {
                    vec4 color = texture(TEXTURE, UV) * COLOR;
                    bool is_attack_red = color.r > color.g * 1.15 && color.r > color.b * 1.15;
                    if (is_attack_red) {
                        float intensity = max(color.r, max(color.g, color.b));
                        color.rgb = vec3(0.0, intensity, 0.0);
                    }
                    COLOR = color;
                }
                """
        };
        _allyCombinedAttackMaterial = new ShaderMaterial { Shader = shader };
        return _allyCombinedAttackMaterial;
    }

    private static void AddMainBadgeNodes(Control holder, IReadOnlyList<IntentBadge> badges)
    {
        List<(IntentBadge Badge, Texture2D Texture)> badgeVisuals = [];
        foreach (IntentBadge badge in badges)
        {
            Texture2D? texture = badge.GetTexture();
            if (GodotTextureSafety.IsValid(texture))
            {
                badgeVisuals.Add((badge, texture));
            }
        }

        for (int i = 0; i < badgeVisuals.Count; i++)
        {
            IntentBadge badge = badgeVisuals[i].Badge;
            TextureRect badgeNode = new()
            {
                Name = MainBadgeNodePrefix + i,
                MouseFilter = badge.HasExtraHoverTips
                    ? Control.MouseFilterEnum.Pass
                    : Control.MouseFilterEnum.Ignore,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                ZIndex = MainBadgeZIndex
            };
            GodotTextureSafety.TrySetTexture(badgeNode, badgeVisuals[i].Texture);

            int column = i % MaxBadgesPerRow;
            int row = i / MaxBadgesPerRow;
            float renderSize = GetMainBadgeRenderSize(badge);
            Vector2 slotPosition = new(
                IntentIconSize - MainBadgeSlotSize - 2f - column * (MainBadgeSlotSize + MainBadgeGap),
                IntentIconSize - MainBadgeSlotSize - 4f - row * (MainBadgeSlotSize + MainBadgeGap));
            float centerOffset = (MainBadgeSlotSize - renderSize) * 0.5f;

            badgeNode.Size = new Vector2(renderSize, renderSize);
            badgeNode.Position = slotPosition + new Vector2(centerOffset, centerOffset);
            if (badge.HasExtraHoverTips)
            {
                badgeNode.Connect(Control.SignalName.MouseEntered, Callable.From(() =>
                {
                    NHoverTipSet.CreateAndShow(badgeNode, badge.ExtraHoverTips, HoverTipAlignment.Right);
                }));
                badgeNode.Connect(Control.SignalName.MouseExited, Callable.From(() =>
                {
                    NHoverTipSet.Remove(badgeNode);
                }));
            }

            holder.AddChild(badgeNode);
        }
    }

    private static bool IsMainOverlayBadge(IntentBadge badge)
    {
        return badge.Kind == IntentBadgeKind.Custom
            && badge.Amount == 0
            && string.IsNullOrWhiteSpace(badge.LeftText)
            && string.IsNullOrWhiteSpace(badge.RightText);
    }

    private static float GetMainBadgeRenderSize(IntentBadge badge)
    {
        return badge.Kind == IntentBadgeKind.Power
            ? MainBadgeSlotSize * PowerBadgeSizeMultiplier
            : MainBadgeSlotSize;
    }

    private static DetailedIntentVisualState GetVisualState(
        AbstractIntent intent,
        IEnumerable<Creature> targets,
        Creature owner)
    {
        if (intent is IDetailedIntentVisuals detailed)
        {
            DetailedIntentVisualState state = detailed.GetDetailedIntentVisuals(targets, owner);
            Creature? singleTarget = state.SingleTarget ?? GetExplicitSingleTarget(intent, targets, owner);
            return singleTarget == state.SingleTarget
                ? state
                : new DetailedIntentVisualState(state.Effects, singleTarget);
        }

        IReadOnlyList<IntentBadge> intentEffects = IntentEffectCollection.Get(intent);
        if (intentEffects.Count > 0)
        {
            Creature? singleTarget = GetExplicitSingleTarget(intent, targets, owner);
            IReadOnlyList<Creature> targetList = singleTarget == null
                ? ResolveEffectTargets(intent, targets, owner)
                : [singleTarget];

            return new DetailedIntentVisualState(
                intentEffects
                    .Where(static badge => badge.IsVisible && !IsMainOverlayBadge(badge))
                    .Select(badge => DetailedIntentVisualEffect.FromBadge(
                        badge,
                        GetBadgeScopeText(intent, badge, targetList, owner)))
                    .ToArray(),
                singleTarget);
        }

        if (intent is ITargetedIntentIndicator)
        {
            Creature? singleTarget = GetExplicitSingleTarget(intent, targets, owner);
            return new DetailedIntentVisualState(Array.Empty<DetailedIntentVisualEffect>(), singleTarget);
        }

        return DetailedIntentVisualState.Empty;
    }

    private static Creature? GetExplicitSingleTarget(AbstractIntent intent, IEnumerable<Creature> targets, Creature owner)
    {
        if (intent is IGroupAttackIntent { IsGroupAttack: true })
        {
            return null;
        }

        if (intent is IIntentTargetLineProvider provider)
        {
            IReadOnlyList<Creature> explicitTargets = provider
                .GetIntentTargetLineTargets(owner, ToTargetList(targets))
                .Select(static target => target.Target)
                .Where(static target => target is { IsAlive: true })
                .Distinct()
                .ToArray();
            return explicitTargets.Count == 1 ? explicitTargets[0] : null;
        }

        if (intent is ITargetedIntentIndicator)
        {
            IReadOnlyList<Creature> targetedList = TargetedMonsterAttackHelper.GetTargetList(owner, ToTargetList(targets));
            return targetedList.Count == 1 ? targetedList[0] : null;
        }

        return null;
    }

    private static IReadOnlyList<Creature> ToTargetList(IEnumerable<Creature> targets)
    {
        return targets as IReadOnlyList<Creature>
            ?? targets.Where(static target => target is { IsAlive: true }).Distinct().ToArray();
    }

    private static IReadOnlyList<Creature> ResolveEffectTargets(
        AbstractIntent intent,
        IEnumerable<Creature> targets,
        Creature owner)
    {
        if (intent is ITargetedIntentIndicator)
        {
            return TargetedMonsterAttackHelper.GetTargetList(owner, ToTargetList(targets));
        }

        return ToTargetList(targets);
    }

    private static string? GetBadgeScopeText(
        AbstractIntent intent,
        IntentBadge badge,
        IReadOnlyList<Creature> effectTargets,
        Creature owner)
    {
        return badge.Kind switch
        {
            IntentBadgeKind.Power => GetPowerBadgeScopeText(intent, badge, effectTargets, owner),
            IntentBadgeKind.StatusCard => GetTargetScopeText(effectTargets, owner),
            IntentBadgeKind.Heal or IntentBadgeKind.Summon => null,
            _ => null
        };
    }

    private static string? GetPowerBadgeScopeText(
        AbstractIntent intent,
        IntentBadge badge,
        IReadOnlyList<Creature> effectTargets,
        Creature owner)
    {
        PowerType powerType = badge.Power.GetTypeForAmount(badge.Amount);
        if (powerType == PowerType.Buff)
        {
            return null;
        }

        if (powerType == PowerType.Debuff)
        {
            return GetTargetScopeText(effectTargets, owner);
        }

        return intent.IntentType == IntentType.Buff ? null : GetTargetScopeText(effectTargets, owner);
    }

    private static string? GetTargetScopeText(IReadOnlyList<Creature> targets, Creature owner)
    {
        IReadOnlyList<Creature> liveTargets = targets
            .Where(static target => target is { IsAlive: true })
            .Distinct()
            .ToArray();

        if (liveTargets.Count == 1)
        {
            return null;
        }

        if (liveTargets.Count > 0 && liveTargets.All(static target => target.IsMonster))
        {
            return liveTargets.Contains(owner)
                ? DetailedIntentScopeText.AllEnemies
                : DetailedIntentScopeText.OtherEnemies;
        }

        return null;
    }

    private static bool ShouldShowTargetMarker(
        AbstractIntent intent,
        Creature target,
        Creature owner,
        IEnumerable<Creature> ordinaryTargets)
    {
        if (target == null || !target.IsAlive)
        {
            return false;
        }

        if (intent is not AttackIntent)
        {
            return false;
        }

        if (target.IsPlayer
            && intent is IUsesVanillaPlayerTargetIntentVisual { UsesVanillaPlayerTargetIntentVisual: true })
        {
            return false;
        }

        IReadOnlyList<Creature> ordinary = ordinaryTargets
            .Where(static creature => creature is { IsAlive: true })
            .Distinct()
            .ToArray();

        return !(target.IsPlayer && ordinary.Count > 1 && ordinary.All(static creature => creature.IsPlayer));
    }

    private static float GetEffectRowY(DetailedIntentVisualEffect effect, int rowsAboveMain)
    {
        float visualHeight = GetVisualHeight(effect);
        float topPadding = (RowHeight - visualHeight) * 0.5f;
        return -(DetailIntentGap + visualHeight + topPadding + rowsAboveMain * (RowHeight + RowGap));
    }

    private static float GetRowX(DetailedIntentVisualEffect effect)
    {
        return (IntentIconSize - GetFullRowWidth(effect)) * 0.5f;
    }

    private static Control BuildEffectRow(DetailedIntentVisualEffect effect, string suffix)
    {
        string? scopeText = DetailedIntentScopeText.Normalize(effect.ScopeText);
        var root = new Control
        {
            Name = DetailNodePrefix + "Effect" + suffix,
            Size = new Vector2(GetFullRowWidth(effect), RowHeight),
            MouseFilter = effect.UseSmallCard || effect.UseCardIntentBadge || effect.HoverTipsFactory != null
                ? Control.MouseFilterEnum.Pass
                : Control.MouseFilterEnum.Ignore,
            ZIndex = DetailZIndex
        };

        float cursor = 0f;

        if (effect.UseCornerNumberLayout)
        {
            Control visual = BuildEffectVisual(effect);
            visual.Position = new Vector2(cursor, (RowHeight - GetVisualHeight(effect)) * 0.5f);
            root.AddChild(visual);

            if (!string.IsNullOrWhiteSpace(effect.TopRightText))
            {
                Label? topRight = CreateLabel(effect.TopRightText, 16, HorizontalAlignment.Right);
                if (topRight != null)
                {
                    topRight.Position = new Vector2(
                        cursor + EffectIconSize - 32f,
                        (RowHeight - EffectIconSize) * 0.5f - 1f);
                    topRight.Size = new Vector2(32f, 18f);
                    root.AddChild(topRight);
                }
            }

            if (!string.IsNullOrWhiteSpace(effect.BottomRightText))
            {
                Label? bottomRight = CreateLabel(effect.BottomRightText, 18, HorizontalAlignment.Right);
                if (bottomRight != null)
                {
                    bottomRight.Position = new Vector2(
                        cursor + EffectIconSize - 32f,
                        (RowHeight + EffectIconSize) * 0.5f - 18f + 1f);
                    bottomRight.Size = new Vector2(32f, 18f);
                    root.AddChild(bottomRight);
                }
            }

            cursor += GetVisualWidth(effect);
        }
        else
        {
            Label? left = CreateLabel(effect.LeftText, 20, HorizontalAlignment.Right);
            if (left != null)
            {
                left.Position = new Vector2(cursor, (RowHeight - ScopeHeight) * 0.5f);
                left.Size = new Vector2(NumberWidth, ScopeHeight);
                root.AddChild(left);
                cursor += NumberWidth;
            }

            Control visual = BuildEffectVisual(effect);
            visual.Position = new Vector2(cursor, (RowHeight - GetVisualHeight(effect)) * 0.5f);
            root.AddChild(visual);
            cursor += GetVisualWidth(effect);

            Label? right = CreateLabel(effect.RightText, 20, HorizontalAlignment.Left);
            if (right != null)
            {
                right.Position = new Vector2(cursor, (RowHeight - ScopeHeight) * 0.5f);
                right.Size = new Vector2(RightNumberWidth, ScopeHeight);
                root.AddChild(right);
                cursor += RightNumberWidth;
            }
        }

        Texture2D? rightIcon = effect.GetRightIconTexture();
        if (rightIcon != null)
        {
            TextureRect icon = CreateTextureRect(rightIcon, RightIconSize);
            icon.Name = "RightIcon";
            icon.ZIndex = PileIconZIndex;
            icon.Position = new Vector2(cursor, (RowHeight - RightIconSize) * 0.5f);
            root.AddChild(icon);
            cursor += RightIconSize + 4f;
        }

        if (!string.IsNullOrWhiteSpace(scopeText))
        {
            Label? scope = CreateLabel(scopeText, 16, HorizontalAlignment.Left);
            if (scope != null)
            {
                scope.Position = new Vector2(cursor, (RowHeight - ScopeHeight) * 0.5f + 1f);
                scope.Size = new Vector2(GetScopeTextWidth(scopeText), ScopeHeight);
                root.AddChild(scope);
            }
        }

        return root;
    }

    private static Control BuildEffectVisual(DetailedIntentVisualEffect effect)
    {
        if (effect.UseCardIntentBadge && effect.CardFactory != null)
        {
            Control? badge = BuildCardIntentBadge(effect);
            if (badge != null)
            {
                return badge;
            }
        }

        if (effect.UseSmallCard && effect.CardFactory != null)
        {
            Control? preview = CreatePreviewCardWrapper(
                effect.CardFactory,
                new Vector2(SmallCardWidth, SmallCardHeight),
                SmallCardScale,
                showHoverTips: false,
                scaleOnHover: false,
                hoverScale: SmallCardHoverScale,
                anchor: PreviewCardAnchor.Center,
                hoverTipsFactory: effect.GetHoverTips);
            if (preview != null)
            {
                preview.Name = "SmallCard";
                return preview;
            }
        }

        Texture2D? texture = effect.GetTexture();
        if (texture != null)
        {
            TextureRect icon = CreateTextureRect(texture, EffectIconSize);
            icon.Name = "Icon";
            if (effect.HoverTipsFactory != null)
            {
                icon.MouseFilter = Control.MouseFilterEnum.Pass;
                icon.Connect(Control.SignalName.MouseEntered, Callable.From(() =>
                {
                    NHoverTipSet.CreateAndShow(icon, effect.GetHoverTips(null), HoverTipAlignment.Right);
                }));
                icon.Connect(Control.SignalName.MouseExited, Callable.From(() =>
                {
                    NHoverTipSet.Remove(icon);
                }));
            }

            return icon;
        }

        return new Control
        {
            Name = "EmptyIcon",
            Size = new Vector2(EffectIconSize, EffectIconSize),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
    }

    private static Control? BuildCardIntentBadge(DetailedIntentVisualEffect effect)
    {
        try
        {
            CardModel card = effect.CardFactory!();
            var root = new Control
            {
                Name = "CardBadge",
                Size = new Vector2(EffectIconSize, EffectIconSize),
                CustomMinimumSize = new Vector2(EffectIconSize, EffectIconSize),
                MouseFilter = effect.HoverTipsFactory != null
                    ? Control.MouseFilterEnum.Pass
                    : Control.MouseFilterEnum.Ignore,
                ClipContents = true
            };

            root.AddChild(new ColorRect
            {
                Name = "CardBadgeBackground",
                Color = new Color(0.10f, 0.075f, 0.045f, 0.90f),
                Size = root.Size,
                MouseFilter = Control.MouseFilterEnum.Ignore
            });

            Texture2D? portrait = card.Portrait;
            if (GodotTextureSafety.IsValid(portrait))
            {
                TextureRect portraitNode = new()
                {
                    Name = "CardPortrait",
                    Position = new Vector2(4f, 4f),
                    Size = new Vector2(EffectIconSize - 8f, EffectIconSize - 8f),
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                    MouseFilter = Control.MouseFilterEnum.Ignore
                };
                GodotTextureSafety.TrySetTexture(portraitNode, portrait);
                root.AddChild(portraitNode);
            }

            AddCardBadgeBorder(root);

            if (effect.HoverTipsFactory != null)
            {
                root.Connect(Control.SignalName.MouseEntered, Callable.From(() =>
                {
                    NHoverTipSet.CreateAndShow(root, effect.GetHoverTips(card), HoverTipAlignment.Right);
                }));
                root.Connect(Control.SignalName.MouseExited, Callable.From(() =>
                {
                    NHoverTipSet.Remove(root);
                }));
            }

            return root;
        }
        catch (Exception ex)
        {
            Log.Warn("[LibraryOfRuina.Intents] Skipped detailed status-card badge: " + ex);
            return null;
        }
    }

    private static void AddCardBadgeBorder(Control root)
    {
        Color color = new(1f, 0.76f, 0.22f, 0.94f);
        const float thickness = 2f;
        Vector2 size = root.Size;
        AddBorderRect(root, Vector2.Zero, new Vector2(size.X, thickness), color);
        AddBorderRect(root, new Vector2(0f, size.Y - thickness), new Vector2(size.X, thickness), color);
        AddBorderRect(root, Vector2.Zero, new Vector2(thickness, size.Y), color);
        AddBorderRect(root, new Vector2(size.X - thickness, 0f), new Vector2(thickness, size.Y), color);
    }

    private static void AddBorderRect(Control parent, Vector2 position, Vector2 size, Color color)
    {
        parent.AddChild(new ColorRect
        {
            Color = color,
            Position = position,
            Size = size,
            MouseFilter = Control.MouseFilterEnum.Ignore
        });
    }

    private static Control BuildFloatingCardNode(DetailedIntentVisualEffect effect, string suffix)
    {
        float scale = effect.FloatingPreviewScale ?? FloatingCardScale;
        float hoverScale = effect.FloatingPreviewHoverScale ?? FloatingCardHoverScale;
        Vector2 size = NCard.defaultSize * scale;
        var root = new Control
        {
            Name = DetailNodePrefix + suffix,
            Size = size,
            MouseFilter = Control.MouseFilterEnum.Pass,
            ZIndex = DetailZIndex
        };

        if (!effect.UseFloatingPreviewCard || effect.CardFactory == null)
        {
            return root;
        }

        Control? preview = CreatePreviewCardWrapper(
            effect.CardFactory,
            root.Size,
            scale,
            showHoverTips: false,
            scaleOnHover: false,
            hoverScale: hoverScale,
            anchor: PreviewCardAnchor.BottomCenter,
            hoverTipsFactory: effect.GetHoverTips);
        if (preview != null)
        {
            preview.Name = "FloatingPreview";
            root.AddChild(preview);
        }

        return root;
    }

    private static Control? CreatePreviewCardWrapper(
        Func<CardModel> cardFactory,
        Vector2 wrapperSize,
        float scale,
        bool showHoverTips,
        bool scaleOnHover,
        float hoverScale,
        PreviewCardAnchor anchor,
        Func<CardModel?, IEnumerable<IHoverTip>> hoverTipsFactory)
    {
        CardModel card = cardFactory();
        NCard? cardNode = NCard.Create(card);
        if (cardNode == null)
        {
            return null;
        }

        WirePreviewCardVisuals(cardNode);
        NPreviewCardHolder? holder = NPreviewCardHolder.Create(cardNode, showHoverTips, scaleOnHover);
        if (holder == null)
        {
            cardNode.QueueFreeSafely();
            return null;
        }

        Vector2 baseScale = Vector2.One * scale;
        Vector2 basePosition = GetPreviewCardPosition(wrapperSize, scale, anchor);
        Vector2 hoverPosition = GetPreviewCardPosition(wrapperSize, hoverScale, anchor);

        var wrapper = new Control
        {
            Size = wrapperSize,
            MouseFilter = Control.MouseFilterEnum.Pass
        };

        holder.Position = basePosition;
        holder.SetCardScale(baseScale);
        holder.MouseFilter = Control.MouseFilterEnum.Ignore;
        holder.SetClickable(false);
        holder.Connect(Node.SignalName.Ready, Callable.From(() =>
        {
            AttachPreviewCardHover(
                holder,
                holder.Hitbox,
                card,
                baseScale,
                basePosition,
                Vector2.One * hoverScale,
                hoverPosition,
                hoverTipsFactory);
        }));
        wrapper.AddChild(holder);

        bool usesCustomHitboxHover = hoverScale > scale;
        if (!showHoverTips && !usesCustomHitboxHover)
        {
            wrapper.Connect(Control.SignalName.MouseEntered, Callable.From(() =>
            {
                NHoverTipSet.CreateAndShow(wrapper, hoverTipsFactory(card), HoverTipAlignment.Right);
            }));
            wrapper.Connect(Control.SignalName.MouseExited, Callable.From(() =>
            {
                NHoverTipSet.Remove(wrapper);
            }));
        }

        return wrapper;
    }

    private static void WirePreviewCardVisuals(NCard cardNode)
    {
        if (cardNode.IsNodeReady())
        {
            SafeUpdatePreviewCardVisuals(cardNode);
            return;
        }

        cardNode.Connect(Node.SignalName.Ready, Callable.From(() =>
        {
            SafeUpdatePreviewCardVisuals(cardNode);
        }), flags: (uint)GodotObject.ConnectFlags.OneShot);
    }

    private static void SafeUpdatePreviewCardVisuals(NCard cardNode)
    {
        try
        {
            cardNode.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
        }
        catch (Exception ex)
        {
            Log.Warn("[LibraryOfRuina.Intents] Skipped detailed intent card visual update: " + ex);
        }
    }

    private static Vector2 GetPreviewCardPosition(Vector2 wrapperSize, float scale, PreviewCardAnchor anchor)
    {
        Vector2 scaledCardSize = NCard.defaultSize * scale;
        return anchor == PreviewCardAnchor.BottomCenter
            ? new Vector2((wrapperSize.X - scaledCardSize.X) * 0.5f, wrapperSize.Y - scaledCardSize.Y)
            : (wrapperSize - scaledCardSize) * 0.5f;
    }

    private static void AttachPreviewCardHover(
        NPreviewCardHolder holder,
        NClickableControl hitbox,
        CardModel card,
        Vector2 baseScale,
        Vector2 basePosition,
        Vector2 hoverScale,
        Vector2 hoverPosition,
        Func<CardModel?, IEnumerable<IHoverTip>> hoverTipsFactory)
    {
        hitbox.MouseFilter = Control.MouseFilterEnum.Pass;
        hitbox.Connect(NClickableControl.SignalName.Focused, Callable.From<NClickableControl>(_ =>
        {
            holder.ZIndex = PreviewCardHoverZIndex;
            holder.Scale = hoverScale;
            holder.Position = hoverPosition;
            NHoverTipSet.Remove(hitbox);
            IEnumerable<IHoverTip> nonCardTips = GetNonCardHoverTips(hoverTipsFactory(card));
            if (nonCardTips.Any())
            {
                NHoverTipSet.CreateAndShow(hitbox, nonCardTips, HoverTipAlignment.Right);
            }
        }));
        hitbox.Connect(NClickableControl.SignalName.Unfocused, Callable.From<NClickableControl>(_ =>
        {
            NHoverTipSet.Remove(hitbox);
            holder.Scale = baseScale;
            holder.Position = basePosition;
            holder.ZIndex = 0;
        }));
    }

    private static IEnumerable<IHoverTip> GetNonCardHoverTips(IEnumerable<IHoverTip> hoverTips)
    {
        return IHoverTip.RemoveDupes(hoverTips.Where(static tip => tip is not CardHoverTip));
    }

    private static float GetVisualWidth(DetailedIntentVisualEffect effect)
    {
        return effect.UseSmallCard ? SmallCardWidth : EffectIconSize;
    }

    private static float GetVisualHeight(DetailedIntentVisualEffect effect)
    {
        return effect.UseSmallCard ? SmallCardHeight : EffectIconSize;
    }

    private static float GetCompactRowWidth(DetailedIntentVisualEffect effect)
    {
        if (effect.UseCornerNumberLayout)
        {
            float width = GetVisualWidth(effect);
            if (!string.IsNullOrWhiteSpace(effect.RightIconPath))
            {
                width += RightIconSize + 4f;
            }

            return width;
        }

        float baseWidth = GetVisualWidth(effect);
        if (!string.IsNullOrWhiteSpace(effect.LeftText))
        {
            baseWidth += NumberWidth;
        }

        if (!string.IsNullOrWhiteSpace(effect.RightText))
        {
            baseWidth += RightNumberWidth;
        }

        if (!string.IsNullOrWhiteSpace(effect.RightIconPath))
        {
            baseWidth += RightIconSize + 4f;
        }

        return baseWidth;
    }

    private static float GetFullRowWidth(DetailedIntentVisualEffect effect)
    {
        float width = GetCompactRowWidth(effect);
        string? scopeText = DetailedIntentScopeText.Normalize(effect.ScopeText);
        if (!string.IsNullOrWhiteSpace(scopeText))
        {
            width += GetScopeTextWidth(scopeText);
        }

        return width;
    }

    private static float GetScopeTextWidth(string scopeText)
    {
        return Math.Clamp(
            scopeText.Length * ScopeTextApproxCharWidth,
            ScopeTextMinWidth,
            ScopeTextMaxWidth);
    }

    private static Control BuildTargetMarker(Creature target)
    {
        var root = new Control
        {
            Name = DetailNodePrefix + "TargetMarker",
            Size = target.IsMonster
                ? new Vector2(TargetBadgeSize, TargetBadgeSize)
                : new Vector2(TargetTextWidth, 24f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = TargetMarkerZIndex
        };

        if (target.IsMonster && TryGetMonsterHeadTexture(target, out Texture2D? texture) && texture != null)
        {
            TextureRect icon = CreateTextureRect(texture, TargetBadgeSize);
            icon.Name = "MonsterHead";
            root.AddChild(icon);
            return root;
        }

        Label? label = CreateLabel(target.Name, 13, HorizontalAlignment.Center);
        if (label != null)
        {
            label.Name = "TargetName";
            label.Position = Vector2.Zero;
            label.Size = root.Size;
            root.AddChild(label);
        }

        return root;
    }

    private static bool TryGetMonsterHeadTexture(Creature target, out Texture2D? texture)
    {
        // 指定目标使用独立头像，避免随战斗姿态或形态变化重新裁切。
        string? portraitPath = target.Monster switch
        {
            NaturalFloorNihilBoss => "res://images/intents/targets/nihil.png",
            content.abnormalities.WrathServant.WrathServant or NaturalFloorBlindRageBoss => "res://images/intents/targets/wrath_servant.png",
            _ => null
        };
        if (portraitPath != null)
        {
            texture = ResourceLoader.Load<Texture2D>(portraitPath);
            return GodotTextureSafety.IsValid(texture);
        }

        texture = null;
        NCreature? creatureNode = CombatQueries.CreatureNodeOf(target);
        Sprite2D? sprite = creatureNode?.Visuals?.GetNodeOrNull<Sprite2D>("%Visuals");
        Texture2D? source = sprite?.Texture;
        if (source == null)
        {
            return false;
        }

        texture = MonsterHeadTextureCache.GetOrCreate(source);
        return GodotTextureSafety.IsValid(texture);
    }

    private static TextureRect CreateTextureRect(Texture2D texture, float size)
    {
        TextureRect rect = new()
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            Size = new Vector2(size, size),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        GodotTextureSafety.TrySetTexture(rect, texture);
        return rect;
    }

    private static Label? CreateLabel(string? text, int fontSize, HorizontalAlignment alignment)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var label = new Label
        {
            Text = text,
            HorizontalAlignment = alignment,
            VerticalAlignment = VerticalAlignment.Center,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            LabelSettings = new LabelSettings
            {
                Font = GetLabelFont(),
                FontSize = fontSize,
                FontColor = new Color(1f, 0.96f, 0.86f),
                OutlineSize = 8,
                OutlineColor = new Color(0f, 0f, 0f, 0.78f),
                ShadowSize = 2,
                ShadowColor = new Color(0f, 0f, 0f, 0.65f)
            }
        };

        label.ApplyLocaleFontSubstitution(FontType.Regular, new StringName("font"));
        return label;
    }

    private static Font? GetLabelFont()
    {
        return ResourceLoader.Load<Font>(LabelFontPath)
            ?? ResourceLoader.Load<Font>(FallbackLabelFontPath);
    }

    private static void RemoveDetailNodes(Control holder)
    {
        foreach (Node child in holder.GetChildren().ToArray())
        {
            if (child.Name.ToString().StartsWith(DetailNodePrefix, StringComparison.Ordinal))
            {
                holder.RemoveChild(child);
                child.QueueFreeSafely();
            }
            else if (child.Name.ToString().StartsWith(MainBadgeNodePrefix, StringComparison.Ordinal))
            {
                holder.RemoveChild(child);
                child.QueueFreeSafely();
            }
        }
    }

    private static string ComputeVisualHash(
        AbstractIntent intent,
        DetailedIntentVisualState visualState,
        Creature owner)
    {
        int hash = intent.GetType().GetHashCode();

        IReadOnlyList<IntentBadge> intentEffects = IntentEffectCollection.Get(intent);
        if (intentEffects.Count > 0)
        {
            hash = HashCode.Combine(hash, intentEffects.Count);
            foreach (IntentBadge badge in intentEffects)
            {
                hash = HashCode.Combine(hash, (int)badge.Kind, badge.Amount);
            }
        }

        hash = HashCode.Combine(hash, visualState.Effects.Count);
        foreach (DetailedIntentVisualEffect effect in visualState.Effects)
        {
            hash = HashCode.Combine(hash,
                effect.LeftText ?? "",
                effect.RightText ?? "",
                effect.ScopeText ?? "",
                effect.VisualKey ?? "",
                (int)effect.Placement);
        }

        if (visualState.SingleTarget != null)
        {
            hash = HashCode.Combine(
                hash,
                visualState.SingleTarget.Name,
                visualState.SingleTarget.SlotName ?? string.Empty,
                visualState.SingleTarget.IsAlive);
        }

        hash = HashCode.Combine(hash, owner.Name, owner.IsAlive);
        return hash.ToString("X8");
    }

    private static class MonsterHeadTextureCache
    {
        private static readonly Dictionary<Rid, Texture2D> Cache = new();

        public static Texture2D? GetOrCreate(Texture2D source)
        {
            Rid rid = source.GetRid();
            if (Cache.TryGetValue(rid, out Texture2D? cached) && GodotTextureSafety.IsValid(cached))
            {
                return cached;
            }

            Image? image = TryGetImage(source);
            if (image == null)
            {
                return null;
            }

            int width = image.GetWidth();
            int height = image.GetHeight();
            if (width <= 0 || height <= 0)
            {
                return null;
            }

            int cropSize = Math.Min(width, height);
            var cropRect = new Rect2I(
                Math.Max(0, (width - cropSize) / 2),
                0,
                cropSize,
                cropSize);

            ImageTexture result = ImageTexture.CreateFromImage(image.GetRegion(cropRect));
            if (!GodotTextureSafety.IsValid(result))
            {
                return null;
            }

            Cache[rid] = result;
            return result;
        }

        private static Image? TryGetImage(Texture2D texture)
        {
            try
            {
                Image? image = texture.GetImage();
                if (image == null || image.GetWidth() <= 0 || image.GetHeight() <= 0)
                {
                    return null;
                }

                Image copy = (Image)image.Duplicate();
                if (copy.IsCompressed() && copy.Decompress() != Error.Ok)
                {
                    return null;
                }

                if (copy.GetFormat() != Image.Format.Rgba8)
                {
                    copy.Convert(Image.Format.Rgba8);
                }

                return copy;
            }
            catch
            {
                return null;
            }
        }
    }

    private enum PreviewCardAnchor
    {
        Center,
        BottomCenter
    }

}

/// <summary><c>AbstractIntent.GetHoverTip</c> 后缀的两个处理函数，先复合图标、后徽记，由 IntentRenderPipeline 依次调用。</summary>
internal static class BadgedIntentHoverTipPatch
{
    /// <summary>复合意图换上自己的提示图标；换成功后徽记不再改写提示。</summary>
    internal static IntentDecoratorOutcome ApplyCombinedHoverIcon(AbstractIntent intent, ref HoverTip tip)
    {
        if (intent is not ICombinedIntentHoverIcon combinedHoverIcon)
        {
            return IntentDecoratorOutcome.Skipped;
        }

        Texture2D? icon = BadgedIntentHoverTipFactory.ResolveCombinedHoverIcon(combinedHoverIcon);
        if (!GodotTextureSafety.IsValid(icon))
        {
            return IntentDecoratorOutcome.Skipped;
        }

        tip = BadgedIntentHoverTipFactory.ReplaceIcon(combinedHoverIcon, tip, icon);
        return IntentDecoratorOutcome.Handled;
    }

    internal static IntentDecoratorOutcome ApplyBadgeTip(
        AbstractIntent intent,
        IEnumerable<Creature> targets,
        Creature owner,
        ref HoverTip tip)
    {
        IReadOnlyList<IntentBadge> effects = IntentEffectCollection.Get(intent);
        if (intent is IndiscriminateAttackIntent
            || effects.Count == 0)
        {
            return IntentDecoratorOutcome.Skipped;
        }

        tip = BadgedIntentHoverTipFactory.Create(intent, effects, tip, targets, owner);
        return IntentDecoratorOutcome.Applied;
    }
}

internal static class BadgedIntentHoverTipDisplayPatch
{
    internal static bool OnIntentHovered(
        AbstractIntent ____intent,
        IEnumerable<Creature> ____targets,
        Creature ____owner)
    {
        IReadOnlyList<IntentBadge> intentEffects = IntentEffectCollection.Get(____intent);
        bool hasEffectTips = intentEffects.Count > 0;
        bool hasDetailedTips = ____intent is IDetailedIntentVisuals;
        if (!hasEffectTips && !hasDetailedTips)
        {
            return true;
        }

        if (!____intent.HasIntentTip)
        {
            return false;
        }

        List<IHoverTip> hoverTips = new()
        {
            ____intent.GetHoverTip(____targets, ____owner)
        };
        if (hasEffectTips)
        {
            hoverTips.AddRange(BadgedIntentHoverTipFactory.GetExtraHoverTips(intentEffects));
        }

        if (hasDetailedTips)
        {
            hoverTips.AddRange(DetailedIntentHoverTipFactory.GetExtraHoverTips(____intent, ____targets, ____owner));
        }

        hoverTips = IHoverTip.RemoveDupes(hoverTips).ToList();

        CombatQueries.CreatureNodeOf(____owner)?.ShowHoverTips(hoverTips);
        return false;
    }
}
