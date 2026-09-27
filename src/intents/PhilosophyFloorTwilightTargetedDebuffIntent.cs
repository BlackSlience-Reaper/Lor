using System;
using System.Linq;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace LibraryOfRuina.intents;

/// <summary>
/// A vanilla debuff intent whose target is resolved from Twilight's saved
/// action plan instead of the move state's shared target list.
/// </summary>
public sealed class PhilosophyFloorTwilightTargetedDebuffIntent :
    DebuffIntent,
    IIntentEffectProvider,
    ITargetedIntentIndicator,
    IIntentTargetLineProvider
{
    private readonly string _descriptionKey;
    private readonly Func<Creature, IReadOnlyList<Creature>> _targetResolver;
    private readonly IReadOnlyDictionary<string, decimal> _descriptionVars;

    public PhilosophyFloorTwilightTargetedDebuffIntent(
        string descriptionKey,
        Func<Creature, IReadOnlyList<Creature>> targetResolver,
        params IntentBadge[] effects)
        : this(descriptionKey, targetResolver, null, effects)
    {
    }

    public PhilosophyFloorTwilightTargetedDebuffIntent(
        string descriptionKey,
        Func<Creature, IReadOnlyList<Creature>> targetResolver,
        IReadOnlyDictionary<string, decimal>? descriptionVars,
        params IntentBadge[] effects)
    {
        _descriptionKey = string.IsNullOrWhiteSpace(descriptionKey)
            ? throw new ArgumentException(
                "A description localization key is required.",
                nameof(descriptionKey))
            : descriptionKey;
        _targetResolver = targetResolver
            ?? throw new ArgumentNullException(nameof(targetResolver));
        _descriptionVars = descriptionVars
            ?? new Dictionary<string, decimal>(StringComparer.Ordinal);
        Effects = IntentEffectCollection.Create(effects);
    }

    public IReadOnlyList<IntentBadge> Effects { get; }

    public override IEnumerable<string> AssetPaths =>
        base.AssetPaths.Concat(Effects.SelectMany(static effect =>
            effect.AssetPaths));

    protected override LocString GetIntentDescription(
        IEnumerable<Creature> targets,
        Creature owner)
    {
        IReadOnlyList<Creature> resolvedTargets = ResolveTargets(
            targets,
            owner);
        LocString description = new("intents", _descriptionKey);
        description.Add(
            "TargetName",
            resolvedTargets.FirstOrDefault()?.Name ?? "Unknown Target");
        description.Add(
            "TargetNames",
            resolvedTargets.Count == 0
                ? "Unknown Target"
                : string.Join(", ", resolvedTargets.Select(static target =>
                    target.Name)));
        foreach ((string key, decimal value) in _descriptionVars)
        {
            description.Add(key, value);
        }
        BadgedIntentDescription.AddBadgeVariables(description, Effects);

        return description;
    }

    public IReadOnlyList<IntentTargetLineTarget> GetIntentTargetLineTargets(
        Creature owner,
        IReadOnlyList<Creature>? fallbackTargets)
    {
        return ResolveTargets(
                fallbackTargets ?? Array.Empty<Creature>(),
                owner)
            .Select(static target => new IntentTargetLineTarget(
                target,
                "TwilightDebuff"))
            .ToArray();
    }

    private IReadOnlyList<Creature> ResolveTargets(
        IEnumerable<Creature> targets,
        Creature owner)
    {
        Creature[] planned = _targetResolver(owner)
            .Where(static target => target.IsAlive)
            .Distinct()
            .ToArray();
        return planned.Length > 0
            ? planned
            : TargetedMonsterAttackHelper.GetTargetList(
                owner,
                targets as IReadOnlyList<Creature> ?? targets.ToArray());
    }
}

public sealed class PhilosophyFloorTwilightPunishmentIntent :
    CombinedAttackIntentBase
{
    public PhilosophyFloorTwilightPunishmentIntent(
        Func<int> damageCalc,
        Func<int> repeatCalc,
        string descriptionKey,
        int healPercent)
        : base(
            () => damageCalc(),
            repeatCalc,
            descriptionKey,
            [IntentBadge.Heal()])
    {
        HealPercent = healPercent;
    }

    public int HealPercent { get; }

    protected override string AttackKind =>
        CombinedIntentAnimData.AttackBuff;

    protected override string IntentPrefix => "COMBINED_ATTACK_BUFF";

    protected override void AddDescriptionVariables(
        LocString desc,
        IEnumerable<Creature> targets,
        Creature owner) => desc.Add("HealPercent", HealPercent);
}
