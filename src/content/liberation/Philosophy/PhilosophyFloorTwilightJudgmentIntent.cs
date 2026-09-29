using System;
using System.Linq;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.intents;
using MegaCrit.Sts2.Core.Localization;

namespace LibraryOfRuina.content.liberation.Philosophy;

public sealed class PhilosophyFloorTwilightJudgmentIntent :
    IndiscriminateAttackIntent
{
    internal const int MaxHpPercent = 20;

    public PhilosophyFloorTwilightJudgmentIntent()
        : base(
            0,
            1,
            "PHILOSOPHY_FLOOR_TWILIGHT_JUDGMENT.description")
    {
    }

    public override int Repeats => 1;

    public override int GetTotalDamage(
        IEnumerable<Creature> targets,
        Creature owner) => ResolveTargets(owner, targets)
        .Select(CalculateDamage)
        .DefaultIfEmpty(0)
        .Max();

    public override LocString GetIntentLabel(
        IEnumerable<Creature> targets,
        Creature owner)
    {
        IReadOnlyList<Creature> resolvedTargets = ResolveTargets(
            owner,
            targets);
        LocString label = new(
            "intents",
            "PHILOSOPHY_FLOOR_TWILIGHT_JUDGMENT.label");
        label.Add("DamageList", BuildDamageList(resolvedTargets));
        return label;
    }

    protected override LocString GetIntentDescription(
        IEnumerable<Creature> targets,
        Creature owner)
    {
        IReadOnlyList<Creature> resolvedTargets = ResolveTargets(
            owner,
            targets);
        LocString description = new(
            "intents",
            "PHILOSOPHY_FLOOR_TWILIGHT_JUDGMENT.description");
        description.Add("Percent", MaxHpPercent);
        description.Add("DamageList", BuildDamageList(resolvedTargets));
        description.Add("DamageDetails", BuildDamageDetails(resolvedTargets));
        return description;
    }

    public override IReadOnlyList<IntentTargetLineTarget> GetIntentTargetLineTargets(
        Creature owner,
        IReadOnlyList<Creature>? fallbackTargets)
    {
        return ResolveTargets(
                owner,
                fallbackTargets ?? Array.Empty<Creature>())
            .Select(static target => new IntentTargetLineTarget(
                target,
                "TwilightJudgmentGroup"))
            .ToArray();
    }

    private static IReadOnlyList<Creature> ResolveTargets(
        Creature owner,
        IEnumerable<Creature> fallbackTargets)
    {
        IEnumerable<Creature> candidates = owner.CombatState?.PlayerCreatures
            ?? fallbackTargets;
        return CombatTargets.DeterministicLiving(
            candidates.Where(static target => target.IsPlayer),
            owner);
    }

    private static string BuildDamageList(
        IReadOnlyList<Creature> targets) => targets.Count == 0
        ? "0"
        : string.Join("/", targets.Select(CalculateDamage));

    private static string BuildDamageDetails(
        IReadOnlyList<Creature> targets) => targets.Count == 0
        ? "-"
        : string.Join(", ", targets.Select(target =>
            $"{target.Name}: {CalculateDamage(target)}"));

    internal static int CalculateDamage(Creature? target) =>
        target == null
            ? 0
            : Math.Max(
                1,
                (int)Math.Floor(
                    target.MaxHp * MaxHpPercent / 100m));
}
