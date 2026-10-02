using System;
using System.Linq;
using MegaCrit.Sts2.Core.Localization;
using LibraryOfRuina.framework.intents;

namespace LibraryOfRuina.content.liberation.Religion;

internal sealed class ReligionFloorAttackIntent(Func<decimal> damage, Func<int> hits)
    : DynamicAttackIntent(damage, hits), IIntentTargetLineProvider
{
    public IReadOnlyList<IntentTargetLineTarget> GetIntentTargetLineTargets(Creature owner, IReadOnlyList<Creature>? fallbackTargets) =>
        (owner.CombatState?.PlayerCreatures ?? fallbackTargets ?? [])
            .Where(static creature => creature.IsPlayer && creature.IsAlive)
            .Select(static creature => new IntentTargetLineTarget(creature))
            .ToArray();

    protected override LocString GetIntentDescription(IEnumerable<Creature> targets, Creature owner)
    {
        var text = new LocString("intents", "RELIGION_ALL_PLAYERS_ATTACK.description");
        text.Add("Damage", GetSingleDamage(targets, owner));
        text.Add("Repeat", Repeats);
        return text;
    }
}
