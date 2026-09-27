using System;
using System.Linq;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace LibraryOfRuina.intents;

public sealed class TargetedSegmentedAttackIntent : AttackIntent, IBadgedIntent, ITargetedIntentIndicator
{
    private readonly int _segmentIndex;
    private readonly string _descriptionKey;
    private readonly IReadOnlyList<string> _segmentDamageKeys;
    private readonly Func<Creature, IReadOnlyList<Creature>?, TargetedSegmentedAttackPreview> _previewFactory;

    public TargetedSegmentedAttackIntent(
        int segmentIndex,
        string descriptionKey,
        IReadOnlyList<string> segmentDamageKeys,
        Func<Creature, IReadOnlyList<Creature>?, TargetedSegmentedAttackPreview> previewFactory,
        params IntentBadge[] badges)
    {
        if (segmentIndex < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(segmentIndex), segmentIndex, "Segment index must be >= 1.");
        }

        if (segmentDamageKeys == null)
        {
            throw new ArgumentNullException(nameof(segmentDamageKeys));
        }

        if (segmentIndex > segmentDamageKeys.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(segmentIndex),
                segmentIndex,
                "Segment index must be within the provided damage-key list.");
        }

        _segmentIndex = segmentIndex;
        _descriptionKey = descriptionKey;
        _segmentDamageKeys = segmentDamageKeys;
        _previewFactory = previewFactory ?? throw new ArgumentNullException(nameof(previewFactory));
        Badges = badges == null || badges.Length == 0
            ? Array.Empty<IntentBadge>()
            : BadgedIntentBadgeList.Create(badges);
        DamageCalc = () => 0;
    }

    public IntentBadge Badge =>
        Badges.Count > 0
            ? Badges[0]
            : throw new InvalidOperationException("This segmented attack intent has no badges.");

    public IReadOnlyList<IntentBadge> Badges { get; }

    public override IEnumerable<string> AssetPaths => base.AssetPaths.Concat(Badges.SelectMany(badge => badge.AssetPaths));

    protected override string IntentPrefix =>
        Badges.Count == 0
            ? base.IntentPrefix
            : BadgedIntentTitle.GetPrefix(Badges, BadgedIntentMainIntent.Attack);

    public override string GetAnimation(IEnumerable<Creature> targets, Creature owner)
    {
        return BadgedIntentAnimation.GetAttackAnimation(GetTotalDamage(targets, owner));
    }

    public override int GetTotalDamage(IEnumerable<Creature> targets, Creature owner)
    {
        return GetPreview(owner, targets as IReadOnlyList<Creature>).GetSegmentDamage(_segmentIndex);
    }

    public override LocString GetIntentLabel(IEnumerable<Creature> targets, Creature owner)
    {
        LocString fmt = new("intents", "FORMAT_DAMAGE_SINGLE");
        fmt.Add("Damage", GetTotalDamage(targets, owner));
        return fmt;
    }

    protected override LocString GetIntentDescription(IEnumerable<Creature> targets, Creature owner)
    {
        TargetedSegmentedAttackPreview preview = GetPreview(owner, targets as IReadOnlyList<Creature>);

        LocString desc = new("intents", _descriptionKey);
        desc.Add("TargetName", preview.TargetName);

        for (int i = 0; i < _segmentDamageKeys.Count; i++)
        {
            desc.AddObj(_segmentDamageKeys[i], preview.GetSegmentDamage(i + 1));
        }

        foreach ((string key, object? value) in preview.ExtraDescriptionVariables)
        {
            if (value != null)
            {
                desc.AddObj(key, value);
            }
        }

        BadgedIntentDescription.AddBadgeVariables(desc, Badges);
        return desc;
    }

    private TargetedSegmentedAttackPreview GetPreview(Creature owner, IReadOnlyList<Creature>? fallbackTargets)
    {
        return _previewFactory(owner, fallbackTargets);
    }
}

public sealed class TargetedSegmentedAttackPreview
{
    private static readonly IReadOnlyDictionary<string, object?> EmptyVariables =
        new Dictionary<string, object?>();

    public TargetedSegmentedAttackPreview(
        IReadOnlyList<int> segmentDamages,
        string targetName,
        IReadOnlyDictionary<string, object?>? extraDescriptionVariables = null)
    {
        SegmentDamages = segmentDamages ?? throw new ArgumentNullException(nameof(segmentDamages));
        TargetName = targetName ?? throw new ArgumentNullException(nameof(targetName));
        ExtraDescriptionVariables = extraDescriptionVariables ?? EmptyVariables;
    }

    public IReadOnlyList<int> SegmentDamages { get; }

    public string TargetName { get; }

    public IReadOnlyDictionary<string, object?> ExtraDescriptionVariables { get; }

    public static TargetedSegmentedAttackPreview Empty(int segmentCount, string fallbackTargetName = "Unknown Target")
    {
        if (segmentCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(segmentCount), segmentCount, "Segment count must be >= 0.");
        }

        return new TargetedSegmentedAttackPreview(new int[segmentCount], fallbackTargetName);
    }

    public int GetSegmentDamage(int segmentIndex)
    {
        if (segmentIndex < 1 || segmentIndex > SegmentDamages.Count)
        {
            return 0;
        }

        return SegmentDamages[segmentIndex - 1];
    }
}
