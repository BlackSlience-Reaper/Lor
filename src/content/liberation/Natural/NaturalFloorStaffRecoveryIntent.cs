using System.Linq;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace LibraryOfRuina.content.liberation.Natural;

public sealed class NaturalFloorStaffRecoveryIntent : HealIntent
{
    protected override LocString GetIntentDescription(IEnumerable<Creature> targets, Creature owner)
    {
        var description = new LocString("intents", "NATURAL_HERMIT_STAY_PUT.description");
        description.Add("HealPercent", NaturalFloorGreenStemHermit.StaffHealPercent);
        return description;
    }
}
