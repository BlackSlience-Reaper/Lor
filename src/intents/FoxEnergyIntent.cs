using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace LibraryOfRuina.intents;

public sealed class FoxEnergyIntent : BuffIntent
{
    public int Amount { get; }

    public FoxEnergyIntent(int amount)
    {
        Amount = amount;
    }

    protected override LocString GetIntentDescription(IEnumerable<Creature> targets, Creature owner)
    {
        LocString desc = LocString.GetIfExists("intents", "FOX_ENERGY.description") ?? base.GetIntentDescription(targets, owner);
        desc.Add("Amount", Amount);
        return desc;
    }
}
