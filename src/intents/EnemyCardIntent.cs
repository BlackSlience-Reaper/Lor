using System;
using System.Linq;
using Godot;
using LibraryOfRuina.cards;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Intents;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace LibraryOfRuina.intents;

internal interface IEnemyCardIntent
{
    EnemyCardSpec EnemyCard { get; }
}

public sealed class EnemyCardIntent : AbstractIntent, IEnemyCardIntent
{
    private readonly EnemyCardSpec _card;
    private readonly Func<decimal>? _damageCalc;
    private readonly Func<int>? _repeatCalc;
    private readonly Func<IntentType> _intentType;
    private readonly Func<string> _intentPrefix;
    private readonly Func<string?> _spritePath;

    public EnemyCardIntent(
        EnemyCardSpec card,
        Func<decimal>? damageCalc = null,
        Func<int>? repeatCalc = null,
        IntentType intentType = IntentType.Attack,
        string intentPrefix = "ATTACK",
        string? spritePath = "atlases/intent_atlas.sprites/intent_attack.tres")
        : this(
            card,
            damageCalc,
            repeatCalc,
            () => intentType,
            () => intentPrefix,
            () => spritePath)
    {
    }

    public EnemyCardIntent(
        EnemyCardSpec card,
        Func<decimal>? damageCalc,
        Func<int>? repeatCalc,
        Func<IntentType> intentType,
        Func<string> intentPrefix,
        Func<string?> spritePath)
    {
        _card = card ?? throw new ArgumentNullException(nameof(card));
        _damageCalc = damageCalc;
        _repeatCalc = repeatCalc;
        _intentType = intentType ?? throw new ArgumentNullException(nameof(intentType));
        _intentPrefix = intentPrefix ?? throw new ArgumentNullException(nameof(intentPrefix));
        _spritePath = spritePath ?? throw new ArgumentNullException(nameof(spritePath));
    }

    public override IntentType IntentType => _intentType();

    public EnemyCardSpec EnemyCard => _card;

    protected override string IntentPrefix => _intentPrefix();

    protected override string? SpritePath => _spritePath();

    public int Repeats => Math.Max(1, _repeatCalc?.Invoke() ?? 1);

    protected override LocString? IntentLabelFormat =>
        _damageCalc == null
            ? base.IntentLabelFormat
            : new LocString("intents", Repeats > 1 ? "FORMAT_DAMAGE_MULTI" : "FORMAT_DAMAGE_SINGLE");

    public override IEnumerable<string> AssetPaths =>
        base.AssetPaths.Concat(_card.AssetPaths);

    public override LocString GetIntentLabel(IEnumerable<Creature> targets, Creature owner)
    {
        if (_damageCalc == null)
        {
            return base.GetIntentLabel(targets, owner);
        }

        LocString fmt = IntentLabelFormat!;
        fmt.Add("Damage", GetSingleDamage(targets, owner));
        if (Repeats > 1)
        {
            fmt.Add("Repeat", Repeats);
        }

        return fmt;
    }

    protected override LocString GetIntentDescription(IEnumerable<Creature> targets, Creature owner)
    {
        LocString description = base.GetIntentDescription(targets, owner);
        if (_damageCalc != null)
        {
            description.Add("Damage", GetSingleDamage(targets, owner));
            description.Add("Repeat", Repeats);
        }

        return description;
    }

    private int GetSingleDamage(IEnumerable<Creature> targets, Creature owner)
    {
        if (_damageCalc == null)
        {
            return 0;
        }

        return Math.Max(0, (int)_damageCalc());
    }
}

public sealed class EnemyCardAttackIntent(
    EnemyCardSpec card,
    Func<decimal> damageCalc,
    Func<int>? repeatCalc = null,
    params Func<decimal>[] additionalDamageCalcs)
    : SingleAttackIntent(damageCalc), IEnemyCardIntent, IEnemyCardPreviewConfigurator
{
    private readonly EnemyCardSpec _card = card ?? throw new ArgumentNullException(nameof(card));
    private readonly Func<int> _repeatCalc = repeatCalc ?? (() => 1);
    private readonly Func<decimal>[] _additionalDamageCalcs = additionalDamageCalcs ?? [];

    public override int Repeats => Math.Max(1, _repeatCalc());

    public EnemyCardSpec EnemyCard => _card;

    protected override LocString IntentLabelFormat =>
        Repeats > 1
            ? new LocString("intents", "FORMAT_DAMAGE_MULTI")
            : new LocString("intents", "FORMAT_DAMAGE_SINGLE");

    public override IEnumerable<string> AssetPaths =>
        base.AssetPaths.Concat(_card.AssetPaths);

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

    public void ConfigureEnemyCardPreview(
        CardModel card,
        IEnumerable<Creature> targets,
        Creature owner)
    {
        if (card is IEnemyAttackPreviewCard previewCard)
        {
            previewCard.SetEnemyAttackPreview(
                GetPreviewDamages(targets, owner),
                Repeats);
        }
    }

    private IReadOnlyList<int> GetPreviewDamages(IEnumerable<Creature> targets, Creature owner)
    {
        int[] damages = new int[1 + _additionalDamageCalcs.Length];
        damages[0] = GetSingleDamage(targets, owner);
        for (int i = 0; i < _additionalDamageCalcs.Length; i++)
        {
            damages[i + 1] = new SingleAttackIntent(_additionalDamageCalcs[i]).GetSingleDamage(targets, owner);
        }

        return damages;
    }
}

public abstract class EnemyCardCombinedAttackIntentBase :
    AttackIntent,
    IEnemyCardIntent,
    ICombinedIntentVisual,
    ICombinedIntentHoverIcon,
    ICompositeIntent
{
    private readonly EnemyCardSpec _card;
    private readonly Func<int> _repeatCalc;
    private readonly string? _descriptionKey;
    private CompositeIntent? _composite;

    protected EnemyCardCombinedAttackIntentBase(
        EnemyCardSpec card,
        Func<decimal> damageCalc,
        Func<int>? repeatCalc = null,
        string? descriptionKey = null)
    {
        _card = card ?? throw new ArgumentNullException(nameof(card));
        DamageCalc = damageCalc ?? throw new ArgumentNullException(nameof(damageCalc));
        _repeatCalc = repeatCalc ?? (() => 1);
        _descriptionKey = descriptionKey;
    }

    public EnemyCardSpec EnemyCard => _card;

    protected abstract string AttackKind { get; }

    public CompositeIntent Composite =>
        _composite ??= CompositeIntent.FromVisualKind(
            AttackKind,
            IntentType,
            CompositeIntentTargeting.Default,
            Array.Empty<IntentBadge>());

    public string HoverIconPath => CombinedIntentAnimData.GetHoverIconPath(AttackKind);

    public string HoverIntentPrefix => IntentPrefix;

    public override int Repeats => Math.Max(1, _repeatCalc());

    public override IEnumerable<string> AssetPaths =>
        CombinedIntentAnimData.GetAssetPaths(AttackKind)
            .Append(HoverIconPath)
            .Concat(_card.AssetPaths);

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
        LocString desc = string.IsNullOrWhiteSpace(_descriptionKey)
            ? new LocString("intents", IntentPrefix + ".description")
            : new LocString("intents", _descriptionKey);
        desc.Add("IsMultiplayer", owner.CombatState != null && owner.CombatState.RunState.Players.Count > 1);
        desc.Add("Damage", GetSingleDamage(targets, owner));
        desc.Add("Repeat", Repeats);
        return desc;
    }
}

public sealed class EnemyCardCombinedAttackDebuffIntent : EnemyCardCombinedAttackIntentBase
{
    public EnemyCardCombinedAttackDebuffIntent(
        EnemyCardSpec card,
        Func<decimal> damageCalc,
        Func<int>? repeatCalc = null,
        string? descriptionKey = null)
        : base(card, damageCalc, repeatCalc, descriptionKey)
    {
    }

    protected override string AttackKind => CombinedIntentAnimData.AttackDebuff;

    protected override string IntentPrefix => "COMBINED_ATTACK_DEBUFF";
}

public sealed class EnemyCardCombinedAttackBuffIntent : EnemyCardCombinedAttackIntentBase
{
    public EnemyCardCombinedAttackBuffIntent(
        EnemyCardSpec card,
        Func<decimal> damageCalc,
        Func<int>? repeatCalc = null,
        string? descriptionKey = null)
        : base(card, damageCalc, repeatCalc, descriptionKey)
    {
    }

    protected override string AttackKind => CombinedIntentAnimData.AttackBuff;

    protected override string IntentPrefix => "COMBINED_ATTACK_BUFF";
}

public sealed class EnemyCardCombinedAttackDefendIntent : EnemyCardCombinedAttackIntentBase
{
    public EnemyCardCombinedAttackDefendIntent(
        EnemyCardSpec card,
        Func<decimal> damageCalc,
        Func<int>? repeatCalc = null,
        string? descriptionKey = null)
        : base(card, damageCalc, repeatCalc, descriptionKey)
    {
    }

    protected override string AttackKind => CombinedIntentAnimData.AttackDefend;

    protected override string IntentPrefix => "COMBINED_ATTACK_DEFEND";
}

public sealed class EnemyCardCombinedDefendBuffIntent :
    DefendIntent,
    IEnemyCardIntent,
    ICombinedIntentVisual,
    ICombinedIntentHoverIcon,
    ICompositeIntent
{
    private readonly EnemyCardSpec _card;
    private readonly string? _descriptionKey;

    public EnemyCardCombinedDefendBuffIntent(
        EnemyCardSpec card,
        string? descriptionKey = null)
    {
        _card = card ?? throw new ArgumentNullException(nameof(card));
        _descriptionKey = descriptionKey;
        Composite = CompositeIntent.FromVisualKind(
            CombinedIntentAnimData.DefendBuff,
            IntentType,
            CompositeIntentTargeting.Default,
            Array.Empty<IntentBadge>());
    }

    public EnemyCardSpec EnemyCard => _card;

    public CompositeIntent Composite { get; }

    protected override string IntentPrefix => "ENEMY_CARD_COMBINED_DEFEND_BUFF";

    public string HoverIconPath => CombinedIntentAnimData.GetHoverIconPath(CombinedIntentAnimData.DefendBuff);

    public string HoverIntentPrefix => IntentPrefix;

    public override IEnumerable<string> AssetPaths =>
        CombinedIntentAnimData.GetAssetPaths(CombinedIntentAnimData.DefendBuff)
            .Append(HoverIconPath)
            .Concat(_card.AssetPaths);

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
        LocString desc = new("intents", _descriptionKey ?? "ENEMY_CARD_COMBINED_DEFEND_BUFF.description");
        desc.Add("IsMultiplayer", owner.CombatState != null && owner.CombatState.RunState.Players.Count > 1);
        return desc;
    }
}

public sealed class EnemyCardCombinedDefendDebuffIntent :
    DefendIntent,
    IEnemyCardIntent,
    ICombinedIntentVisual,
    ICombinedIntentHoverIcon,
    ICompositeIntent
{
    private readonly EnemyCardSpec _card;
    private readonly string? _descriptionKey;

    public EnemyCardCombinedDefendDebuffIntent(
        EnemyCardSpec card,
        string? descriptionKey = null)
    {
        _card = card ?? throw new ArgumentNullException(nameof(card));
        _descriptionKey = descriptionKey;
        Composite = CompositeIntent.FromVisualKind(
            CombinedIntentAnimData.DefendDebuff,
            IntentType,
            CompositeIntentTargeting.Default,
            Array.Empty<IntentBadge>());
    }

    public EnemyCardSpec EnemyCard => _card;

    public CompositeIntent Composite { get; }

    protected override string IntentPrefix => "ENEMY_CARD_COMBINED_DEFEND_DEBUFF";

    public string HoverIconPath => CombinedIntentAnimData.GetHoverIconPath(CombinedIntentAnimData.DefendDebuff);

    public string HoverIntentPrefix => IntentPrefix;

    public override IEnumerable<string> AssetPaths =>
        CombinedIntentAnimData.GetAssetPaths(CombinedIntentAnimData.DefendDebuff)
            .Append(HoverIconPath)
            .Concat(_card.AssetPaths);

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
        LocString desc = new("intents", _descriptionKey ?? "ENEMY_CARD_COMBINED_DEFEND_DEBUFF.description");
        desc.Add("IsMultiplayer", owner.CombatState != null && owner.CombatState.RunState.Players.Count > 1);
        return desc;
    }
}

public sealed class EnemyCardCombinedMagicIntent :
    AbstractIntent,
    IEnemyCardIntent,
    ICombinedIntentVisual,
    ICombinedIntentHoverIcon,
    ICompositeIntent
{
    private readonly EnemyCardSpec _card;
    private readonly string? _descriptionKey;
    private readonly bool _large;

    public EnemyCardCombinedMagicIntent(
        EnemyCardSpec card,
        string? descriptionKey = null,
        bool large = false)
    {
        _card = card ?? throw new ArgumentNullException(nameof(card));
        _descriptionKey = descriptionKey;
        _large = large;
        Composite = CompositeIntent.FromVisualKind(
            IntentKind,
            IntentType,
            CompositeIntentTargeting.Default,
            Array.Empty<IntentBadge>());
    }

    public EnemyCardSpec EnemyCard => _card;

    public CompositeIntent Composite { get; }

    public override IntentType IntentType => IntentType.Unknown;

    protected override string IntentPrefix => "COMBINED_MAGIC";

    protected override string? SpritePath => null;

    private string IntentKind => _large ? CombinedIntentAnimData.MagicLarge : CombinedIntentAnimData.Magic;

    public string HoverIconPath => CombinedIntentAnimData.GetHoverIconPath(IntentKind);

    public string HoverIntentPrefix => IntentPrefix;

    public override IEnumerable<string> AssetPaths =>
        CombinedIntentAnimData.GetAssetPaths(IntentKind)
            .Append(HoverIconPath)
            .Concat(_card.AssetPaths);

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
        LocString desc = new("intents", _descriptionKey ?? IntentPrefix + ".description");
        desc.Add("IsMultiplayer", owner.CombatState != null && owner.CombatState.RunState.Players.Count > 1);
        return desc;
    }
}

public static class EnemyCardCombinedIntentFactory
{
    public static EnemyCardCombinedAttackDebuffIntent AttackDebuff(
        EnemyCardSpec card,
        Func<decimal> damageCalc,
        Func<int>? repeatCalc = null,
        string? descriptionKey = null)
    {
        return new EnemyCardCombinedAttackDebuffIntent(card, damageCalc, repeatCalc, descriptionKey);
    }

    public static EnemyCardCombinedAttackBuffIntent AttackBuff(
        EnemyCardSpec card,
        Func<decimal> damageCalc,
        Func<int>? repeatCalc = null,
        string? descriptionKey = null)
    {
        return new EnemyCardCombinedAttackBuffIntent(card, damageCalc, repeatCalc, descriptionKey);
    }

    public static EnemyCardCombinedAttackDefendIntent AttackDefend(
        EnemyCardSpec card,
        Func<decimal> damageCalc,
        Func<int>? repeatCalc = null,
        string? descriptionKey = null)
    {
        return new EnemyCardCombinedAttackDefendIntent(card, damageCalc, repeatCalc, descriptionKey);
    }

    public static EnemyCardCombinedDefendBuffIntent DefendBuff(
        EnemyCardSpec card,
        string? descriptionKey = null)
    {
        return new EnemyCardCombinedDefendBuffIntent(card, descriptionKey);
    }

    public static EnemyCardCombinedDefendDebuffIntent DefendDebuff(
        EnemyCardSpec card,
        string? descriptionKey = null)
    {
        return new EnemyCardCombinedDefendDebuffIntent(card, descriptionKey);
    }

    public static EnemyCardCombinedMagicIntent Magic(
        EnemyCardSpec card,
        string? descriptionKey = null,
        bool large = false)
    {
        return new EnemyCardCombinedMagicIntent(card, descriptionKey, large);
    }
}
