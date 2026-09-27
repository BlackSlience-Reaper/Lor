using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace LibraryOfRuina.intents;

internal sealed class NaturalFloorGoldRushDefendIntent(int block, string descriptionKey) : DefendIntent
{
    protected override LocString GetIntentDescription(IEnumerable<Creature> targets, Creature owner)
    {
        LocString description = new("intents", descriptionKey);
        description.Add("Amount", block);
        return description;
    }
}
