using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace LibraryOfRuina.intents;

public sealed class FoxAttackIntent : AttackIntent, ITargetedIntentIndicator
{
    private readonly int _repeats;

    public override int Repeats => _repeats;

    protected override LocString IntentLabelFormat =>
        Repeats > 1
            ? new LocString("intents", "FORMAT_DAMAGE_MULTI")
            : new LocString("intents", "FORMAT_DAMAGE_SINGLE");

    public FoxAttackIntent(int damage, int repeats = 1)
    {
        DamageCalc = () => damage;
        _repeats = repeats < 1 ? 1 : repeats;
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
        LocString desc = LocString.GetIfExists("intents", "FOX_ATTACK.description") ?? base.GetIntentDescription(targets, owner);
        desc.Add("Damage", GetSingleDamage(targets, owner));
        desc.Add("Repeat", Repeats);
        desc.Add("TargetName", TargetedMonsterAttackHelper.GetPrimaryTargetName(owner, targets as IReadOnlyList<Creature>));
        return desc;
    }
}
