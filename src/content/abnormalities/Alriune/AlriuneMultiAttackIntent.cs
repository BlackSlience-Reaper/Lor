using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace LibraryOfRuina.content.abnormalities.Alriune;

internal sealed class AlriuneMultiAttackIntent(int damage, int repeats, string descriptionKey)
    : MultiAttackIntent(damage, repeats)
{
    protected override LocString GetIntentDescription(IEnumerable<Creature> targets, Creature owner)
    {
        LocString text = new("intents", descriptionKey);
        text.Add("Damage", GetSingleDamage(targets, owner));
        text.Add("Repeat", Repeats);
        return text;
    }
}
