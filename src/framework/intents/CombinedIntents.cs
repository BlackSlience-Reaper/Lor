using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using LibraryOfRuina.patches;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Intents;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.framework.intents;

/// <summary>
/// STS2 intent icon + STS1 particle VFX, rendered via <see cref="CombinedIntentVisualPatch"/>.
/// </summary>
public interface ICombinedIntentVisual
{
    string GetCombinedAnimation(IEnumerable<Creature> targets, Creature owner);
}

public interface ICombinedIntentHoverIcon
{
    string HoverIconPath { get; }

    string HoverIntentPrefix { get; }
}

/// <summary>
/// 攻击+负面 / 攻击+强化 / 攻击+格挡：STS2 五档攻击图标 + STS1 粒子。
/// </summary>
public abstract class CombinedAttackIntentBase :
    AttackIntent,
    IIntentEffectProvider,
    IGroupAttackIntent,
    ICombinedIntentVisual,
    ICombinedIntentHoverIcon,
    ICompositeIntent
{
    private readonly Func<int> _repeatCalc;
    private readonly string? _descriptionKey;
    private CompositeIntent? _composite;

    protected CombinedAttackIntentBase(
        Func<decimal> damageCalc,
        Func<int>? repeatCalc,
        string? descriptionKey,
        IntentBadge[] badges)
    {
        DamageCalc = damageCalc ?? throw new ArgumentNullException(nameof(damageCalc));
        _repeatCalc = repeatCalc ?? (() => 1);
        _descriptionKey = descriptionKey;
        Effects = IntentEffectCollection.Create(badges);
    }

    protected abstract string AttackKind { get; }

    /// <summary>
    /// 第一次读取时才生成：子类的 <see cref="AttackKind"/> 可能依赖子类构造函数里才赋值的字段
    /// （例如伊织的组合攻击按传入的种类选图标组），在基类构造函数里读取会得到错误的组。
    /// </summary>
    public CompositeIntent Composite =>
        _composite ??= CompositeIntent.FromVisualKind(
            AttackKind,
            IntentType,
            CompositeIntent.ResolveTargeting(this, Effects),
            Effects);

    public string HoverIconPath => CombinedIntentAnimData.GetHoverIconPath(AttackKind);

    public string HoverIntentPrefix => IntentPrefix;

    public IReadOnlyList<IntentBadge> Effects { get; }

    public bool IsGroupAttack => IntentEffectCollection.HasGroupAttack(Effects);

    public override int Repeats => Math.Max(1, _repeatCalc());

    public override IEnumerable<string> AssetPaths =>
        CombinedIntentAnimData.GetAssetPaths(AttackKind)
            .Append(HoverIconPath)
            .Concat(Effects.SelectMany(static effect => effect.AssetPaths));

    protected override LocString IntentLabelFormat =>
        Repeats > 1
            ? new LocString("intents", "FORMAT_DAMAGE_MULTI")
            : new LocString("intents", "FORMAT_DAMAGE_SINGLE");

    public string GetCombinedAnimation(IEnumerable<Creature> targets, Creature owner)
    {
        int tier = CombinedIntentAnimData.GetAttackTier(GetTotalDamage(targets, owner));
        return CombinedIntentAnimData.GetAttackAnimationKey(AttackKind, tier);
    }

    public override Texture2D GetTexture(IEnumerable<Creature> targets, Creature owner)
    {
        int tier = CombinedIntentAnimData.GetAttackTier(GetTotalDamage(targets, owner));
        return PreloadManager.Cache.GetTexture2D(CombinedIntentAnimData.GetIconPath(AttackKind, tier));
    }

    public override string GetAnimation(IEnumerable<Creature> targets, Creature owner)
    {
        return IntentAnimData.buff;
    }

    public override int GetTotalDamage(IEnumerable<Creature> targets, Creature owner)
    {
        return GetSingleDamage(targets, owner) * Repeats;
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
        LocString desc = BadgedIntentDescription.Create(_descriptionKey, owner, IntentPrefix);
        desc.Add("Damage", GetSingleDamage(targets, owner));
        desc.Add("Repeat", Repeats);
        AddDescriptionVariables(desc, targets, owner);
        BadgedIntentDescription.AddBadgeVariables(desc, Effects);
        return desc;
    }

    protected virtual void AddDescriptionVariables(
        LocString desc,
        IEnumerable<Creature> targets,
        Creature owner)
    {
    }
}

public sealed class CombinedAttackDebuffIntent : CombinedAttackIntentBase
{
    public CombinedAttackDebuffIntent(
        int damage,
        int repeats = 1,
        string? descriptionKey = null,
        params IntentBadge[] badges)
        : this(() => damage, () => repeats, descriptionKey, badges)
    {
    }

    public CombinedAttackDebuffIntent(
        Func<decimal> damageCalc,
        Func<int>? repeatCalc = null,
        string? descriptionKey = null,
        params IntentBadge[] badges)
        : base(damageCalc, repeatCalc, descriptionKey, badges)
    {
    }

    protected override string AttackKind => CombinedIntentAnimData.AttackDebuff;

    protected override string IntentPrefix => "COMBINED_ATTACK_DEBUFF";
}

public sealed class CombinedAttackCardDebuffIntent : CombinedAttackIntentBase
{
    public CombinedAttackCardDebuffIntent(
        int damage,
        int repeats = 1,
        string? descriptionKey = null,
        params IntentBadge[] badges)
        : this(() => damage, () => repeats, descriptionKey, badges)
    {
    }

    public CombinedAttackCardDebuffIntent(
        Func<decimal> damageCalc,
        Func<int>? repeatCalc = null,
        string? descriptionKey = null,
        params IntentBadge[] badges)
        : base(damageCalc, repeatCalc, descriptionKey, badges)
    {
    }

    public override IntentType IntentType => IntentType.CardDebuff;

    protected override string AttackKind => CombinedIntentAnimData.AttackDebuff;

    protected override string IntentPrefix => "CARD_DEBUFF";
}

public sealed class CombinedAttackBuffIntent : CombinedAttackIntentBase
{
    public CombinedAttackBuffIntent(
        int damage,
        int repeats = 1,
        string? descriptionKey = null,
        params IntentBadge[] badges)
        : this(() => damage, () => repeats, descriptionKey, badges)
    {
    }

    public CombinedAttackBuffIntent(
        Func<decimal> damageCalc,
        Func<int>? repeatCalc = null,
        string? descriptionKey = null,
        params IntentBadge[] badges)
        : base(damageCalc, repeatCalc, descriptionKey, badges)
    {
    }

    protected override string AttackKind => CombinedIntentAnimData.AttackBuff;

    protected override string IntentPrefix => "COMBINED_ATTACK_BUFF";
}

public abstract class CombinedTargetedAttackIntentBase :
    CombinedAttackIntentBase,
    ITargetedIntentIndicator,
    IIntentTargetLineProvider,
    IUsesVanillaPlayerTargetIntentVisual
{
    private readonly string? _playerTargetDescriptionKey;
    private readonly string? _descriptionKey;
    private readonly Func<Creature, Creature?>? _targetResolver;

    protected CombinedTargetedAttackIntentBase(
        Func<decimal> damageCalc,
        Func<int>? repeatCalc,
        string? descriptionKey,
        string? playerTargetDescriptionKey,
        bool useVanillaPlayerTargetIntentVisual,
        Func<Creature, Creature?>? targetResolver,
        IntentBadge[] badges)
        : base(damageCalc, repeatCalc, descriptionKey, badges)
    {
        _descriptionKey = descriptionKey;
        _playerTargetDescriptionKey = playerTargetDescriptionKey;
        _targetResolver = targetResolver;
        UsesVanillaPlayerTargetIntentVisual = useVanillaPlayerTargetIntentVisual;
    }

    public bool UsesVanillaPlayerTargetIntentVisual { get; }

    public Func<Creature, IReadOnlyList<Creature>>? TargetLineResolver { get; set; }

    protected override LocString GetIntentDescription(IEnumerable<Creature> targets, Creature owner)
    {
        Creature? target = ResolveTarget(targets, owner);
        bool usePlayerTargetDescription = UsesVanillaPlayerTargetIntentVisual
            && target is { IsPlayer: true }
            && !string.IsNullOrWhiteSpace(_playerTargetDescriptionKey);
        LocString desc = BadgedIntentDescription.Create(
            usePlayerTargetDescription ? _playerTargetDescriptionKey : _descriptionKey,
            owner,
            IntentPrefix);
        desc.Add("Damage", GetTargetedSingleDamage(targets, owner));
        desc.Add("Repeat", Repeats);
        if (!usePlayerTargetDescription)
        {
            desc.Add("TargetName", target?.Name ?? "Unknown Target");
        }

        AddDescriptionVariables(desc, targets, owner);
        BadgedIntentDescription.AddBadgeVariables(desc, Effects);
        return desc;
    }

    public override int GetTotalDamage(IEnumerable<Creature> targets, Creature owner)
    {
        return GetTargetedSingleDamage(targets, owner) * Repeats;
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

    private int GetTargetedSingleDamage(IEnumerable<Creature> targets, Creature owner)
    {
        Creature? target = ResolveTarget(targets, owner);
        return TargetedAttackIntentPreviewHelper.GetModifiedDamage(owner, target, DamageCalc?.Invoke() ?? 0m);
    }

    public IReadOnlyList<IntentTargetLineTarget> GetIntentTargetLineTargets(
        Creature owner,
        IReadOnlyList<Creature>? fallbackTargets)
    {
        if (TargetLineResolver is { } resolveTargets)
        {
            return resolveTargets(owner)
                .Select(target => new IntentTargetLineTarget(target, "CombinedTargetedAttack"))
                .ToArray();
        }

        Creature? target = ResolveTarget(
            fallbackTargets ?? Array.Empty<Creature>(),
            owner);
        return target == null
            ? Array.Empty<IntentTargetLineTarget>()
            : [new IntentTargetLineTarget(target, "CombinedTargetedAttack")];
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
}

public sealed class CombinedTargetedAttackDebuffIntent : CombinedTargetedAttackIntentBase
{
    public CombinedTargetedAttackDebuffIntent(
        int damage,
        int repeats,
        string? descriptionKey,
        string? playerTargetDescriptionKey,
        bool useVanillaPlayerTargetIntentVisual,
        params IntentBadge[] badges)
        : this(
            () => damage,
            () => repeats,
            descriptionKey,
            playerTargetDescriptionKey,
            useVanillaPlayerTargetIntentVisual,
            badges)
    {
    }

    public CombinedTargetedAttackDebuffIntent(
        Func<decimal> damageCalc,
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

    public CombinedTargetedAttackDebuffIntent(
        Func<decimal> damageCalc,
        Func<int>? repeatCalc,
        string? descriptionKey,
        string? playerTargetDescriptionKey,
        bool useVanillaPlayerTargetIntentVisual,
        Func<Creature, Creature?>? targetResolver,
        params IntentBadge[] badges)
        : base(
            damageCalc,
            repeatCalc,
            descriptionKey,
            playerTargetDescriptionKey,
            useVanillaPlayerTargetIntentVisual,
            targetResolver,
            badges)
    {
    }

    protected override string AttackKind => CombinedIntentAnimData.AttackDebuff;

    protected override string IntentPrefix => "COMBINED_ATTACK_DEBUFF";
}

public sealed class CombinedTargetedAttackBuffIntent : CombinedTargetedAttackIntentBase
{
    public CombinedTargetedAttackBuffIntent(
        Func<decimal> damageCalc,
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

    public CombinedTargetedAttackBuffIntent(
        Func<decimal> damageCalc,
        Func<int>? repeatCalc,
        string? descriptionKey,
        string? playerTargetDescriptionKey,
        bool useVanillaPlayerTargetIntentVisual,
        Func<Creature, Creature?>? targetResolver,
        params IntentBadge[] badges)
        : base(
            damageCalc,
            repeatCalc,
            descriptionKey,
            playerTargetDescriptionKey,
            useVanillaPlayerTargetIntentVisual,
            targetResolver,
            badges)
    {
    }

    protected override string AttackKind => CombinedIntentAnimData.AttackBuff;

    protected override string IntentPrefix => "COMBINED_ATTACK_BUFF";
}

public sealed class CombinedTargetedAttackDefendIntent : CombinedTargetedAttackIntentBase
{
    public CombinedTargetedAttackDefendIntent(
        int damage,
        int repeats,
        string? descriptionKey,
        string? playerTargetDescriptionKey,
        bool useVanillaPlayerTargetIntentVisual,
        int blockAmount = 0,
        params IntentBadge[] badges)
        : this(
            () => damage,
            () => repeats,
            descriptionKey,
            playerTargetDescriptionKey,
            useVanillaPlayerTargetIntentVisual,
            blockAmount,
            badges)
    {
    }

    public CombinedTargetedAttackDefendIntent(
        int damage,
        int repeats,
        string? descriptionKey,
        string? playerTargetDescriptionKey,
        bool useVanillaPlayerTargetIntentVisual,
        Func<Creature, Creature?> targetResolver,
        int blockAmount = 0,
        params IntentBadge[] badges)
        : this(
            () => damage,
            () => repeats,
            descriptionKey,
            playerTargetDescriptionKey,
            useVanillaPlayerTargetIntentVisual,
            targetResolver,
            blockAmount,
            badges)
    {
    }

    public CombinedTargetedAttackDefendIntent(
        Func<decimal> damageCalc,
        Func<int>? repeatCalc,
        string? descriptionKey,
        string? playerTargetDescriptionKey,
        bool useVanillaPlayerTargetIntentVisual,
        int blockAmount = 0,
        params IntentBadge[] badges)
        : this(
            damageCalc,
            repeatCalc,
            descriptionKey,
            playerTargetDescriptionKey,
            useVanillaPlayerTargetIntentVisual,
            targetResolver: null,
            blockAmount,
            badges)
    {
    }

    public CombinedTargetedAttackDefendIntent(
        Func<decimal> damageCalc,
        Func<int>? repeatCalc,
        string? descriptionKey,
        string? playerTargetDescriptionKey,
        bool useVanillaPlayerTargetIntentVisual,
        Func<Creature, Creature?>? targetResolver,
        int blockAmount = 0,
        params IntentBadge[] badges)
        : base(
            damageCalc,
            repeatCalc,
            descriptionKey,
            playerTargetDescriptionKey,
            useVanillaPlayerTargetIntentVisual,
            targetResolver,
            badges)
    {
        BlockAmount = blockAmount;
    }

    public int BlockAmount { get; }

    protected override string AttackKind => CombinedIntentAnimData.AttackDefend;

    protected override string IntentPrefix => "COMBINED_ATTACK_DEFEND";

    protected override void AddDescriptionVariables(
        LocString desc,
        IEnumerable<Creature> targets,
        Creature owner)
    {
        if (BlockAmount <= 0)
        {
            return;
        }

        desc.Add("Amount", BlockAmount);
        desc.Add("BlockAmount", BlockAmount);
    }
}

public sealed class CombinedAttackDefendIntent : CombinedAttackIntentBase
{
    public CombinedAttackDefendIntent(
        int damage,
        int repeats = 1,
        string? descriptionKey = null,
        int blockAmount = 0,
        params IntentBadge[] badges)
        : this(() => damage, () => repeats, descriptionKey, blockAmount, badges)
    {
    }

    public CombinedAttackDefendIntent(
        Func<decimal> damageCalc,
        Func<int>? repeatCalc = null,
        string? descriptionKey = null,
        int blockAmount = 0,
        params IntentBadge[] badges)
        : base(damageCalc, repeatCalc, descriptionKey, badges)
    {
        BlockAmount = blockAmount;
    }

    public int BlockAmount { get; }

    protected override string AttackKind => CombinedIntentAnimData.AttackDefend;

    protected override string IntentPrefix => "COMBINED_ATTACK_DEFEND";

    protected override void AddDescriptionVariables(
        LocString desc,
        IEnumerable<Creature> targets,
        Creature owner)
    {
        if (BlockAmount <= 0)
        {
            return;
        }

        desc.Add("Amount", BlockAmount);
        desc.Add("BlockAmount", BlockAmount);
    }
}

public abstract class CombinedCounterAttackIntentBase : CombinedAttackIntentBase, ICounterIntent
{
    private readonly Func<PlayerChoiceContext, Creature, Creature, Task>? _perform;

    protected CombinedCounterAttackIntentBase(
        Func<decimal> damageCalc,
        Func<int>? repeatCalc,
        string? descriptionKey,
        Func<PlayerChoiceContext, Creature, Creature, Task>? perform,
        IntentBadge[] badges)
        : base(damageCalc, repeatCalc, descriptionKey, badges)
    {
        _perform = perform;
    }

    public string CounterAnimationFramePath => CombinedIntentAnimData.GetIconPath(AttackKind, tier: 1);

    public string CounterAnimation => CombinedIntentAnimData.GetAnimationKey(AttackKind, tier: 1);

    public async Task PerformCounterIntent(PlayerChoiceContext choiceContext, Creature owner, Creature counterTarget)
    {
        ArgumentNullException.ThrowIfNull(choiceContext);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(counterTarget);

        if (_perform != null)
        {
            await _perform(choiceContext, owner, counterTarget);
            return;
        }

        if (owner.IsDead || owner.Monster == null || counterTarget.IsDead)
        {
            return;
        }

        using (TargetedMonsterAttackHelper.ForceTargets(owner, [counterTarget]))
        {
            await DamageCmd.Attack(DamageCalc?.Invoke() ?? 0m)
                .FromMonster(owner.Monster)
                .WithHitCount(Repeats)
                .Execute(choiceContext);
        }

        await AfterDefaultCounterAttack(choiceContext, owner, counterTarget);
    }

    protected virtual Task AfterDefaultCounterAttack(
        PlayerChoiceContext choiceContext,
        Creature owner,
        Creature counterTarget)
    {
        return Task.CompletedTask;
    }
}

public sealed class CombinedCounterAttackDebuffIntent : CombinedCounterAttackIntentBase
{
    public CombinedCounterAttackDebuffIntent(
        Func<decimal> damageCalc,
        Func<int>? repeatCalc = null,
        string? descriptionKey = null,
        Func<PlayerChoiceContext, Creature, Creature, Task>? perform = null,
        params IntentBadge[] badges)
        : base(damageCalc, repeatCalc, descriptionKey, perform, badges)
    {
    }

    protected override string AttackKind => CombinedIntentAnimData.CounterAttackDebuff;

    protected override string IntentPrefix => "COUNTER_COMBINED_ATTACK_DEBUFF";
}

public sealed class CombinedCounterAttackBuffIntent : CombinedCounterAttackIntentBase
{
    public CombinedCounterAttackBuffIntent(
        Func<decimal> damageCalc,
        Func<int>? repeatCalc = null,
        string? descriptionKey = null,
        Func<PlayerChoiceContext, Creature, Creature, Task>? perform = null,
        params IntentBadge[] badges)
        : base(damageCalc, repeatCalc, descriptionKey, perform, badges)
    {
    }

    protected override string AttackKind => CombinedIntentAnimData.CounterAttackBuff;

    protected override string IntentPrefix => "COUNTER_COMBINED_ATTACK_BUFF";
}

public sealed class CombinedCounterAttackDefendIntent : CombinedCounterAttackIntentBase
{
    public CombinedCounterAttackDefendIntent(
        Func<decimal> damageCalc,
        Func<int>? repeatCalc = null,
        string? descriptionKey = null,
        Func<PlayerChoiceContext, Creature, Creature, Task>? perform = null,
        int blockAmount = 0,
        params IntentBadge[] badges)
        : base(damageCalc, repeatCalc, descriptionKey, perform, badges)
    {
        BlockAmount = blockAmount;
    }

    public int BlockAmount { get; }

    protected override string AttackKind => CombinedIntentAnimData.CounterAttackDefend;

    protected override string IntentPrefix => "COUNTER_COMBINED_ATTACK_DEFEND";

    protected override async Task AfterDefaultCounterAttack(
        PlayerChoiceContext choiceContext,
        Creature owner,
        Creature counterTarget)
    {
        if (BlockAmount > 0m && !owner.IsDead)
        {
            await CreatureCmd.GainBlock(owner, BlockAmount, ValueProp.Move, null);
        }
    }

    protected override void AddDescriptionVariables(
        LocString desc,
        IEnumerable<Creature> targets,
        Creature owner)
    {
        if (BlockAmount <= 0)
        {
            return;
        }

        desc.Add("Amount", BlockAmount);
        desc.Add("BlockAmount", BlockAmount);
    }
}

/// <summary>格挡+强化：STS1 defendBuff 图标 + buffVFX。</summary>
public sealed class CombinedDefendBuffIntent :
    DefendIntent,
    IIntentEffectProvider,
    ICombinedIntentVisual,
    ICombinedIntentHoverIcon,
    ICompositeIntent
{
    private readonly string? _descriptionKey;

    public CombinedDefendBuffIntent(
        int blockAmount = 0,
        string? descriptionKey = null,
        params IntentBadge[] badges)
    {
        BlockAmount = blockAmount;
        _descriptionKey = descriptionKey;
        Effects = IntentEffectCollection.Create(badges);
        Composite = CompositeIntent.FromVisualKind(
            CombinedIntentAnimData.DefendBuff,
            IntentType,
            CompositeIntent.ResolveTargeting(this, Effects),
            Effects);
    }

    public IReadOnlyList<IntentBadge> Effects { get; }

    public CompositeIntent Composite { get; }

    public int BlockAmount { get; }

    protected override string IntentPrefix => "COMBINED_DEFEND_BUFF";

    public string HoverIconPath => CombinedIntentAnimData.GetHoverIconPath(CombinedIntentAnimData.DefendBuff);

    public string HoverIntentPrefix => IntentPrefix;

    public override IEnumerable<string> AssetPaths =>
        CombinedIntentAnimData.GetAssetPaths(CombinedIntentAnimData.DefendBuff)
            .Append(HoverIconPath)
            .Concat(Effects.SelectMany(static effect => effect.AssetPaths));

    public string GetCombinedAnimation(IEnumerable<Creature> targets, Creature owner) =>
        CombinedIntentAnimData.GetAnimationKey(CombinedIntentAnimData.DefendBuff);

    public override Texture2D GetTexture(IEnumerable<Creature> targets, Creature owner)
    {
        return PreloadManager.Cache.GetTexture2D(
            CombinedIntentAnimData.GetIconPath(CombinedIntentAnimData.DefendBuff, tier: null));
    }

    public override string GetAnimation(IEnumerable<Creature> targets, Creature owner)
    {
        return IntentAnimData.buff;
    }

    protected override LocString GetIntentDescription(IEnumerable<Creature> targets, Creature owner)
    {
        LocString desc = BadgedIntentDescription.Create(_descriptionKey, owner, IntentPrefix);
        desc.Add("Amount", BlockAmount);
        desc.Add("BlockAmount", BlockAmount);
        BadgedIntentDescription.AddBadgeVariables(desc, Effects);
        return desc;
    }
}

/// <summary>格挡+负面：STS2 格挡图标 + debuffVFX。</summary>
public sealed class CombinedDefendDebuffIntent :
    DefendIntent,
    IIntentEffectProvider,
    ICombinedIntentVisual,
    ICombinedIntentHoverIcon,
    ICompositeIntent
{
    private readonly string? _descriptionKey;
    private readonly int? _descriptionBadgeAmount;

    public CombinedDefendDebuffIntent(
        int blockAmount = 0,
        string? descriptionKey = null,
        params IntentBadge[] badges)
    {
        BlockAmount = blockAmount;
        _descriptionKey = descriptionKey;
        Effects = IntentEffectCollection.Create(badges);
        Composite = CompositeIntent.FromVisualKind(
            CombinedIntentAnimData.DefendDebuff,
            IntentType,
            CompositeIntent.ResolveTargeting(this, Effects),
            Effects);
    }

    public CombinedDefendDebuffIntent(
        int blockAmount,
        string? descriptionKey,
        int descriptionBadgeAmount)
        : this(blockAmount, descriptionKey)
    {
        _descriptionBadgeAmount = descriptionBadgeAmount;
    }

    public IReadOnlyList<IntentBadge> Effects { get; }

    public CompositeIntent Composite { get; }

    public int BlockAmount { get; }

    protected override string IntentPrefix => "COMBINED_DEFEND_DEBUFF";

    public string HoverIconPath => CombinedIntentAnimData.GetHoverIconPath(CombinedIntentAnimData.DefendDebuff);

    public string HoverIntentPrefix => IntentPrefix;

    public override IEnumerable<string> AssetPaths =>
        CombinedIntentAnimData.GetAssetPaths(CombinedIntentAnimData.DefendDebuff)
            .Append(HoverIconPath)
            .Concat(Effects.SelectMany(static effect => effect.AssetPaths));

    public string GetCombinedAnimation(IEnumerable<Creature> targets, Creature owner) =>
        CombinedIntentAnimData.GetAnimationKey(CombinedIntentAnimData.DefendDebuff);

    public override Texture2D GetTexture(IEnumerable<Creature> targets, Creature owner)
    {
        return PreloadManager.Cache.GetTexture2D(
            CombinedIntentAnimData.GetIconPath(CombinedIntentAnimData.DefendDebuff, tier: null));
    }

    public override string GetAnimation(IEnumerable<Creature> targets, Creature owner)
    {
        return IntentAnimData.buff;
    }

    protected override LocString GetIntentDescription(IEnumerable<Creature> targets, Creature owner)
    {
        LocString desc = BadgedIntentDescription.Create(_descriptionKey, owner, IntentPrefix);
        desc.Add("Amount", BlockAmount);
        desc.Add("BlockAmount", BlockAmount);
        BadgedIntentDescription.AddBadgeVariables(desc, Effects);
        if (_descriptionBadgeAmount.HasValue)
        {
            desc.Add("BadgeAmount", _descriptionBadgeAmount.Value);
        }

        return desc;
    }
}

public sealed class CombinedCounterDefendBuffIntent :
    DefendIntent,
    IIntentEffectProvider,
    ICombinedIntentVisual,
    ICombinedIntentHoverIcon,
    ICompositeIntent
{
    private readonly string? _descriptionKey;

    public CombinedCounterDefendBuffIntent(
        int blockAmount = 0,
        string? descriptionKey = null,
        params IntentBadge[] badges)
    {
        BlockAmount = blockAmount;
        _descriptionKey = descriptionKey;
        Effects = IntentEffectCollection.Create(badges);
        Composite = CompositeIntent.FromVisualKind(
            CombinedIntentAnimData.CounterDefendBuff,
            IntentType,
            CompositeIntent.ResolveTargeting(this, Effects),
            Effects);
    }

    public IReadOnlyList<IntentBadge> Effects { get; }

    public CompositeIntent Composite { get; }

    public int BlockAmount { get; }

    protected override string IntentPrefix => "COUNTER_COMBINED_DEFEND_BUFF";

    public string HoverIconPath => CombinedIntentAnimData.GetHoverIconPath(CombinedIntentAnimData.CounterDefendBuff);

    public string HoverIntentPrefix => IntentPrefix;

    public override IEnumerable<string> AssetPaths =>
        CombinedIntentAnimData.GetAssetPaths(CombinedIntentAnimData.CounterDefendBuff)
            .Append(HoverIconPath)
            .Concat(Effects.SelectMany(static effect => effect.AssetPaths));

    public string GetCombinedAnimation(IEnumerable<Creature> targets, Creature owner) =>
        CombinedIntentAnimData.GetAnimationKey(CombinedIntentAnimData.CounterDefendBuff);

    public override Texture2D GetTexture(IEnumerable<Creature> targets, Creature owner)
    {
        return PreloadManager.Cache.GetTexture2D(
            CombinedIntentAnimData.GetIconPath(CombinedIntentAnimData.CounterDefendBuff, tier: null));
    }

    public override string GetAnimation(IEnumerable<Creature> targets, Creature owner)
    {
        return IntentAnimData.buff;
    }

    protected override LocString GetIntentDescription(IEnumerable<Creature> targets, Creature owner)
    {
        LocString desc = BadgedIntentDescription.Create(_descriptionKey, owner, IntentPrefix);
        desc.Add("Amount", BlockAmount);
        desc.Add("BlockAmount", BlockAmount);
        BadgedIntentDescription.AddBadgeVariables(desc, Effects);
        return desc;
    }
}

public sealed class CombinedCounterDefendDebuffIntent :
    DefendIntent,
    IIntentEffectProvider,
    ICombinedIntentVisual,
    ICombinedIntentHoverIcon,
    ICompositeIntent
{
    private readonly string? _descriptionKey;

    public CombinedCounterDefendDebuffIntent(
        int blockAmount = 0,
        string? descriptionKey = null,
        params IntentBadge[] badges)
    {
        BlockAmount = blockAmount;
        _descriptionKey = descriptionKey;
        Effects = IntentEffectCollection.Create(badges);
        Composite = CompositeIntent.FromVisualKind(
            CombinedIntentAnimData.CounterDefendDebuff,
            IntentType,
            CompositeIntent.ResolveTargeting(this, Effects),
            Effects);
    }

    public IReadOnlyList<IntentBadge> Effects { get; }

    public CompositeIntent Composite { get; }

    public int BlockAmount { get; }

    protected override string IntentPrefix => "COUNTER_COMBINED_DEFEND_DEBUFF";

    public string HoverIconPath => CombinedIntentAnimData.GetHoverIconPath(CombinedIntentAnimData.CounterDefendDebuff);

    public string HoverIntentPrefix => IntentPrefix;

    public override IEnumerable<string> AssetPaths =>
        CombinedIntentAnimData.GetAssetPaths(CombinedIntentAnimData.CounterDefendDebuff)
            .Append(HoverIconPath)
            .Concat(Effects.SelectMany(static effect => effect.AssetPaths));

    public string GetCombinedAnimation(IEnumerable<Creature> targets, Creature owner) =>
        CombinedIntentAnimData.GetAnimationKey(CombinedIntentAnimData.CounterDefendDebuff);

    public override Texture2D GetTexture(IEnumerable<Creature> targets, Creature owner)
    {
        return PreloadManager.Cache.GetTexture2D(
            CombinedIntentAnimData.GetIconPath(CombinedIntentAnimData.CounterDefendDebuff, tier: null));
    }

    public override string GetAnimation(IEnumerable<Creature> targets, Creature owner)
    {
        return IntentAnimData.buff;
    }

    protected override LocString GetIntentDescription(IEnumerable<Creature> targets, Creature owner)
    {
        LocString desc = BadgedIntentDescription.Create(_descriptionKey, owner, IntentPrefix);
        desc.Add("Amount", BlockAmount);
        desc.Add("BlockAmount", BlockAmount);
        BadgedIntentDescription.AddBadgeVariables(desc, Effects);
        return desc;
    }
}

/// <summary>魔法：STS1 静态图标，无动画。</summary>
public class CombinedMagicIntent :
    AbstractIntent,
    IIntentEffectProvider,
    ICombinedIntentVisual,
    ICombinedIntentHoverIcon,
    ICompositeIntent
{
    private readonly string? _descriptionKey;
    private readonly bool _large;

    public CombinedMagicIntent(
        string? descriptionKey = null,
        bool large = false,
        params IntentBadge[] badges)
    {
        _descriptionKey = descriptionKey;
        _large = large;
        Effects = IntentEffectCollection.Create(badges);
        Composite = CompositeIntent.FromVisualKind(
            IntentKind,
            IntentType,
            CompositeIntent.ResolveTargeting(this, Effects),
            Effects);
    }

    private string IntentKind => _large ? CombinedIntentAnimData.MagicLarge : CombinedIntentAnimData.Magic;

    public IReadOnlyList<IntentBadge> Effects { get; }

    public CompositeIntent Composite { get; }

    public string HoverIconPath => CombinedIntentAnimData.GetHoverIconPath(IntentKind);

    public string HoverIntentPrefix => IntentPrefix;

    public override IntentType IntentType => IntentType.Unknown;

    protected override string IntentPrefix => "COMBINED_MAGIC";

    protected override string? SpritePath => null;

    public override IEnumerable<string> AssetPaths =>
        CombinedIntentAnimData.GetAssetPaths(IntentKind)
            .Append(HoverIconPath)
            .Concat(Effects.SelectMany(static effect => effect.AssetPaths));

    public string GetCombinedAnimation(IEnumerable<Creature> targets, Creature owner)
    {
        return CombinedIntentAnimData.GetAnimationKey(IntentKind);
    }

    public override Texture2D GetTexture(IEnumerable<Creature> targets, Creature owner)
    {
        return PreloadManager.Cache.GetTexture2D(CombinedIntentAnimData.GetIconPath(IntentKind, tier: null));
    }

    public override string GetAnimation(IEnumerable<Creature> targets, Creature owner)
    {
        return IntentAnimData.unknown;
    }

    protected override LocString GetIntentDescription(IEnumerable<Creature> targets, Creature owner)
    {
        LocString desc = BadgedIntentDescription.Create(_descriptionKey, owner, IntentPrefix);
        BadgedIntentDescription.AddBadgeVariables(desc, Effects);
        return desc;
    }
}

public sealed class CombinedTargetedMagicIntent :
    CombinedMagicIntent,
    ITargetedIntentIndicator,
    IIntentTargetLineProvider
{
    private readonly string? _descriptionKey;
    private readonly Func<Creature, Creature?> _targetResolver;

    public CombinedTargetedMagicIntent(
        string? descriptionKey,
        Func<Creature, Creature?> targetResolver,
        bool large = false,
        params IntentBadge[] badges)
        : base(descriptionKey, large, badges)
    {
        _descriptionKey = descriptionKey;
        _targetResolver = targetResolver
            ?? throw new ArgumentNullException(nameof(targetResolver));
    }

    protected override LocString GetIntentDescription(
        IEnumerable<Creature> targets,
        Creature owner)
    {
        Creature? target = ResolveTarget(targets, owner);
        LocString description = BadgedIntentDescription.Create(
            _descriptionKey,
            owner,
            "COMBINED_MAGIC");
        description.Add("TargetName", target?.Name ?? "Unknown Target");
        BadgedIntentDescription.AddBadgeVariables(description, Effects);
        return description;
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
            : [new IntentTargetLineTarget(target, "CombinedTargetedMagic")];
    }

    private Creature? ResolveTarget(
        IEnumerable<Creature> targets,
        Creature owner)
    {
        Creature? target = _targetResolver(owner);
        return target is { IsAlive: true }
            ? target
            : TargetedMonsterAttackHelper.GetPrimaryTarget(
                owner,
                targets as IReadOnlyList<Creature>);
    }
}
