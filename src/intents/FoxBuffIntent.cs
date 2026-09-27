using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace LibraryOfRuina.intents;

public class FoxBuffIntent : BuffIntent
{
    public int Amount { get; }

    public FoxBuffIntent(int amount)
    {
        Amount = amount;
    }

    protected override LocString GetIntentDescription(IEnumerable<Creature> targets, Creature owner)
    {
        LocString desc = LocString.GetIfExists("intents", "FOX_BUFF.description") ?? base.GetIntentDescription(targets, owner);
        desc.Add("Amount", Amount);
        return desc;
    }
}
