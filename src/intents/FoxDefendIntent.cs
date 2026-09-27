using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace LibraryOfRuina.intents;

public class FoxDefendIntent : DefendIntent
{
    public int BlockAmount { get; }

    public FoxDefendIntent(int blockAmount)
    {
        BlockAmount = blockAmount;
    }

    protected override LocString GetIntentDescription(IEnumerable<Creature> targets, Creature owner)
    {
        LocString desc = LocString.GetIfExists("intents", "FOX_DEFEND.description") ?? base.GetIntentDescription(targets, owner);
        desc.Add("Amount", BlockAmount);
        return desc;
    }
}
