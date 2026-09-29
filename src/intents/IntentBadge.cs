using System;
using Godot;
using LibraryOfRuina.guests.DawnOffice;
using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.powers;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;

namespace LibraryOfRuina.intents;

public enum IntentBadgeKind
{
    Power,
    StatusCard,
    Heal,
    Summon,
    Custom
}

[Flags]
public enum IntentEffectSemantics
{
    None = 0,
    GroupAttack = 1
}

public sealed class IntentBadge
{
    private readonly int _amount;
    private readonly Func<int>? _amountFactory;
    private readonly Func<PowerModel>? _powerFactory;
    private readonly Func<CardModel>? _cardFactory;
    private readonly Func<IEnumerable<IHoverTip>>? _hoverTipsFactory;
    private readonly IReadOnlyList<string> _assetPaths;

    private IntentBadge(
        IntentBadgeKind kind,
        int amount,
        string iconPath,
        Func<int>? amountFactory = null,
        Type? powerType = null,
        Func<PowerModel>? powerFactory = null,
        Type? cardModelType = null,
        Func<CardModel>? cardFactory = null,
        Func<IEnumerable<IHoverTip>>? hoverTipsFactory = null,
        string? leftText = null,
        string? rightText = null,
        bool isVisible = true,
        IntentEffectSemantics semantics = IntentEffectSemantics.None)
    {
        Kind = kind;
        _amount = amount;
        _amountFactory = amountFactory;
        IconPath = iconPath ?? throw new ArgumentNullException(nameof(iconPath));
        _assetPaths = CreateAssetPaths(iconPath);
        PowerType = powerType;
        _powerFactory = powerFactory;
        CardModelType = cardModelType;
        _cardFactory = cardFactory;
        _hoverTipsFactory = hoverTipsFactory;
        LeftText = leftText;
        RightText = rightText;
        IsVisible = isVisible;
        Semantics = semantics;
    }

    public IntentBadgeKind Kind { get; }

    public Type? PowerType { get; }

    public Type? CardModelType { get; }

    public bool HasPower => _powerFactory != null;

    public bool HasCard => _cardFactory != null;

    public bool HasExtraHoverTips => _hoverTipsFactory != null;

    public bool IsVisible { get; }

    public IntentEffectSemantics Semantics { get; }

    public int Amount
    {
        get
        {
            try
            {
                return _amountFactory?.Invoke() ?? _amount;
            }
            catch
            {
                return _amount;
            }
        }
    }

    public string? LeftText { get; }

    public string? RightText { get; }

    public PowerModel Power =>
        _powerFactory?.Invoke()
        ?? throw new InvalidOperationException("This intent badge is not backed by a PowerModel.");

    public CardModel Card =>
        _cardFactory?.Invoke()
        ?? throw new InvalidOperationException("This intent badge is not backed by a CardModel.");

    public IEnumerable<IHoverTip> ExtraHoverTips => _hoverTipsFactory?.Invoke() ?? Array.Empty<IHoverTip>();

    public string IconPath { get; }

    public IEnumerable<string> AssetPaths => _assetPaths;

    public Texture2D? GetTexture()
    {
        return IntentAssetResolver.GetTexture(AssetPaths, IconPath);
    }

    public IntentBadge WithoutVisual() => new(
        Kind,
        _amount,
        IconPath,
        _amountFactory,
        PowerType,
        _powerFactory,
        CardModelType,
        _cardFactory,
        _hoverTipsFactory,
        LeftText,
        RightText,
        isVisible: false,
        semantics: Semantics);

    public static IntentBadge FromPower<TPower>(int amount = 0, string? upText = null, string? downText = null)
        where TPower : PowerModel
    {
        TPower power = ModelDb.Power<TPower>();
        return new IntentBadge(
            IntentBadgeKind.Power,
            amount,
            GetPowerIconPath(power),
            powerType: typeof(TPower),
            powerFactory: static () => ModelDb.Power<TPower>(),
            hoverTipsFactory: static () => new[] { HoverTipFactory.FromPower<TPower>() },
            leftText: upText,
            rightText: downText);
    }

    public static IntentBadge FromPower<TPower>(Func<int> amountCalc, string? leftText = null, string? rightText = null)
        where TPower : PowerModel
    {
        ArgumentNullException.ThrowIfNull(amountCalc);

        TPower power = ModelDb.Power<TPower>();
        return new IntentBadge(
            IntentBadgeKind.Power,
            0,
            GetPowerIconPath(power),
            amountFactory: amountCalc,
            powerType: typeof(TPower),
            powerFactory: static () => ModelDb.Power<TPower>(),
            hoverTipsFactory: static () => new[] { HoverTipFactory.FromPower<TPower>() },
            leftText: leftText,
            rightText: rightText);
    }

    public static IntentBadge Vulnerable(int amount = 0) => FromPower<VulnerablePower>(amount);

    public static IntentBadge Weak(int amount = 0) => FromPower<WeakPower>(amount);

    public static IntentBadge Frail(int amount = 0) => FromPower<FrailPower>(amount);

    public static IntentBadge Flaw(int amount = 0) => FromPower<LibraryDisarmPower>(amount);

    public static IntentBadge Flaw(int amount, int turns) =>
        FromPower<LibraryDisarmPower>(
            amount,
            turns > 0 ? turns.ToString() : null,
            amount > 0 ? amount.ToString() : null);

    public static IntentBadge Strength(int amount = 0) => FromPower<StrengthPower>(amount);

    public static IntentBadge NextTurnStrength(int amount = 0) => FromPower<LibraryOfRuinaNextTurnStrength>(amount);

    public static IntentBadge NextTurnStrength(Func<int> amountCalc) =>
        FromPower<LibraryOfRuinaNextTurnStrength>(amountCalc);

    public static IntentBadge StrengthDown(int amount = 0) => FromPower<LibraryWeakPower>(amount);

    public static IntentBadge Guard(int amount = 0) => FromPower<LibraryEndurancePower>(amount);

    public static IntentBadge Guard(int amount, int turns) =>
        FromPower<LibraryEndurancePower>(
            amount,
            turns > 0 ? turns.ToString() : null,
            amount > 0 ? amount.ToString() : null);

    public static IntentBadge Bleed(int amount = 0) => FromPower<LibraryBleedingPower>(amount);

    public static IntentBadge Burn(int amount = 0) => FromPower<LibraryBurnPower>(amount);

    public static IntentBadge Bind(int amount = 0) => FromPower<LibraryBindingPower>(amount);

    public static IntentBadge RapidWear(int amount = 0) => FromPower<LibraryVulnerablePower>(amount);

    public static IntentBadge RapidWear(int amount, int turns) =>
        FromPower<LibraryVulnerablePower>(
            amount,
            turns > 0 ? turns.ToString() : null,
            amount > 0 ? amount.ToString() : null);

    public static IntentBadge Confusion(int amount = 0) => FromPower<LibraryOfRuinaConfusionPower>(amount);

    public static IntentBadge StatusCards(int amount = 0) => StatusCard<Dazed>(amount);

    public static IntentBadge StatusCard<TCard>(int amount = 0)
        where TCard : CardModel
    {
        return FromIntentIcon(
            IntentBadgeKind.StatusCard,
            "atlases/intent_atlas.sprites/intent_status_card.tres",
            amount,
            typeof(TCard),
            static () => ModelDb.Card<TCard>(),
            static () => HoverTipFactory.FromCardWithCardHoverTips<TCard>());
    }

    public static IntentBadge StatusCard<TCard>(Func<int> amountCalc)
        where TCard : CardModel
    {
        ArgumentNullException.ThrowIfNull(amountCalc);

        return FromIntentIcon(
            IntentBadgeKind.StatusCard,
            "atlases/intent_atlas.sprites/intent_status_card.tres",
            0,
            typeof(TCard),
            static () => ModelDb.Card<TCard>(),
            static () => HoverTipFactory.FromCardWithCardHoverTips<TCard>(),
            amountCalc);
    }

    public static IntentBadge CardDebuff<TCard>(int amount = 0)
        where TCard : CardModel
    {
        return new IntentBadge(
            IntentBadgeKind.Custom,
            amount,
            ImageHelper.GetImagePath(
                "atlases/intent_atlas.sprites/intent_card_debuff.tres"),
            cardModelType: typeof(TCard),
            cardFactory: static () => ModelDb.Card<TCard>(),
            hoverTipsFactory: static () =>
                HoverTipFactory.FromCardWithCardHoverTips<TCard>());
    }

    public static IntentBadge Heal(int amount = 0) =>
        FromIntentIcon(IntentBadgeKind.Heal, "atlases/intent_atlas.sprites/intent_heal.tres", amount);

    public static IntentBadge Heal(Func<int> amountCalc) =>
        FromIntentIcon(IntentBadgeKind.Heal, "atlases/intent_atlas.sprites/intent_heal.tres", 0, amountFactory: amountCalc);

    public static IntentBadge Summon(int amount = 0) =>
        FromIntentIcon(
            IntentBadgeKind.Summon,
            "atlases/intent_atlas.sprites/intent_summon.tres",
            amount,
            hoverTipsFactory: static () => new[] { HoverTipFactory.Static(StaticHoverTip.SummonStatic) });

    public static IntentBadge Custom(string imagePath, int amount = 0) =>
        new(IntentBadgeKind.Custom, amount, ImageHelper.GetImagePath(imagePath));

    public static IntentBadge Custom(
        string imagePath,
        int amount,
        string? leftText,
        string? rightText,
        IntentEffectSemantics semantics) =>
        new(
            IntentBadgeKind.Custom,
            amount,
            ImageHelper.GetImagePath(imagePath),
            leftText: leftText,
            rightText: rightText,
            semantics: semantics);

    public static IntentBadge Custom(string imagePath, StaticHoverTip hoverTip, int amount = 0) =>
        new(
            IntentBadgeKind.Custom,
            amount,
            ImageHelper.GetImagePath(imagePath),
            hoverTipsFactory: () => new[] { HoverTipFactory.Static(hoverTip) });

    public static IntentBadge Custom(string imagePath, Func<IEnumerable<IHoverTip>> hoverTipsFactory, int amount = 0) =>
        new(
            IntentBadgeKind.Custom,
            amount,
            ImageHelper.GetImagePath(imagePath),
            hoverTipsFactory: hoverTipsFactory);

    public static IntentBadge Custom(
        string imagePath,
        IntentEffectSemantics semantics,
        Func<IEnumerable<IHoverTip>> hoverTipsFactory,
        int amount = 0) =>
        new(
            IntentBadgeKind.Custom,
            amount,
            ImageHelper.GetImagePath(imagePath),
            hoverTipsFactory: hoverTipsFactory,
            semantics: semantics);

    private static IntentBadge FromIntentIcon(
        IntentBadgeKind kind,
        string spritePath,
        int amount,
        Type? cardModelType = null,
        Func<CardModel>? cardFactory = null,
        Func<IEnumerable<IHoverTip>>? hoverTipsFactory = null,
        Func<int>? amountFactory = null)
    {
        return new IntentBadge(
            kind,
            amount,
            ImageHelper.GetImagePath(spritePath),
            amountFactory: amountFactory,
            cardModelType: cardModelType,
            cardFactory: cardFactory,
            hoverTipsFactory: hoverTipsFactory);
    }

    private static string GetPowerIconPath(PowerModel power)
    {
        return PowerIconResolver.TryResolve(power, out ResolvedPowerIcon resolved)
            ? resolved.Path
            : power.ResolvedBigIconPath;
    }

    private static IReadOnlyList<string> CreateAssetPaths(string iconPath)
    {
        if (string.IsNullOrWhiteSpace(iconPath))
        {
            return Array.Empty<string>();
        }

        var paths = new List<string> { iconPath };
        if (TryGetPowerAtlasSpriteName(iconPath, out string? spriteName))
        {
            AddIfMissing(paths, ImageHelper.GetImagePath("powers/" + spriteName + ".png"));
            AddIfMissing(paths, ImageHelper.GetImagePath("powers/beta/" + spriteName + ".png"));
        }

        return paths;
    }

    private static bool TryGetPowerAtlasSpriteName(string path, out string? spriteName)
    {
        const string atlasPrefix = "res://images/atlases/power_atlas.sprites/";
        const string suffix = ".tres";

        spriteName = null;
        if (!path.StartsWith(atlasPrefix, StringComparison.Ordinal) || !path.EndsWith(suffix, StringComparison.Ordinal))
        {
            return false;
        }

        spriteName = path.Substring(atlasPrefix.Length, path.Length - atlasPrefix.Length - suffix.Length);
        return !string.IsNullOrWhiteSpace(spriteName);
    }

    private static void AddIfMissing(List<string> paths, string path)
    {
        if (!paths.Contains(path))
        {
            paths.Add(path);
        }
    }
}

internal static class IntentAssetResolver
{
    public static Texture2D? GetTexture(IEnumerable<string> assetPaths, string? fallbackPath)
    {
        foreach (string assetPath in assetPaths)
        {
            if (string.IsNullOrWhiteSpace(assetPath) || !PreloadManager.Cache.ContainsKey(assetPath))
            {
                continue;
            }

            return PreloadManager.Cache.GetTexture2D(assetPath);
        }

        return string.IsNullOrWhiteSpace(fallbackPath)
            ? null
            : PreloadManager.Cache.GetTexture2D(fallbackPath);
    }
}
