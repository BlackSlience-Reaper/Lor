using System;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace LibraryOfRuina.intents;

public interface IDetailedIntentVisuals
{
    DetailedIntentVisualState GetDetailedIntentVisuals(IEnumerable<Creature> targets, Creature owner);
}

public sealed class DetailedIntentVisualState
{
    public static readonly DetailedIntentVisualState Empty = new(Array.Empty<DetailedIntentVisualEffect>());

    public DetailedIntentVisualState(
        IReadOnlyList<DetailedIntentVisualEffect> effects,
        Creature? singleTarget = null)
    {
        Effects = effects;
        SingleTarget = singleTarget;
    }

    public IReadOnlyList<DetailedIntentVisualEffect> Effects { get; }

    public Creature? SingleTarget { get; }
}

public enum DetailedBuffTargetScope
{
    Self,
    Target,
    AllEnemies,
    OtherEnemies,
    RandomEnemy
}

public enum DetailedIntentEffectPlacement
{
    AboveMain,
    BelowMain,
    FloatingCardAboveIntent
}

public enum DetailedIntentEffectKind
{
    Buff,
    Debuff,
    Erosion,
    StatusCard,
    SpecialCard,
    Custom
}

public static class DetailedIntentScopeText
{
    public const string Self = "->自身";
    public const string Target = "->目标";
    public const string AllEnemies = "->所有敌人";
    public const string OtherEnemies = "->其他敌人";
    public const string RandomEnemy = "->随机敌人";

    public static string? Normalize(string? scopeText)
    {
        if (string.IsNullOrWhiteSpace(scopeText))
        {
            return null;
        }

        string text = scopeText.Trim();
        if (text.StartsWith("->", StringComparison.Ordinal))
        {
            text = "->" + text.Substring(2).TrimStart();
        }

        return text == Self ? null : text;
    }
}

public sealed class DetailedIntentVisualEffect
{
    public DetailedIntentEffectPlacement Placement { get; init; }

    public DetailedIntentEffectKind Kind { get; init; }

    public string? IconPath { get; init; }

    public IReadOnlyList<string>? ExtraAssetPaths { get; init; }

    public Func<Texture2D?>? TextureFactory { get; init; }

    public Func<CardModel>? CardFactory { get; init; }

    public bool UseSmallCard { get; init; }

    public bool UseFloatingPreviewCard { get; init; }

    public bool UseCardIntentBadge { get; init; }

    public float? FloatingPreviewScale { get; init; }

    public float? FloatingPreviewHoverScale { get; init; }

    public float? FloatingPreviewVerticalOffset { get; init; }

    public float? FloatingPreviewStackOffset { get; init; }

    public string? LeftText { get; init; }

    public string? RightText { get; init; }

    public string? RightIconPath { get; init; }

    public string? ScopeText { get; init; }

    public bool UseCornerNumberLayout { get; init; }

    public string? TopRightText { get; init; }

    public string? BottomRightText { get; init; }

    public Func<IEnumerable<IHoverTip>>? HoverTipsFactory { get; init; }

    public string? VisualKey { get; init; }

    public bool IsDecorativeOnly =>
        Kind == DetailedIntentEffectKind.Custom
        && HoverTipsFactory == null
        && string.IsNullOrWhiteSpace(LeftText)
        && string.IsNullOrWhiteSpace(RightText)
        && string.IsNullOrWhiteSpace(RightIconPath)
        && string.IsNullOrWhiteSpace(ScopeText)
        && !UseFloatingPreviewCard
        && !UseCardIntentBadge;

    public IEnumerable<string> AssetPaths
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(IconPath))
            {
                yield return IconPath;
            }

            if (ExtraAssetPaths != null)
            {
                foreach (string path in ExtraAssetPaths)
                {
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        yield return path;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(RightIconPath))
            {
                yield return RightIconPath;
            }
        }
    }

    public Texture2D? GetTexture()
    {
        if (TextureFactory != null)
        {
            return TextureFactory();
        }

        return IntentAssetResolver.GetTexture(AssetPaths, IconPath);
    }

    public Texture2D? GetRightIconTexture()
    {
        return IntentAssetResolver.GetTexture(
            string.IsNullOrWhiteSpace(RightIconPath)
                ? Array.Empty<string>()
                : new[] { RightIconPath },
            RightIconPath);
    }

    public IEnumerable<IHoverTip> GetHoverTips(CardModel? card)
    {
        if (HoverTipsFactory != null)
        {
            return HoverTipsFactory();
        }

        if (card != null)
        {
            return new[] { HoverTipFactory.FromCard(card) }.Concat(card.HoverTips);
        }

        return Array.Empty<IHoverTip>();
    }

    public static DetailedIntentVisualEffect FromBadge(IntentBadge badge, string? scopeText = null)
    {
        DetailedIntentEffectKind kind = GetBadgeKind(badge);

        bool isStatusCard = badge.Kind == IntentBadgeKind.StatusCard && badge.HasCard;
        bool useCornerNumbers = badge.Kind == IntentBadgeKind.Power;
        bool hideNumbers = badge.Kind == IntentBadgeKind.Heal;
        return new DetailedIntentVisualEffect
        {
            Placement = DetailedIntentEffectPlacement.AboveMain,
            Kind = kind,
            IconPath = isStatusCard ? null : badge.IconPath,
            ExtraAssetPaths = isStatusCard ? null : badge.AssetPaths.ToArray(),
            CardFactory = isStatusCard ? () => (CardModel)badge.Card.ToMutable() : null,
            UseCardIntentBadge = isStatusCard,
            VisualKey = isStatusCard ? badge.Card.Id.ToString() : badge.IconPath,
            LeftText = useCornerNumbers || hideNumbers ? null : GetBadgeLeftText(badge, isStatusCard),
            RightText = useCornerNumbers || hideNumbers ? null : GetBadgeRightText(badge, isStatusCard),
            TopRightText = useCornerNumbers ? badge.LeftText : null,
            BottomRightText = useCornerNumbers ? GetBadgeRightText(badge, isStatusCard) : null,
            UseCornerNumberLayout = useCornerNumbers,
            ScopeText = DetailedIntentScopeText.Normalize(scopeText),
            HoverTipsFactory = badge.HasExtraHoverTips ? () => badge.ExtraHoverTips : null
        };
    }

    private static string? GetBadgeLeftText(IntentBadge badge, bool isStatusCard)
    {
        if (!string.IsNullOrWhiteSpace(badge.LeftText))
        {
            return badge.LeftText;
        }

        return isStatusCard && badge.Amount > 0 ? badge.Amount.ToString() : null;
    }

    private static string? GetBadgeRightText(IntentBadge badge, bool isStatusCard)
    {
        if (!string.IsNullOrWhiteSpace(badge.RightText))
        {
            return badge.RightText;
        }

        return !isStatusCard && badge.Amount != 0 ? badge.Amount.ToString() : null;
    }

    public static DetailedIntentVisualEffect StatusCard<TCard>(
        int count,
        PileType pileType,
        string? scopeText = null)
        where TCard : CardModel
    {
        return new DetailedIntentVisualEffect
        {
            Placement = DetailedIntentEffectPlacement.AboveMain,
            Kind = DetailedIntentEffectKind.StatusCard,
            ExtraAssetPaths = ModelDb.Card<TCard>().AllPortraitPaths.ToArray(),
            CardFactory = static () => (CardModel)ModelDb.Card<TCard>().ToMutable(),
            UseCardIntentBadge = true,
            LeftText = count > 0 ? count.ToString() : null,
            RightIconPath = DetailedIntentPileIcons.GetIconPath(pileType),
            ScopeText = DetailedIntentScopeText.Normalize(scopeText),
            HoverTipsFactory = static () => HoverTipFactory.FromCardWithCardHoverTips<TCard>(),
            VisualKey = typeof(TCard).FullName
        };
    }

    public static DetailedIntentVisualEffect StatusCard<TCard>(
        int count,
        PileType pileType,
        string? scopeText,
        DetailedIntentEffectPlacement placement)
        where TCard : CardModel
    {
        DetailedIntentVisualEffect effect = StatusCard<TCard>(count, pileType, scopeText);
        return new DetailedIntentVisualEffect
        {
            Placement = placement,
            Kind = effect.Kind,
            IconPath = effect.IconPath,
            ExtraAssetPaths = effect.ExtraAssetPaths,
            TextureFactory = effect.TextureFactory,
            CardFactory = effect.CardFactory,
            UseSmallCard = effect.UseSmallCard,
            UseFloatingPreviewCard = effect.UseFloatingPreviewCard,
            UseCardIntentBadge = effect.UseCardIntentBadge,
            FloatingPreviewScale = effect.FloatingPreviewScale,
            FloatingPreviewHoverScale = effect.FloatingPreviewHoverScale,
            FloatingPreviewVerticalOffset = effect.FloatingPreviewVerticalOffset,
            FloatingPreviewStackOffset = effect.FloatingPreviewStackOffset,
            LeftText = effect.LeftText,
            RightText = effect.RightText,
            RightIconPath = effect.RightIconPath,
            ScopeText = effect.ScopeText,
            UseCornerNumberLayout = effect.UseCornerNumberLayout,
            TopRightText = effect.TopRightText,
            BottomRightText = effect.BottomRightText,
            HoverTipsFactory = effect.HoverTipsFactory,
            VisualKey = effect.VisualKey
        };
    }

    private static DetailedIntentEffectKind GetBadgeKind(IntentBadge badge)
    {
        if (badge.HasPower)
        {
            PowerType type = badge.Power.GetTypeForAmount(badge.Amount);
            return type == PowerType.Buff
                ? DetailedIntentEffectKind.Buff
                : DetailedIntentEffectKind.Debuff;
        }

        return badge.Kind switch
        {
            IntentBadgeKind.StatusCard => DetailedIntentEffectKind.StatusCard,
            IntentBadgeKind.Heal or IntentBadgeKind.Summon => DetailedIntentEffectKind.Buff,
            _ => DetailedIntentEffectKind.Custom
        };
    }
}

internal static class DetailedIntentPileIcons
{
    public static string? GetIconPath(PileType pileType)
    {
        return pileType switch
        {
            PileType.Hand => "res://images/packed/sprite_fonts/card_icon.png",
            PileType.Draw => "res://images/packed/combat_ui/draw_pile.png",
            PileType.Discard => "res://images/packed/combat_ui/discard_pile.png",
            PileType.Exhaust => "res://images/packed/combat_ui/exhaust_pile.png",
            PileType.Deck => ImageHelper.GetImagePath("atlases/ui_atlas.sprites/top_bar/top_bar_deck.tres"),
            _ => null
        };
    }

    public static IEnumerable<string> GetAssetPaths(PileType pileType)
    {
        string? path = GetIconPath(pileType);
        return string.IsNullOrWhiteSpace(path) ? Array.Empty<string>() : new[] { path };
    }
}

public class DetailedStatusCardIntent<TCard> : StatusIntent, IDetailedIntentVisuals
    where TCard : CardModel
{
    private readonly PileType _pileType;
    private readonly string? _scopeText;
    private readonly bool _showSingleTargetMarker;

    public DetailedStatusCardIntent(
        int count,
        PileType pileType,
        string? scopeText = null,
        bool showSingleTargetMarker = true)
        : base(count)
    {
        _pileType = pileType;
        _scopeText = scopeText;
        _showSingleTargetMarker = showSingleTargetMarker;
    }

    public override IEnumerable<string> AssetPaths =>
        base.AssetPaths
            .Concat(ModelDb.Card<TCard>().AllPortraitPaths)
            .Concat(DetailedIntentPileIcons.GetAssetPaths(_pileType));

    public DetailedIntentVisualState GetDetailedIntentVisuals(IEnumerable<Creature> targets, Creature owner)
    {
        IReadOnlyList<Creature> targetList = targets
            .Where(static target => target is { IsAlive: true })
            .Distinct()
            .ToArray();

        Creature? singleTarget = _showSingleTargetMarker && targetList.Count == 1 ? targetList[0] : null;
        string? scopeText = _scopeText ?? (singleTarget != null
            ? GetSingleTargetScope(singleTarget, owner)
            : GetGroupScope(targetList));

        return new DetailedIntentVisualState(
            new[]
            {
                DetailedIntentVisualEffect.StatusCard<TCard>(CardCount, _pileType, scopeText)
            },
            singleTarget);
    }

    private static string? GetSingleTargetScope(Creature target, Creature owner)
    {
        return null;
    }

    private static string? GetGroupScope(IReadOnlyList<Creature> targets)
    {
        if (targets.Count > 0 && targets.All(static target => target.IsMonster))
        {
            return DetailedIntentScopeText.OtherEnemies;
        }

        return null;
    }
}

public sealed class TargetedDetailedStatusCardIntent<TCard> :
    DetailedStatusCardIntent<TCard>,
    ITargetedIntentIndicator,
    IIntentTargetLineProvider
    where TCard : CardModel
{
    private readonly string _descriptionKey;
    private readonly Func<Creature, IReadOnlyList<Creature>> _targetResolver;

    public TargetedDetailedStatusCardIntent(
        int count,
        PileType pileType,
        string descriptionKey,
        Func<Creature, IReadOnlyList<Creature>> targetResolver,
        string? scopeText = null)
        : base(
            count,
            pileType,
            scopeText,
            showSingleTargetMarker: false)
    {
        _descriptionKey = descriptionKey;
        _targetResolver = targetResolver
            ?? throw new ArgumentNullException(nameof(targetResolver));
    }

    protected override LocString GetIntentDescription(
        IEnumerable<Creature> targets,
        Creature owner)
    {
        LocString description = new("intents", _descriptionKey);
        description.Add("CardCount", CardCount);
        return description;
    }

    public IReadOnlyList<IntentTargetLineTarget> GetIntentTargetLineTargets(
        Creature owner,
        IReadOnlyList<Creature>? fallbackTargets) =>
        _targetResolver(owner)
            .Where(static target => target.IsAlive)
            .Distinct()
            .Select(static target =>
                new IntentTargetLineTarget(target, "GroupStatusCard"))
            .ToArray();
}

public sealed class DynamicDetailedStatusCardIntent<TCard>
    : AbstractIntent,
        IDetailedIntentVisuals
    where TCard : CardModel
{
    private readonly Func<int> _countFactory;
    private readonly PileType _pileType;
    private readonly string? _scopeText;

    public DynamicDetailedStatusCardIntent(
        Func<int> countFactory,
        PileType pileType,
        string? scopeText = null)
    {
        _countFactory = countFactory
            ?? throw new ArgumentNullException(nameof(countFactory));
        _pileType = pileType;
        _scopeText = scopeText;
    }

    private int CardCount => Math.Max(0, _countFactory());

    public override IntentType IntentType => IntentType.StatusCard;

    protected override LocString IntentLabelFormat =>
        new("intents", "FORMAT_STATUS_CARD_COUNT");

    protected override string IntentPrefix => "STATUS";

    protected override string SpritePath =>
        "atlases/intent_atlas.sprites/intent_status_card.tres";

    public override IEnumerable<string> AssetPaths =>
        base.AssetPaths
            .Concat(ModelDb.Card<TCard>().AllPortraitPaths)
            .Concat(DetailedIntentPileIcons.GetAssetPaths(_pileType));

    public override LocString GetIntentLabel(
        IEnumerable<Creature> targets,
        Creature owner)
    {
        LocString label = IntentLabelFormat;
        label.Add("CardCount", CardCount);
        return label;
    }

    protected override LocString GetIntentDescription(
        IEnumerable<Creature> targets,
        Creature owner)
    {
        LocString description = base.GetIntentDescription(targets, owner);
        description.Add("CardCount", CardCount);
        return description;
    }

    public DetailedIntentVisualState GetDetailedIntentVisuals(
        IEnumerable<Creature> targets,
        Creature owner) =>
        new(
            [
                DetailedIntentVisualEffect.StatusCard<TCard>(
                    CardCount,
                    _pileType,
                    _scopeText)
            ]);
}

public sealed class DetailedSplitStatusCardIntent<TCard> : StatusIntent, IDetailedIntentVisuals
    where TCard : CardModel
{
    private readonly IReadOnlyList<(int Count, PileType PileType)> _entries;
    private readonly string? _scopeText;
    private readonly string? _descriptionKey;
    private readonly IReadOnlyDictionary<string, decimal>? _descriptionVars;

    public DetailedSplitStatusCardIntent(
        IReadOnlyList<(int Count, PileType PileType)> entries,
        string? scopeText = null,
        string? descriptionKey = null,
        IReadOnlyDictionary<string, decimal>? descriptionVars = null)
        : base(entries?.Sum(static entry => Math.Max(0, entry.Count)) ?? 0)
    {
        _entries = entries?.Where(static entry => entry.Count > 0).ToArray()
            ?? throw new ArgumentNullException(nameof(entries));
        _scopeText = scopeText;
        _descriptionKey = descriptionKey;
        _descriptionVars = descriptionVars;
    }

    public override IEnumerable<string> AssetPaths =>
        base.AssetPaths
            .Concat(ModelDb.Card<TCard>().AllPortraitPaths)
            .Concat(_entries.SelectMany(static entry => DetailedIntentPileIcons.GetAssetPaths(entry.PileType)));

    public DetailedIntentVisualState GetDetailedIntentVisuals(IEnumerable<Creature> targets, Creature owner)
    {
        IReadOnlyList<Creature> targetList = targets
            .Where(static target => target is { IsAlive: true })
            .Distinct()
            .ToArray();

        Creature? singleTarget = targetList.Count == 1 ? targetList[0] : null;
        string? scopeText = _scopeText ?? (singleTarget != null
            ? null
            : GetGroupScope(targetList));

        return new DetailedIntentVisualState(
            _entries
                .Select(entry => DetailedIntentVisualEffect.StatusCard<TCard>(
                    entry.Count,
                    entry.PileType,
                    scopeText,
                    DetailedIntentEffectPlacement.AboveMain))
                .ToArray(),
            singleTarget);
    }

    protected override LocString GetIntentDescription(IEnumerable<Creature> targets, Creature owner)
    {
        if (string.IsNullOrWhiteSpace(_descriptionKey))
        {
            return base.GetIntentDescription(targets, owner);
        }

        LocString desc = new("intents", _descriptionKey);
        desc.Add("CardCount", CardCount);
        desc.Add("CardName", ModelDb.Card<TCard>().Title);
        if (_descriptionVars != null)
        {
            foreach ((string key, decimal value) in _descriptionVars)
            {
                desc.Add(key, value);
            }
        }

        return desc;
    }

    private static string? GetGroupScope(IReadOnlyList<Creature> targets)
    {
        if (targets.Count > 0 && targets.All(static target => target.IsMonster))
        {
            return DetailedIntentScopeText.OtherEnemies;
        }

        return null;
    }
}

internal static class DetailedIntentHoverTipFactory
{
    public static IEnumerable<IHoverTip> GetExtraHoverTips(
        AbstractIntent intent,
        IEnumerable<Creature> targets,
        Creature owner)
    {
        if (intent is not IDetailedIntentVisuals detailed)
        {
            return Array.Empty<IHoverTip>();
        }

        DetailedIntentVisualState state = detailed.GetDetailedIntentVisuals(targets, owner);
        return IHoverTip.RemoveDupes(state.Effects.SelectMany(GetEffectHoverTips));
    }

    private static IEnumerable<IHoverTip> GetEffectHoverTips(DetailedIntentVisualEffect effect)
    {
        CardModel? card = null;
        if (effect.CardFactory != null)
        {
            try
            {
                card = effect.CardFactory();
            }
            catch
            {
                card = null;
            }
        }

        return IHoverTip.RemoveDupes(
        effect.GetHoverTips(card).Where(static tip => tip is not CardHoverTip));

    }
}
