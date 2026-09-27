using System;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace LibraryOfRuina.intents;





public class DynamicAttackIntent : AttackIntent
{
    private readonly Func<int> _repeatCalc;

    public override int Repeats => _repeatCalc();

    protected override LocString IntentLabelFormat =>
        Repeats > 1
            ? new LocString("intents", "FORMAT_DAMAGE_MULTI")
            : new LocString("intents", "FORMAT_DAMAGE_SINGLE");

    public DynamicAttackIntent(int damage, Func<int> repeatCalc)
        : this(() => damage, repeatCalc)
    {
    }

    public DynamicAttackIntent(Func<decimal> damageCalc, Func<int> repeatCalc)
    {
        DamageCalc = damageCalc;
        _repeatCalc = repeatCalc;
    }

    public override int GetTotalDamage(IEnumerable<Creature> targets, Creature owner)
    {
        return GetSingleDamage(targets, owner) * Repeats;
    }

    public override LocString GetIntentLabel(IEnumerable<Creature> targets, Creature owner)
    {
        LocString fmt = IntentLabelFormat;
        int dmg = GetSingleDamage(targets, owner);
        fmt.Add("Damage", dmg);
        if (Repeats > 1)
            fmt.Add("Repeat", Repeats);
        return fmt;
    }
}
