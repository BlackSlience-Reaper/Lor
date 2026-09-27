using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace LibraryOfRuina.intents;

public sealed class FoxWeakIntent : DebuffIntent, ITargetedIntentIndicator
{
    public int Amount { get; }

    public FoxWeakIntent(int amount)
    {
        Amount = amount;
    }

    protected override LocString GetIntentDescription(IEnumerable<Creature> targets, Creature owner)
    {
        LocString desc = LocString.GetIfExists("intents", "FOX_WEAK.description") ?? base.GetIntentDescription(targets, owner);
        desc.Add("Amount", Amount);
        desc.Add("TargetName", TargetedMonsterAttackHelper.GetPrimaryTargetName(owner, targets as IReadOnlyList<Creature>));
        return desc;
    }
}
