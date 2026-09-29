using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace LibraryOfRuina.intents;

public interface IBadgedIntent
{
    IntentBadge Badge { get; }

    IReadOnlyList<IntentBadge> Badges { get; }
}

public interface IIntentEffectProvider
{
    IReadOnlyList<IntentBadge> Effects { get; }
}

public interface IGroupAttackIntent
{
    bool IsGroupAttack { get; }
}

internal static class IntentEffectCollection
{
    public static IReadOnlyList<IntentBadge> Create(IntentBadge[]? effects)
    {
        if (effects == null || effects.Length == 0)
        {
            return Array.Empty<IntentBadge>();
        }

        return effects
            .Select(effect =>
                effect ?? throw new ArgumentNullException(
                    nameof(effects),
                    "Intent effect entries cannot be null."))
            .ToArray();
    }

    public static IReadOnlyList<IntentBadge> Get(AbstractIntent intent)
    {
        return intent switch
        {
            IIntentEffectProvider provider => provider.Effects,
            IBadgedIntent badged => badged.Badges,
            _ => Array.Empty<IntentBadge>()
        };
    }

    public static bool HasGroupAttack(IReadOnlyList<IntentBadge> effects)
    {
        return effects.Any(static effect =>
            (effect.Semantics & IntentEffectSemantics.GroupAttack) != 0);
    }
}

public sealed class BadgedAttackIntent : AttackIntent, IBadgedIntent
{
    private readonly Func<int> _repeatCalc;
    private readonly string? _descriptionKey;

    public BadgedAttackIntent(int damage, IntentBadge badge, string? descriptionKey = null)
        : this(() => damage, () => 1, descriptionKey, badge)
    {
    }

    public BadgedAttackIntent(int damage, int repeats, IntentBadge badge, string? descriptionKey = null)
        : this(() => damage, () => repeats, descriptionKey, badge)
    {
    }

    public BadgedAttackIntent(int damage, string? descriptionKey, params IntentBadge[] badges)
        : this(() => damage, () => 1, descriptionKey, badges)
    {
    }

    public BadgedAttackIntent(int damage, int repeats, string? descriptionKey, params IntentBadge[] badges)
        : this(() => damage, () => repeats, descriptionKey, badges)
    {
    }

    public BadgedAttackIntent(Func<decimal> damageCalc, Func<int>? repeatCalc, IntentBadge badge, string? descriptionKey = null)
        : this(damageCalc, repeatCalc, descriptionKey, badge)
    {
    }

    public BadgedAttackIntent(
        Func<decimal> damageCalc,
        Func<int>? repeatCalc,
        string? descriptionKey,
        params IntentBadge[] badges)
    {
        DamageCalc = damageCalc ?? throw new ArgumentNullException(nameof(damageCalc));
        _repeatCalc = repeatCalc ?? (() => 1);
        Badges = BadgedIntentBadgeList.Create(badges);
        _descriptionKey = descriptionKey;
    }

    public IntentBadge Badge => Badges[0];

    public IReadOnlyList<IntentBadge> Badges { get; }

    public override int Repeats => Math.Max(1, _repeatCalc());

    public override IEnumerable<string> AssetPaths => base.AssetPaths.Concat(Badges.SelectMany(badge => badge.AssetPaths));

    protected override string IntentPrefix => BadgedIntentTitle.GetPrefix(Badges, BadgedIntentMainIntent.Attack);

    protected override LocString IntentLabelFormat =>
        Repeats > 1
            ? new LocString("intents", "FORMAT_DAMAGE_MULTI")
            : new LocString("intents", "FORMAT_DAMAGE_SINGLE");

    public override int GetTotalDamage(IEnumerable<Creature> targets, Creature owner)
    {
        return GetSingleDamage(targets, owner) * Repeats;
    }

    public override string GetAnimation(IEnumerable<Creature> targets, Creature owner)
    {
        return BadgedIntentAnimation.GetAttackAnimation(GetTotalDamage(targets, owner));
    }

    public override LocString GetIntentLabel(IEnumerable<Creature> targets, Creature owner)
    {
        LocString fmt = IntentLabelFormat;
        fmt.Add("Damage", GetSingleDamage(targets, owner));
        if (Repeats > 1)
        {
            fmt.Add("Repeat", Repeats);
        }

        return fmt;
    }

    protected override LocString GetIntentDescription(IEnumerable<Creature> targets, Creature owner)
    {
        LocString desc = BadgedIntentDescription.Create(_descriptionKey, owner, "ATTACK");
        desc.Add("Damage", GetSingleDamage(targets, owner));
        desc.Add("Repeat", Repeats);
        BadgedIntentDescription.AddBadgeVariables(desc, Badges);
        return desc;
    }

    internal bool TryCreateCombinedDisplayIntent([NotNullWhen(true)] out AbstractIntent? combined)
    {
        combined = null;
        if (!BadgedIntentBadgeClassifier.TryGetAttackKind(Badges, out string? kind))
        {
            return false;
        }

        IntentBadge[] badges = Badges.ToArray();
        combined = kind == CombinedIntentAnimData.AttackDebuff
            ? new CombinedAttackDebuffIntent(DamageCalc ?? (() => 0m), _repeatCalc, _descriptionKey, badges)
            : new CombinedAttackBuffIntent(DamageCalc ?? (() => 0m), _repeatCalc, _descriptionKey, badges);
        return true;
    }
}

public sealed class BadgedTargetedAttackIntent :
    AttackIntent,
    IBadgedIntent,
    ITargetedIntentIndicator,
    IIntentTargetLineProvider,
    IUsesVanillaPlayerTargetIntentVisual
{
    private readonly Func<int> _repeatCalc;
    private readonly string? _descriptionKey;
    private readonly string? _playerTargetDescriptionKey;
    private readonly Func<Creature, Creature?>? _targetResolver;

    public BadgedTargetedAttackIntent(int damage, int repeats, IntentBadge badge, string? descriptionKey = null)
        : this(() => damage, () => repeats, descriptionKey, badge)
    {
    }

    public BadgedTargetedAttackIntent(int damage, int repeats, string? descriptionKey, params IntentBadge[] badges)
        : this(() => damage, () => repeats, descriptionKey, badges)
    {
    }

    public BadgedTargetedAttackIntent(Func<int> damageCalc, Func<int>? repeatCalc, IntentBadge badge, string? descriptionKey = null)
        : this(damageCalc, repeatCalc, descriptionKey, badge)
    {
    }

    public BadgedTargetedAttackIntent(
        Func<int> damageCalc,
        Func<int>? repeatCalc,
        string? descriptionKey,
        params IntentBadge[] badges)
        : this(
            damageCalc,
            repeatCalc,
            descriptionKey,
            playerTargetDescriptionKey: null,
            useVanillaPlayerTargetIntentVisual: false,
            badges)
    {
    }

    public BadgedTargetedAttackIntent(
        Func<int> damageCalc,
        Func<int>? repeatCalc,
        string? descriptionKey,
        string? playerTargetDescriptionKey,
        bool useVanillaPlayerTargetIntentVisual,
        params IntentBadge[] badges)
        : this(
            damageCalc,
            repeatCalc,
            descriptionKey,
            playerTargetDescriptionKey,
            useVanillaPlayerTargetIntentVisual,
            targetResolver: null,
            badges)
    {
    }

    public BadgedTargetedAttackIntent(
        Func<int> damageCalc,
        Func<int>? repeatCalc,
        string? descriptionKey,
        string? playerTargetDescriptionKey,
        bool useVanillaPlayerTargetIntentVisual,
        Func<Creature, Creature?>? targetResolver,
        params IntentBadge[] badges)
    {
        ArgumentNullException.ThrowIfNull(damageCalc);
        DamageCalc = () => damageCalc();
        _repeatCalc = repeatCalc ?? (() => 1);
        Badges = BadgedIntentBadgeList.Create(badges);
        _descriptionKey = descriptionKey;
        _playerTargetDescriptionKey = playerTargetDescriptionKey;
        _targetResolver = targetResolver;
        UsesVanillaPlayerTargetIntentVisual = useVanillaPlayerTargetIntentVisual;
    }

    public IntentBadge Badge => Badges[0];

    public IReadOnlyList<IntentBadge> Badges { get; }

    public bool UsesVanillaPlayerTargetIntentVisual { get; }

    public override int Repeats => Math.Max(1, _repeatCalc());

    public override IEnumerable<string> AssetPaths => base.AssetPaths.Concat(Badges.SelectMany(badge => badge.AssetPaths));

    protected override string IntentPrefix => BadgedIntentTitle.GetPrefix(Badges, BadgedIntentMainIntent.Attack);

    protected override LocString IntentLabelFormat =>
        Repeats > 1
            ? new LocString("intents", "FORMAT_DAMAGE_MULTI")
            : new LocString("intents", "FORMAT_DAMAGE_SINGLE");

    public override Texture2D GetTexture(IEnumerable<Creature> targets, Creature owner)
    {
        return base.GetTexture(targets, owner);
    }

    public override int GetTotalDamage(IEnumerable<Creature> targets, Creature owner)
    {
        return GetTargetedSingleDamage(targets, owner) * Repeats;
    }

    public override string GetAnimation(IEnumerable<Creature> targets, Creature owner)
    {
        return BadgedIntentAnimation.GetAttackAnimation(GetTotalDamage(targets, owner));
    }

    public override LocString GetIntentLabel(IEnumerable<Creature> targets, Creature owner)
    {
        LocString fmt = IntentLabelFormat;
        fmt.Add("Damage", GetTargetedSingleDamage(targets, owner));
        if (Repeats > 1)
        {
            fmt.Add("Repeat", Repeats);
        }

        return fmt;
    }

    protected override LocString GetIntentDescription(IEnumerable<Creature> targets, Creature owner)
    {
        Creature? target = ResolveTarget(targets, owner);
        bool usePlayerTargetDescription = UsesVanillaPlayerTargetIntentVisual
            && target is { IsPlayer: true }
            && !string.IsNullOrWhiteSpace(_playerTargetDescriptionKey);
        LocString desc = BadgedIntentDescription.Create(
            usePlayerTargetDescription ? _playerTargetDescriptionKey : _descriptionKey,
            owner,
            "ATTACK");
        desc.Add("Damage", GetTargetedSingleDamage(targets, owner));
        desc.Add("Repeat", Repeats);
        if (!usePlayerTargetDescription)
        {
            desc.Add("TargetName", target?.Name ?? "Unknown Target");
        }

        BadgedIntentDescription.AddBadgeVariables(desc, Badges);
        return desc;
    }

    private int GetTargetedSingleDamage(IEnumerable<Creature> targets, Creature owner)
    {
        Creature? target = ResolveTarget(targets, owner);
        return TargetedAttackIntentPreviewHelper.GetModifiedDamage(owner, target, DamageCalc?.Invoke() ?? 0m);
    }

    public IReadOnlyList<IntentTargetLineTarget> GetIntentTargetLineTargets(
        Creature owner,
        IReadOnlyList<Creature>? fallbackTargets)
    {
        Creature? target = ResolveTarget(
            fallbackTargets ?? Array.Empty<Creature>(),
            owner);
        return target == null
            ? Array.Empty<IntentTargetLineTarget>()
            : [new IntentTargetLineTarget(target, "BadgedTargetedAttack")];
    }

    private Creature? ResolveTarget(
        IEnumerable<Creature> targets,
        Creature owner)
    {
        Creature? target = _targetResolver?.Invoke(owner);
        return target is { IsAlive: true }
            ? target
            : TargetedMonsterAttackHelper.GetPrimaryTarget(
                owner,
                targets as IReadOnlyList<Creature>);
    }

    internal bool TryCreateCombinedDisplayIntent([NotNullWhen(true)] out AbstractIntent? combined)
    {
        combined = null;
        if (!BadgedIntentBadgeClassifier.TryGetAttackKind(Badges, out string? kind))
        {
            return false;
        }

        IntentBadge[] badges = Badges.ToArray();
        Func<decimal> damage = DamageCalc ?? (() => 0m);
        combined = kind == CombinedIntentAnimData.AttackDebuff
            ? new CombinedTargetedAttackDebuffIntent(
                damage,
                _repeatCalc,
                _descriptionKey,
                _playerTargetDescriptionKey,
                UsesVanillaPlayerTargetIntentVisual,
                _targetResolver,
                badges)
            : new CombinedTargetedAttackBuffIntent(
                damage,
                _repeatCalc,
                _descriptionKey,
                _playerTargetDescriptionKey,
                UsesVanillaPlayerTargetIntentVisual,
                _targetResolver,
                badges);
        return true;
    }
}

public class BadgedDefendIntent : DefendIntent, IBadgedIntent
{
    private readonly string? _descriptionKey;

    public BadgedDefendIntent(IntentBadge badge, int blockAmount = 0, string? descriptionKey = null)
        : this(new[] { badge }, blockAmount, descriptionKey)
    {
    }

    public BadgedDefendIntent(IEnumerable<IntentBadge> badges, int blockAmount = 0, string? descriptionKey = null)
    {
        Badges = BadgedIntentBadgeList.Create(badges);
        BlockAmount = blockAmount;
        _descriptionKey = descriptionKey;
    }

    public BadgedDefendIntent(int blockAmount, string? descriptionKey, params IntentBadge[] badges)
        : this(badges, blockAmount, descriptionKey)
    {
    }

    public IntentBadge Badge => Badges[0];

    public IReadOnlyList<IntentBadge> Badges { get; }

    public int BlockAmount { get; }

    public override IEnumerable<string> AssetPaths => base.AssetPaths.Concat(Badges.SelectMany(badge => badge.AssetPaths));

    protected override string IntentPrefix => BadgedIntentTitle.GetPrefix(Badges, BadgedIntentMainIntent.Defend);

    public override Texture2D? GetTexture(IEnumerable<Creature> targets, Creature owner)
    {
        return base.GetTexture(targets, owner);
    }

    public override string GetAnimation(IEnumerable<Creature> targets, Creature owner)
    {
        return "defend";
    }

    protected override LocString GetIntentDescription(IEnumerable<Creature> targets, Creature owner)
    {
        LocString desc = BadgedIntentDescription.Create(_descriptionKey, owner, "DEFEND");
        desc.Add("Amount", BlockAmount);
        desc.Add("BlockAmount", BlockAmount);
        BadgedIntentDescription.AddBadgeVariables(desc, Badges);
        return desc;
    }
}

public class BadgedBuffIntent : BuffIntent, IBadgedIntent
{
    private readonly string? _descriptionKey;

    public BadgedBuffIntent(IntentBadge badge, int amount = 0, string? descriptionKey = null)
        : this(new[] { badge }, amount, descriptionKey)
    {
    }

    public BadgedBuffIntent(IEnumerable<IntentBadge> badges, int amount = 0, string? descriptionKey = null)
    {
        Badges = BadgedIntentBadgeList.Create(badges);
        Amount = amount;
        _descriptionKey = descriptionKey;
    }

    public IntentBadge Badge => Badges[0];

    public IReadOnlyList<IntentBadge> Badges { get; }

    public int Amount { get; }

    public override IEnumerable<string> AssetPaths => base.AssetPaths.Concat(Badges.SelectMany(badge => badge.AssetPaths));

    public override Texture2D? GetTexture(IEnumerable<Creature> targets, Creature owner)
    {
        return base.GetTexture(targets, owner);
    }

    protected override LocString GetIntentDescription(IEnumerable<Creature> targets, Creature owner)
    {
        LocString desc = BadgedIntentDescription.Create(_descriptionKey, owner, "BUFF");
        desc.Add("Amount", Amount);
        BadgedIntentDescription.AddBadgeVariables(desc, Badges);
        return desc;
    }
}

public class BadgedDebuffIntent : DebuffIntent, IBadgedIntent
{
    private readonly string? _descriptionKey;
    private Func<int, IntentBadge>? bind;
    private object? value;
    private bool v;


    public BadgedDebuffIntent(IntentBadge badge, int amount = 0, string? descriptionKey = null, bool strong = false)
        : this(new[] { badge }, amount, descriptionKey, strong)
    {
    }

    public BadgedDebuffIntent(IEnumerable<IntentBadge> badges, int amount = 0, string? descriptionKey = null, bool strong = false)
        : base(strong)
    {
        Badges = BadgedIntentBadgeList.Create(badges);
        Amount = amount;
        _descriptionKey = descriptionKey;
    }

    public BadgedDebuffIntent(Func<int, IntentBadge> bind, int amount, object value, bool v)
    {
        this.bind = bind ?? throw new ArgumentNullException(nameof(bind));
        Amount = amount;
        this.value = value;
        this.v = v;
        Badges = BadgedIntentBadgeList.Create(this.bind(amount));
    }


    public IntentBadge Badge => Badges[0];

    public IReadOnlyList<IntentBadge> Badges { get; }

    public int Amount { get; }

    public override IEnumerable<string> AssetPaths => base.AssetPaths.Concat(Badges.SelectMany(badge => badge.AssetPaths));

    public override Texture2D? GetTexture(IEnumerable<Creature> targets, Creature owner)
    {
        return base.GetTexture(targets, owner);
    }

    protected override LocString GetIntentDescription(IEnumerable<Creature> targets, Creature owner)
    {
        LocString desc = BadgedIntentDescription.Create(_descriptionKey, owner, "DEBUFF");
        desc.Add("Amount", Amount);
        BadgedIntentDescription.AddBadgeVariables(desc, Badges);
        return desc;
    }
}

internal enum BadgedIntentMainIntent
{
    Attack,
    Defend
}

internal enum BadgedIntentBadgeEffectType
{
    None,
    Buff,
    Debuff
}

internal static class BadgedIntentBadgeClassifier
{
    public static bool TryGetAttackKind(IReadOnlyList<IntentBadge> badges, [NotNullWhen(true)] out string? kind)
    {
        BadgedIntentBadgeEffectType effectType = GetEffectType(badges);
        kind = effectType switch
        {
            BadgedIntentBadgeEffectType.Debuff => CombinedIntentAnimData.AttackDebuff,
            BadgedIntentBadgeEffectType.Buff => CombinedIntentAnimData.AttackBuff,
            _ => null
        };

        return kind != null;
    }

    private static BadgedIntentBadgeEffectType GetEffectType(IReadOnlyList<IntentBadge> badges)
    {
        bool hasBuff = false;

        foreach (IntentBadge badge in badges)
        {
            BadgedIntentBadgeEffectType effectType = GetEffectType(badge);
            if (effectType == BadgedIntentBadgeEffectType.Debuff)
            {
                return BadgedIntentBadgeEffectType.Debuff;
            }

            if (effectType == BadgedIntentBadgeEffectType.Buff)
            {
                hasBuff = true;
            }
        }

        return hasBuff ? BadgedIntentBadgeEffectType.Buff : BadgedIntentBadgeEffectType.None;
    }

    private static BadgedIntentBadgeEffectType GetEffectType(IntentBadge badge)
    {
        if (badge.HasPower)
        {
            PowerType powerType = badge.Power.GetTypeForAmount(badge.Amount);
            return powerType switch
            {
                PowerType.Debuff => BadgedIntentBadgeEffectType.Debuff,
                PowerType.Buff => BadgedIntentBadgeEffectType.Buff,
                _ => BadgedIntentBadgeEffectType.None
            };
        }

        return badge.Kind switch
        {
            IntentBadgeKind.StatusCard => BadgedIntentBadgeEffectType.Debuff,
            IntentBadgeKind.Heal or IntentBadgeKind.Summon => BadgedIntentBadgeEffectType.Buff,
            _ => BadgedIntentBadgeEffectType.None
        };
    }
}

internal static class BadgedIntentTitle
{
    public static string GetPrefix(IReadOnlyList<IntentBadge> badges, BadgedIntentMainIntent mainIntent)
    {
        if (TryGetTitleEffectType(badges, out PowerType effectType))
        {
            return (effectType, mainIntent) switch
            {
                (PowerType.Buff, BadgedIntentMainIntent.Attack) => "ATTACK",
                (PowerType.Buff, BadgedIntentMainIntent.Defend) => "DEFEND",
                (PowerType.Debuff, BadgedIntentMainIntent.Attack) => "ATTACK",
                (PowerType.Debuff, BadgedIntentMainIntent.Defend) => "DEFEND",
                _ => GetMainPrefix(mainIntent)
            };
        }

        return GetMainPrefix(mainIntent);
    }

    public static string GetPrefix(IntentBadge badge, BadgedIntentMainIntent mainIntent)
    {
        return GetPrefix(new[] { badge }, mainIntent);
    }

    public static string GetPrefix(IReadOnlyList<IntentBadge> badges, IntentType intentType)
    {
        return intentType switch
        {
            IntentType.Attack => "ATTACK",
            IntentType.Defend => "DEFEND",
            IntentType.Buff => "BUFF",
            IntentType.Debuff or IntentType.DebuffStrong => "DEBUFF",
            _ => "UNKNOWN"
        };
    }

    public static string GetPrefix(IntentBadge badge, IntentType intentType)
    {
        return GetPrefix(new[] { badge }, intentType);
    }

    private static bool TryGetTitleEffectType(IReadOnlyList<IntentBadge> badges, out PowerType effectType)
    {
        effectType = default;
        if (badges.Count == 0)
        {
            return false;
        }

        bool hasBuff = false;
        bool hasDebuff = false;
        foreach (IntentBadge badge in badges)
        {
            PowerType badgeType = GetBadgeTitleEffectType(badge);
            if (badgeType == PowerType.Debuff)
            {
                hasDebuff = true;
            }
            else if (badgeType == PowerType.Buff)
            {
                hasBuff = true;
            }
        }

        if (!hasBuff && !hasDebuff)
        {
            return false;
        }

        effectType = hasDebuff ? PowerType.Debuff : PowerType.Buff;
        return true;
    }

    private static PowerType GetBadgeTitleEffectType(IntentBadge badge)
    {
        if (badge.HasPower)
        {
            PowerType type = badge.Power.GetTypeForAmount(badge.Amount);
            return type is PowerType.Buff or PowerType.Debuff ? type : PowerType.None;
        }

        return badge.Kind switch
        {
            IntentBadgeKind.StatusCard => PowerType.Debuff,
            IntentBadgeKind.Heal or IntentBadgeKind.Summon => PowerType.Buff,
            _ => PowerType.None
        };
    }

    private static string GetMainPrefix(BadgedIntentMainIntent mainIntent)
    {
        return mainIntent switch
        {
            BadgedIntentMainIntent.Attack => "ATTACK",
            BadgedIntentMainIntent.Defend => "DEFEND",
            _ => "UNKNOWN"
        };
    }
}

internal static class BadgedIntentAnimation
{
    public static string GetAttackAnimation(int totalDamage)
    {
        if (totalDamage < 5)
        {
            return "attack_1";
        }

        if (totalDamage < 10)
        {
            return "attack_2";
        }

        if (totalDamage < 20)
        {
            return "attack_3";
        }

        return totalDamage < 40 ? "attack_4" : "attack_5";
    }
}

internal static class BadgedIntentHoverTipFactory
{
    public static HoverTip Create(
        AbstractIntent intent,
        IReadOnlyList<IntentBadge> effects,
        HoverTip baseTip,
        IEnumerable<Creature> targets,
        Creature owner)
    {
        IReadOnlyList<Creature> targetList = targets as IReadOnlyList<Creature> ?? targets.ToArray();
        Texture2D? icon = ResolveIcon(intent, effects, targetList, owner);
        string titlePrefix = BadgedIntentTitle.GetPrefix(effects, intent.IntentType);

        HoverTip tip = icon == null
            ? new HoverTip(new LocString("intents", titlePrefix + ".title"), baseTip.Description)
            : new HoverTip(new LocString("intents", titlePrefix + ".title"), baseTip.Description, icon);

        tip.Id = baseTip.Id;
        return tip;
    }

    public static HoverTip ReplaceIcon(ICombinedIntentHoverIcon combinedHoverIcon, HoverTip baseTip, Texture2D icon)
    {
        HoverTip tip = new(
            new LocString("intents", combinedHoverIcon.HoverIntentPrefix + ".title"),
            baseTip.Description,
            icon);

        tip.Id = baseTip.Id;
        tip.IsSmart = baseTip.IsSmart;
        tip.IsDebuff = baseTip.IsDebuff;
        tip.IsInstanced = baseTip.IsInstanced;
        tip.ShouldOverrideTextOverflow = baseTip.ShouldOverrideTextOverflow;
        return tip;
    }

    public static Texture2D? ResolveCombinedHoverIcon(ICombinedIntentHoverIcon combinedHoverIcon)
    {
        return IntentAssetResolver.GetTexture(
            new[] { combinedHoverIcon.HoverIconPath },
            combinedHoverIcon.HoverIconPath);
    }

    public static IEnumerable<IHoverTip> GetExtraHoverTips(IReadOnlyList<IntentBadge> effects)
    {
        return IHoverTip.RemoveDupes(effects.SelectMany(static effect => effect.ExtraHoverTips));
    }

    private static Texture2D? ResolveIcon(
        AbstractIntent intent,
        IReadOnlyList<IntentBadge> effects,
        IReadOnlyList<Creature> targets,
        Creature owner)
    {
        if (intent is ICombinedIntentHoverIcon combinedHoverIcon)
        {
            Texture2D? hoverIcon = IntentAssetResolver.GetTexture(
                new[] { combinedHoverIcon.HoverIconPath },
                combinedHoverIcon.HoverIconPath);
            if (hoverIcon != null)
            {
                return hoverIcon;
            }
        }

        try
        {
            Texture2D? icon = intent.GetTexture(targets, owner);
            if (icon != null)
            {
                return icon;
            }
        }
        catch
        {
            
        }

        return effects.Count > 0 ? effects[0].GetTexture() : null;
    }
}

internal static class BadgedIntentDescription
{
    public static LocString Create(string? descriptionKey, Creature owner, string fallbackIntentPrefix)
    {
        LocString desc = string.IsNullOrWhiteSpace(descriptionKey)
            ? new LocString("intents", fallbackIntentPrefix + ".description")
            : new LocString("intents", descriptionKey);

        desc.Add("IsMultiplayer", owner.CombatState != null && owner.CombatState.RunState.Players.Count > 1);
        NaturalFloorLiberation.NaturalFloorNihilIntentText.AddVariables(desc, descriptionKey, owner);
        return desc;
    }

    public static void AddBadgeVariables(LocString desc, IntentBadge badge)
    {
        AddBadgeVariables(desc, new[] { badge });
    }

    public static void AddBadgeVariables(LocString desc, IReadOnlyList<IntentBadge> badges)
    {
        desc.Add("BadgeCount", badges.Count);
        if (badges.Count == 0)
        {
            return;
        }

        IntentBadge primaryBadge = badges.FirstOrDefault(static badge =>
            (badge.Semantics & IntentEffectSemantics.GroupAttack) == 0)
            ?? badges[0];
        AddSingleBadgeVariables(desc, primaryBadge, "Badge");

        for (int i = 0; i < badges.Count; i++)
        {
            AddSingleBadgeVariables(desc, badges[i], "Badge" + (i + 1));
        }
    }

    private static void AddSingleBadgeVariables(LocString desc, IntentBadge badge, string prefix)
    {
        desc.Add(prefix + "Amount", badge.Amount);
        desc.Add(prefix + "SignedMagnitude", SignedMagnitude(badge.Amount));
        desc.Add(prefix + "Magnitude", Math.Abs(badge.Amount));
        desc.Add(prefix + "Kind", badge.Kind.ToString());
        desc.Add(prefix + "Id", GetBadgeId(badge));
        desc.Add(prefix + "LeftText", badge.LeftText ?? string.Empty);
        desc.Add(prefix + "RightText", badge.RightText ?? string.Empty);
        if (badge.HasPower)
        {
            desc.Add(prefix + "PowerId", badge.Power.Id.Entry);
        }

        if (badge.HasCard)
        {
            desc.Add(prefix + "CardId", badge.Card.Id.Entry);
            desc.Add(prefix + "CardName", badge.Card.Title);
        }
    }

    private static string GetBadgeId(IntentBadge badge)
    {
        return badge.HasPower ? badge.Power.Id.Entry : badge.Kind.ToString().ToUpperInvariant();
    }

    private static string SignedMagnitude(int amount)
    {
        if (amount > 0)
        {
            return "+" + amount;
        }

        return amount.ToString();
    }
}

internal static class BadgedIntentBadgeList
{
    public static IReadOnlyList<IntentBadge> Create(params IntentBadge[] badges)
    {
        return Create((IEnumerable<IntentBadge>)badges);
    }

    public static IReadOnlyList<IntentBadge> Create(IEnumerable<IntentBadge> badges)
    {
        if (badges == null)
        {
            throw new ArgumentNullException(nameof(badges));
        }

        IntentBadge[] materialized = badges
            .Select(badge => badge ?? throw new ArgumentNullException(nameof(badges), "Badge entries cannot be null."))
            .ToArray();

        if (materialized.Length == 0)
        {
            throw new ArgumentException("At least one intent badge is required.", nameof(badges));
        }

        return materialized;
    }
}
