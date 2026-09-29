using MegaCrit.Sts2.Core.Localization;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.monsters.NaturalFloorLiberation;

namespace LibraryOfRuina.intents.NaturalFloorLiberation;

public sealed class NaturalFloorDespairSwordIntent : CombinedAttackIntentBase
{
    private readonly int _percent;
    private readonly bool _debuff;

    public NaturalFloorDespairSwordIntent(
        int damage,
        int hits,
        string description,
        int percent,
        bool debuff,
        params IntentBadge[] badges)
        : base(() => damage, () => hits, description, badges)
    {
        _percent = percent;
        _debuff = debuff;
    }

    protected override string AttackKind => _debuff ? CombinedIntentAnimData.AttackDebuff : CombinedIntentAnimData.AttackBuff;

    protected override string IntentPrefix => _debuff ? "COMBINED_ATTACK_DEBUFF" : "COMBINED_ATTACK_BUFF";

    protected override void AddDescriptionVariables(LocString description, IEnumerable<Creature> targets, Creature owner)
    {
        description.Add("BossDamagePercent", _percent);
        description.Add("HealPercent", NaturalFloorForgottenSword.HealPercent);
    }
}
