using System;
using System.Linq;
using MegaCrit.Sts2.Core.Helpers;

namespace LibraryOfRuina.framework.intents;

/// <summary>
/// Frame paths for STS2 attack icons composited with STS1 intent particle VFX.
/// Attack variants are baked per damage tier (1-5); other kinds use a single set.
/// </summary>
internal static class CombinedIntentAnimData
{
    public const string AttackDebuff = "attack_debuff";
    public const string AttackBuff = "attack_buff";
    public const string AttackDefend = "attack_defend";
    public const string CounterAttackDebuff = "counter_attack_debuff";
    public const string CounterAttackBuff = "counter_attack_buff";
    public const string CounterAttackDefend = "counter_attack_defend";
    public const string DefendBuff = "defend_buff";
    public const string DefendDebuff = "defend_debuff";
    public const string CounterDefendBuff = "counter_defend_buff";
    public const string CounterDefendDebuff = "counter_defend_debuff";
    public const string Magic = "magic";
    public const string MagicLarge = "magicL";

    internal const int FrameCount = 30;

    private static readonly HashSet<string> AttackKinds = new(StringComparer.Ordinal)
    {
        AttackDebuff,
        AttackBuff,
        AttackDefend,
        CounterAttackDebuff,
        CounterAttackBuff,
        CounterAttackDefend
    };

    private static readonly HashSet<string> SingleFrameKinds = new(StringComparer.Ordinal)
    {
        DefendBuff,
        DefendDebuff,
        CounterDefendBuff,
        CounterDefendDebuff
    };

    private static readonly HashSet<string> StaticIconKinds = new(StringComparer.Ordinal)
    {
        Magic,
        MagicLarge
    };

    private static readonly HashSet<string> AllKinds = AttackKinds
        .Concat(SingleFrameKinds)
        .Concat(StaticIconKinds)
        .ToHashSet(StringComparer.Ordinal);

    public static IEnumerable<string> HoverIconPaths =>
        AllKinds.Select(GetHoverIconPath);

    public static IEnumerable<string> AssetPaths =>
        AttackKinds.SelectMany(kind => Enumerable.Range(1, 5).SelectMany(tier => GetFramePaths(kind, tier)))
            .Concat(SingleFrameKinds.SelectMany(kind => GetFramePaths(kind, tier: null)))
            .Concat(GetAllIconPaths())
            .Concat(HoverIconPaths);

    public static int GetAttackTier(int totalDamage)
    {
        if (totalDamage < 5)
        {
            return 1;
        }

        if (totalDamage < 10)
        {
            return 2;
        }

        if (totalDamage < 20)
        {
            return 3;
        }

        return totalDamage < 40 ? 4 : 5;
    }

    public static string GetAttackAnimationKey(string kind, int tier) =>
        $"combined_{kind}_{Math.Clamp(tier, 1, 5)}";

    public static string GetAnimationKey(string kind, int? tier = null) =>
        tier.HasValue ? GetAttackAnimationKey(kind, tier.Value) : $"combined_{kind}";

    public static IEnumerable<string> GetAssetPaths(string kind, int? tier = null)
    {
        if (!AllKinds.Contains(kind))
        {
            return Array.Empty<string>();
        }

        if (tier.HasValue)
        {
            return GetFramePaths(kind, tier).Append(GetIconPath(kind, tier));
        }

        if (AttackKinds.Contains(kind))
        {
            return Enumerable.Range(1, 5)
                .SelectMany(t => GetFramePaths(kind, t))
                .Concat(Enumerable.Range(1, 5).Select(t => GetIconPath(kind, t)));
        }

        if (StaticIconKinds.Contains(kind))
        {
            return [GetIconPath(kind, tier: null)];
        }

        return GetFramePaths(kind, tier: null).Append(GetIconPath(kind, tier: null));
    }

    public static string GetIconPath(string kind, int? tier)
    {
        if (tier.HasValue)
        {
            return ImageHelper.GetImagePath($"intents/combined/icons/{kind}_{tier}.png");
        }

        return ImageHelper.GetImagePath($"intents/combined/icons/{kind}.png");
    }

    public static string GetHoverIconPath(string kind)
    {
        return ImageHelper.GetImagePath($"intents/combined/hover_icons/{kind}.png");
    }

    public static bool TryGetAnimationFrame(string animationKey, int frame, out string path)
    {
        path = string.Empty;
        if (!animationKey.StartsWith("combined_", StringComparison.Ordinal))
        {
            return false;
        }

        string body = animationKey["combined_".Length..];
        int lastUnderscore = body.LastIndexOf('_');
        if (lastUnderscore <= 0)
        {
            return TryGetSingleKindFrame(body, frame, out path);
        }

        string maybeTier = body[(lastUnderscore + 1)..];
        if (int.TryParse(maybeTier, out int tier) && tier is >= 1 and <= 5)
        {
            string kind = body[..lastUnderscore];
            if (AttackKinds.Contains(kind))
            {
                path = BuildFramePath(kind, tier, frame % FrameCount);
                return true;
            }
        }

        return TryGetSingleKindFrame(body, frame, out path);
    }

    private static bool TryGetSingleKindFrame(string kind, int frame, out string path)
    {
        if (StaticIconKinds.Contains(kind))
        {
            path = GetIconPath(kind, tier: null);
            return true;
        }

        if (!SingleFrameKinds.Contains(kind))
        {
            path = string.Empty;
            return false;
        }

        path = BuildFramePath(kind, tier: null, frame % FrameCount);
        return true;
    }

    private static IEnumerable<string> GetFramePaths(string kind, int? tier)
    {
        if (tier.HasValue)
        {
            return Enumerable.Range(0, FrameCount).Select(f => BuildFramePath(kind, tier, f));
        }

        return Enumerable.Range(0, FrameCount).Select(f => BuildFramePath(kind, tier: null, f));
    }

    private static IEnumerable<string> GetAllIconPaths()
    {
        foreach (string kind in AttackKinds)
        {
            for (int tier = 1; tier <= 5; tier++)
            {
                yield return GetIconPath(kind, tier);
            }
        }

        foreach (string kind in SingleFrameKinds)
        {
            yield return GetIconPath(kind, tier: null);
        }

        yield return GetIconPath(Magic, tier: null);
        yield return GetIconPath(MagicLarge, tier: null);
    }

    private static string BuildFramePath(string kind, int? tier, int frame)
    {
        string suffix = tier.HasValue ? $"_{tier}" : string.Empty;
        string frameKey = $"combined_{kind}{suffix}_{frame:00}.png";
        string folder = tier.HasValue
            ? $"intents/combined/frames/{kind}/{tier}"
            : $"intents/combined/frames/{kind}";
        return ImageHelper.GetImagePath($"{folder}/{frameKey}");
    }
}
