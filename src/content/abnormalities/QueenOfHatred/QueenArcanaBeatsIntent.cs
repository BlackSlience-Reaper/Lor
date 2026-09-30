using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using LibraryOfRuina.framework.intents;

namespace LibraryOfRuina.content.abnormalities.QueenOfHatred;

public sealed class QueenArcanaBeatsIntent : DebuffIntent, ITargetedIntentIndicator
{
    protected override LocString GetIntentDescription(IEnumerable<Creature> targets, Creature owner)
    {
        LocString desc = new("intents", "QUEEN_ARCANA_BEATS.description");
        desc.Add("TargetName", TargetedMonsterAttackHelper.GetPrimaryTargetName(owner, targets as IReadOnlyList<Creature>));
        return desc;
    }
}
