using System;
using System.Linq;
using Godot;
using LibraryOfRuina.framework.intents;
using LibraryOfRuina.powers.JudgementBird;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Intents;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace LibraryOfRuina.intents.JudgementBird;

internal static class JudgementBirdIntentTargets
{
    internal static IReadOnlyList<Creature> LivingPlayers(Creature owner) =>
        owner.CombatState?.PlayerCreatures
            .Where(static target => target.IsAlive)
            .OrderBy(static target => target.CombatId ?? uint.MaxValue)
            .ToArray()
        ?? [];

    internal static IReadOnlyList<IntentTargetLineTarget> PlayerLines(
        Creature owner,
        string label) =>
        LivingPlayers(owner)
            .Select(target => new IntentTargetLineTarget(target, label))
            .ToArray();
}

public sealed class JudgementBirdDefendDebuffIntent :
    DefendIntent,
    IIntentEffectProvider,
    ICombinedIntentVisual,
    ICombinedIntentHoverIcon,
    IIntentTargetLineProvider
{
    private readonly string _descriptionKey;

    public JudgementBirdDefendDebuffIntent(
        int blockAmount,
        string descriptionKey,
        params IntentBadge[] effects)
    {
        BlockAmount = blockAmount;
        _descriptionKey = descriptionKey;
        Effects = IntentEffectCollection.Create(effects);
    }

    public int BlockAmount { get; }

    public IReadOnlyList<IntentBadge> Effects { get; }

    protected override string IntentPrefix => "COMBINED_DEFEND_DEBUFF";

    public string HoverIconPath =>
        CombinedIntentAnimData.GetHoverIconPath(
            CombinedIntentAnimData.DefendDebuff);

    public string HoverIntentPrefix => IntentPrefix;

    public override IEnumerable<string> AssetPaths =>
        CombinedIntentAnimData.GetAssetPaths(
                CombinedIntentAnimData.DefendDebuff)
            .Append(HoverIconPath)
            .Concat(Effects.SelectMany(static effect => effect.AssetPaths));

    public string GetCombinedAnimation(
        IEnumerable<Creature> targets,
        Creature owner) =>
        CombinedIntentAnimData.GetAnimationKey(
            CombinedIntentAnimData.DefendDebuff);

    public override Texture2D GetTexture(
        IEnumerable<Creature> targets,
        Creature owner) =>
        PreloadManager.Cache.GetTexture2D(
            CombinedIntentAnimData.GetIconPath(
                CombinedIntentAnimData.DefendDebuff,
                tier: null));

    public override string GetAnimation(
        IEnumerable<Creature> targets,
        Creature owner) => IntentAnimData.buff;

    public IReadOnlyList<IntentTargetLineTarget>
        GetIntentTargetLineTargets(
            Creature owner,
            IReadOnlyList<Creature>? fallbackTargets) =>
        JudgementBirdIntentTargets.PlayerLines(
            owner,
            "JudgementBirdGazeOne");

    protected override LocString GetIntentDescription(
        IEnumerable<Creature> targets,
        Creature owner)
    {
        LocString description = new("intents", _descriptionKey);
        description.Add("BlockAmount", BlockAmount);
        BadgedIntentDescription.AddBadgeVariables(
            description,
            Effects);
        return description;
    }
}

public sealed class JudgementBirdHealDebuffIntent :
    HealIntent,
    IIntentEffectProvider,
    IIntentTargetLineProvider
{
    private readonly string _descriptionKey;

    public JudgementBirdHealDebuffIntent(
        int healPercent,
        string descriptionKey,
        params IntentBadge[] effects)
    {
        HealPercent = healPercent;
        _descriptionKey = descriptionKey;
        Effects = IntentEffectCollection.Create(effects);
    }

    public int HealPercent { get; }

    public IReadOnlyList<IntentBadge> Effects { get; }

    public override IEnumerable<string> AssetPaths =>
        base.AssetPaths.Concat(
            Effects.SelectMany(static effect => effect.AssetPaths));

    public IReadOnlyList<IntentTargetLineTarget>
        GetIntentTargetLineTargets(
            Creature owner,
            IReadOnlyList<Creature>? fallbackTargets) =>
        JudgementBirdIntentTargets.PlayerLines(
            owner,
            "JudgementBirdGazeThree");

    protected override LocString GetIntentDescription(
        IEnumerable<Creature> targets,
        Creature owner)
    {
        LocString description = new("intents", _descriptionKey);
        description.Add("HealPercent", HealPercent);
        BadgedIntentDescription.AddBadgeVariables(
            description,
            Effects);
        return description;
    }
}

public sealed class JudgementBirdJudgementIntent :
    UnknownIntent,
    IIntentEffectProvider,
    ITargetedIntentIndicator,
    IIntentTargetLineProvider
{
    private readonly Func<Creature, IReadOnlyList<Creature>>
        _targetResolver;

    public JudgementBirdJudgementIntent(
        Func<Creature, IReadOnlyList<Creature>> targetResolver)
    {
        _targetResolver = targetResolver;
        Effects = IntentEffectCollection.Create(
        [
            IntentBadge.FromPower<JudgementBirdSinPower>(downText: "×2")
        ]);
    }

    public IReadOnlyList<IntentBadge> Effects { get; }

    public override IEnumerable<string> AssetPaths =>
        base.AssetPaths.Concat(
            Effects.SelectMany(static effect => effect.AssetPaths));

    public IReadOnlyList<IntentTargetLineTarget>
        GetIntentTargetLineTargets(
            Creature owner,
            IReadOnlyList<Creature>? fallbackTargets) =>
        ResolveTargets(owner)
            .Select(static target => new IntentTargetLineTarget(
                target,
                "JudgementBirdJudgement"))
            .ToArray();

    protected override LocString GetIntentDescription(
        IEnumerable<Creature> targets,
        Creature owner)
    {
        IReadOnlyList<Creature> resolved = ResolveTargets(owner);
        LocString description = new(
            "intents",
            "JUDGEMENT_BIRD_JUDGEMENT.description");
        description.Add(
            "TargetNames",
            resolved.Count == 0
                ? "-"
                : string.Join(", ", resolved.Select(static target =>
                    target.Name)));
        BadgedIntentDescription.AddBadgeVariables(
            description,
            Effects);
        return description;
    }

    private IReadOnlyList<Creature> ResolveTargets(Creature owner) =>
        _targetResolver(owner)
            .Where(static target => target.IsAlive)
            .Distinct()
            .OrderBy(static target => target.CombatId ?? uint.MaxValue)
            .ToArray();
}
