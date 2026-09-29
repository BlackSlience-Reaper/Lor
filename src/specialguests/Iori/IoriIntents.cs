using System;
using System.Linq;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.intents;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace LibraryOfRuina.specialguests.Iori;

// 伊织按姿态结算伤害类型的攻击意图：预览数值走一次带伤害类型的 ModifyDamage（见 IoriMonsterBase.ResolveTypedPreviewDamage）。

internal sealed class IoriTypedAttackIntent : AttackIntent
{
    private readonly IoriMonsterBase _iori;
    private readonly Func<int> _baseDamage;
    private readonly Func<LibraryDamageType> _damageType;
    private readonly Func<int> _repeats;

    internal IoriTypedAttackIntent(
        IoriMonsterBase iori,
        Func<int> baseDamage,
        Func<LibraryDamageType> damageType,
        Func<int> repeats)
    {
        _iori = iori;
        _baseDamage = baseDamage;
        _damageType = damageType;
        _repeats = repeats;
        DamageCalc = static () => 0m;
    }

    public override int Repeats => Math.Max(1, _repeats());

    protected override LocString IntentLabelFormat =>
        Repeats > 1
            ? new LocString("intents", "FORMAT_DAMAGE_MULTI")
            : new LocString("intents", "FORMAT_DAMAGE_SINGLE");

    public override int GetTotalDamage(
        IEnumerable<Creature> targets,
        Creature owner) => PreviewDamage(owner) * Repeats;

    public override LocString GetIntentLabel(
        IEnumerable<Creature> targets,
        Creature owner)
    {
        var label = IntentLabelFormat;
        label.Add("Damage", PreviewDamage(owner));
        if (Repeats > 1)
        {
            label.Add("Repeat", Repeats);
        }
        return label;
    }

    protected override LocString GetIntentDescription(
        IEnumerable<Creature> targets,
        Creature owner)
    {
        LocString description = BadgedIntentDescription.Create(
            null,
            owner,
            "ATTACK");
        description.Add("Damage", PreviewDamage(owner));
        description.Add("Repeat", Repeats);
        return description;
    }

    private int PreviewDamage(Creature owner) =>
        _iori.ResolveTypedPreviewDamage(
            _baseDamage(),
            _damageType(),
            IoriIntentPreviewTarget.Resolve(owner));
}

internal enum IoriTypedCombinedAttackKind
{
    Buff,
    Debuff,
    Defend,
}

internal sealed class IoriTypedCombinedAttackIntent :
    CombinedAttackIntentBase
{
    private readonly IoriMonsterBase _iori;
    private readonly Func<int> _baseDamage;
    private readonly Func<LibraryDamageType> _damageType;
    private readonly IoriTypedCombinedAttackKind _kind;

    internal IoriTypedCombinedAttackIntent(
        IoriMonsterBase iori,
        Func<int> baseDamage,
        Func<LibraryDamageType> damageType,
        Func<int> repeats,
        IoriTypedCombinedAttackKind kind,
        int blockAmount,
        params IntentBadge[] badges)
        : base(static () => 0m, repeats, null, badges)
    {
        _iori = iori;
        _baseDamage = baseDamage;
        _damageType = damageType;
        _kind = kind;
        BlockAmount = blockAmount;
    }

    internal int BlockAmount { get; }

    protected override string AttackKind => _kind switch
    {
        IoriTypedCombinedAttackKind.Buff =>
            CombinedIntentAnimData.AttackBuff,
        IoriTypedCombinedAttackKind.Debuff =>
            CombinedIntentAnimData.AttackDebuff,
        _ => CombinedIntentAnimData.AttackDefend,
    };

    protected override string IntentPrefix => _kind switch
    {
        IoriTypedCombinedAttackKind.Buff => "COMBINED_ATTACK_BUFF",
        IoriTypedCombinedAttackKind.Debuff => "COMBINED_ATTACK_DEBUFF",
        _ => "COMBINED_ATTACK_DEFEND",
    };

    public override int GetTotalDamage(
        IEnumerable<Creature> targets,
        Creature owner) => PreviewDamage(owner) * Repeats;

    public override LocString GetIntentLabel(
        IEnumerable<Creature> targets,
        Creature owner)
    {
        LocString label = Repeats > 1
            ? new LocString("intents", "FORMAT_DAMAGE_MULTI")
            : new LocString("intents", "FORMAT_DAMAGE_SINGLE");
        label.Add("Damage", PreviewDamage(owner));
        if (Repeats > 1)
        {
            label.Add("Repeat", Repeats);
        }
        return label;
    }

    protected override LocString GetIntentDescription(
        IEnumerable<Creature> targets,
        Creature owner)
    {
        LocString description = BadgedIntentDescription.Create(
            null,
            owner,
            IntentPrefix);
        description.Add("Damage", PreviewDamage(owner));
        description.Add("Repeat", Repeats);
        if (BlockAmount > 0)
        {
            description.Add("Amount", BlockAmount);
            description.Add("BlockAmount", BlockAmount);
        }
        BadgedIntentDescription.AddBadgeVariables(description, Effects);
        return description;
    }

    private int PreviewDamage(Creature owner) =>
        _iori.ResolveTypedPreviewDamage(
            _baseDamage(),
            _damageType(),
            IoriIntentPreviewTarget.Resolve(owner));
}

internal static class IoriIntentPreviewTarget
{
    internal static Creature? Resolve(Creature owner)
    {
        if (owner.CombatState == null)
        {
            return null;
        }

        return LocalContextCompat.GetMe(owner.CombatState)?.Creature
            is { IsAlive: true } local
                ? local
                : owner.CombatState.PlayerCreatures.FirstOrDefault(
                    static creature => creature.IsAlive);
    }
}
