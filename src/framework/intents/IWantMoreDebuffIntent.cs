using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace LibraryOfRuina.framework.intents;

public sealed class IWantMoreDebuffIntent : DebuffIntent
{
    protected override LocString GetIntentDescription(IEnumerable<Creature> targets, Creature owner)
    {
        return LocString.GetIfExists("intents", "I_WANT_MORE_DEBUFF.description")
            ?? base.GetIntentDescription(targets, owner);
    }
}
