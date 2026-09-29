using System;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace LibraryOfRuina.framework.intents;

public sealed class TargetedMonsterAttackIntent : AttackIntent, ITargetedIntentIndicator
{
    private readonly Func<int> _repeatCalc;
    private readonly string _descriptionKey;

    public override int Repeats => Math.Max(1, _repeatCalc());

    protected override LocString IntentLabelFormat =>
        Repeats > 1
            ? new LocString("intents", "FORMAT_DAMAGE_MULTI")
            : new LocString("intents", "FORMAT_DAMAGE_SINGLE");

    public TargetedMonsterAttackIntent(int damage, int repeats, string descriptionKey)
        : this(() => damage, () => repeats, descriptionKey)
    {
    }

    public TargetedMonsterAttackIntent(Func<int> damageCalc, Func<int>? repeatCalc, string descriptionKey)
    {
        DamageCalc = () => damageCalc();
        _repeatCalc = repeatCalc ?? (() => 1);
        _descriptionKey = descriptionKey;
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

    protected override LocString GetIntentDescription(IEnumerable<Creature> targets, Creature owner)
    {
        LocString desc = new("intents", _descriptionKey);
        desc.Add("Damage", GetTargetedSingleDamage(targets, owner));
        desc.Add("Repeat", Repeats);
        desc.Add("TargetName", TargetedMonsterAttackHelper.GetPrimaryTargetName(owner, targets as IReadOnlyList<Creature>));
        return desc;
    }

    private int GetTargetedSingleDamage(IEnumerable<Creature> targets, Creature owner)
    {
        Creature? target = TargetedMonsterAttackHelper.GetPrimaryTarget(owner, targets as IReadOnlyList<Creature>);
        return TargetedAttackIntentPreviewHelper.GetModifiedDamage(owner, target, DamageCalc?.Invoke() ?? 0m);
    }
}
