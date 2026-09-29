using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.combat;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.combat;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Intents;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace LibraryOfRuina.framework.intents;

public enum GroupAttackCombinedEffect
{
    None,
    Debuff,
    Buff
}

public class IndiscriminateAttackIntent :
    AttackIntent,
    IBadgedIntent,
    IGroupAttackIntent,
    ITargetedIntentIndicator,
    IIntentTargetLineProvider,
    ICombinedIntentVisual,
    ICombinedIntentHoverIcon
{
    public const string GroupAttackBadgeImagePath = "intents/indiscriminate_attack_badge.png";
    private static readonly IntentBadge GroupAttackBadge = CreateGroupAttackBadge();

    private readonly Func<int> _repeatCalc;
    private readonly string? _descriptionKey;
    private readonly Func<Creature, IReadOnlyList<Creature>>? _targetResolver;

    public IndiscriminateAttackIntent(
        int damage,
        int repeats,
        string? descriptionKey,
        params IntentBadge[] badges)
        : this(() => damage, () => repeats, descriptionKey, badges)
    {
    }

    public IndiscriminateAttackIntent(
        int damage,
        int repeats,
        string? descriptionKey,
        Func<Creature, IReadOnlyList<Creature>> targetResolver,
        params IntentBadge[] badges)
        : this(() => damage, () => repeats, descriptionKey, badges)
    {
        _targetResolver = targetResolver;
    }

    public IndiscriminateAttackIntent(Func<int> damageCalc, Func<int>? repeatCalc, string? descriptionKey)
        : this(damageCalc, repeatCalc, descriptionKey, Array.Empty<IntentBadge>())
    {
    }

    public IndiscriminateAttackIntent(
        Func<int> damageCalc,
        Func<int>? repeatCalc,
        string? descriptionKey,
        Func<Creature, IReadOnlyList<Creature>> targetResolver,
        params IntentBadge[] badges)
        : this(damageCalc, repeatCalc, descriptionKey, badges)
    {
        _targetResolver = targetResolver;
    }

    public IndiscriminateAttackIntent(
        Func<int> damageCalc,
        Func<int>? repeatCalc,
        string? descriptionKey,
        params IntentBadge[] badges)
    {
        ArgumentNullException.ThrowIfNull(damageCalc);
        DamageCalc = () => damageCalc();
        _repeatCalc = repeatCalc ?? (() => 1);
        _descriptionKey = descriptionKey;
        Badges = BadgedIntentBadgeList.Create(new[] { GroupAttackBadge }.Concat(badges ?? Array.Empty<IntentBadge>()));
    }

    public IntentBadge Badge => GroupAttackBadge;

    public IReadOnlyList<IntentBadge> Badges { get; }

    public bool IsGroupAttack => true;

    public GroupAttackCombinedEffect CombinedEffect { get; init; }

    private string? CombinedAttackKind => CombinedEffect switch
    {
        GroupAttackCombinedEffect.Debuff => CombinedIntentAnimData.AttackDebuff,
        GroupAttackCombinedEffect.Buff => CombinedIntentAnimData.AttackBuff,
        _ => null
    };

    public string HoverIconPath => CombinedAttackKind is { } kind
        ? CombinedIntentAnimData.GetHoverIconPath(kind)
        : string.Empty;

    public string HoverIntentPrefix => IntentPrefix;

    public override int Repeats => Math.Max(1, _repeatCalc());

    public Func<IReadOnlyList<Creature>, Task> WithPreAttackBlockBreak(
        MonsterModel attacker,
        Func<IReadOnlyList<Creature>, Task> performMove)
    {
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(performMove);

        return async fallbackTargets =>
        {
            Creature? owner = attacker.Creature;
            if (owner != null)
            {
                await IndiscriminateAttackBlockBreaker.BreakBlockBeforeAttack(
                    attacker,
                    Math.Max(0, (int)(DamageCalc?.Invoke() ?? 0m)),
                    ResolveTargets(owner, fallbackTargets),
                    suppressNextDamageHook: false);
            }

            await performMove(fallbackTargets);
        };
    }

    public override IEnumerable<string> AssetPaths
    {
        get
        {
            IEnumerable<string> paths = base.AssetPaths.Concat(Badges.SelectMany(badge => badge.AssetPaths));
            if (CombinedAttackKind is { } kind)
            {
                paths = paths.Concat(CombinedIntentAnimData.GetAssetPaths(kind)).Append(HoverIconPath);
            }

            return paths;
        }
    }

    public string GetCombinedAnimation(IEnumerable<Creature> targets, Creature owner)
    {
        if (CombinedAttackKind is not { } kind)
        {
            return string.Empty;
        }

        int tier = CombinedIntentAnimData.GetAttackTier(GetTotalDamage(targets, owner));
        return CombinedIntentAnimData.GetAttackAnimationKey(kind, tier);
    }

    public override Texture2D GetTexture(IEnumerable<Creature> targets, Creature owner)
    {
        if (CombinedAttackKind is not { } kind)
        {
            return base.GetTexture(targets, owner);
        }

        int tier = CombinedIntentAnimData.GetAttackTier(GetTotalDamage(targets, owner));
        return PreloadManager.Cache.GetTexture2D(CombinedIntentAnimData.GetIconPath(kind, tier));
    }

    protected override string IntentPrefix => "ATTACK";

    protected override LocString IntentLabelFormat =>
        Repeats > 1
            ? new LocString("intents", "FORMAT_DAMAGE_MULTI")
            : new LocString("intents", "FORMAT_DAMAGE_SINGLE");

    public override int GetTotalDamage(IEnumerable<Creature> targets, Creature owner)
    {
        return GetPlayerPreviewDamage(owner) * Repeats;
    }

    public override string GetAnimation(IEnumerable<Creature> targets, Creature owner)
    {
        return CombinedAttackKind != null
            ? IntentAnimData.buff
            : BadgedIntentAnimation.GetAttackAnimation(GetTotalDamage(targets, owner));
    }

    public override LocString GetIntentLabel(IEnumerable<Creature> targets, Creature owner)
    {
        LocString fmt = IntentLabelFormat;
        fmt.Add("Damage", GetPlayerPreviewDamage(owner));
        if (Repeats > 1)
        {
            fmt.Add("Repeat", Repeats);
        }

        return fmt;
    }

    protected override LocString GetIntentDescription(IEnumerable<Creature> targets, Creature owner)
    {
        IReadOnlyList<Creature> fallbackTargets = targets as IReadOnlyList<Creature>
            ?? targets.Where(static target => target is { IsAlive: true }).Distinct().ToArray();
        IReadOnlyList<Creature> resolvedTargets = ResolveTargets(owner, fallbackTargets);
        LocString desc = BadgedIntentDescription.Create(_descriptionKey, owner, "ATTACK");
        desc.Add("Damage", GetPlayerPreviewDamage(owner));
        desc.Add("Repeat", Repeats);
        desc.Add("TargetCount", resolvedTargets.Count);
        desc.Add(
            "TargetNames",
            resolvedTargets.Count == 0
                ? "未知目标"
                : string.Join("、", resolvedTargets.Select(static target => target.Name)));
        BadgedIntentDescription.AddBadgeVariables(desc, Badges);
        return desc;
    }

    public virtual IReadOnlyList<IntentTargetLineTarget> GetIntentTargetLineTargets(
        Creature owner,
        IReadOnlyList<Creature>? fallbackTargets)
    {
        return ResolveTargets(owner, fallbackTargets ?? Array.Empty<Creature>())
            .Select(static target => new IntentTargetLineTarget(target, "IndiscriminateAttack"))
            .ToArray();
    }

    private IReadOnlyList<Creature> ResolveTargets(
        Creature owner,
        IReadOnlyList<Creature> fallbackTargets)
    {
        if (_targetResolver != null)
        {
            return CombatTargets.DeterministicLiving(
                _targetResolver(owner),
                owner);
        }

        IEnumerable<Creature> targets =
            TargetedMonsterAttackHelper.GetTargetList(owner, fallbackTargets);
        if (owner.CombatState != null)
        {
            targets = targets.Concat(owner.CombatState.PlayerCreatures);
        }

        return CombatTargets.DeterministicLiving(targets, owner);
    }

    private int GetPlayerPreviewDamage(Creature owner)
    {
        Creature? playerTarget = GetPlayerPreviewTarget(owner);
        return TargetedAttackIntentPreviewHelper.GetModifiedDamage(
            owner,
            playerTarget,
            DamageCalc?.Invoke() ?? 0m);
    }

    private static Creature? GetPlayerPreviewTarget(Creature owner)
    {
        if (owner.CombatState == null)
        {
            return null;
        }

        Player? localPlayer = LocalContextCompat.GetMe(owner.CombatState);
        return localPlayer?.Creature is { IsAlive: true } localCreature
            ? localCreature
            : owner.CombatState.PlayerCreatures.FirstOrDefault(static creature => creature.IsAlive);
    }

    public static IntentBadge CreateGroupAttackBadge()
    {
        return IntentBadge.Custom(
            GroupAttackBadgeImagePath,
            IntentEffectSemantics.GroupAttack,
            CreateGroupAttackHoverTips);
    }

    private static IEnumerable<IHoverTip> CreateGroupAttackHoverTips()
    {
        return new IHoverTip[]
        {
            new HoverTip(
                new LocString("intents", "INDISCRIMINATE_ATTACK.title"),
                new LocString("intents", "INDISCRIMINATE_ATTACK.description"))
        };
    }
}
