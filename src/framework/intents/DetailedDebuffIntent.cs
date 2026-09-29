using System;
using System.Linq;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace LibraryOfRuina.framework.intents;

public sealed class DetailedDebuffIntent<TPower> :
    DebuffIntent,
    IDetailedIntentVisuals
    where TPower : PowerModel
{
    private readonly IntentBadge _badge;
    private readonly string? _scopeText;
    private readonly Func<int> _amountFactory;

    public DetailedDebuffIntent(
        int amount,
        string? scopeText = null,
        bool strong = false)
        : this(() => amount, scopeText, strong)
    {
    }

    public DetailedDebuffIntent(
        Func<int> amountFactory,
        string? scopeText = null,
        bool strong = false)
        : base(strong)
    {
        _amountFactory = amountFactory
            ?? throw new ArgumentNullException(nameof(amountFactory));
        _badge = IntentBadge.FromPower<TPower>(_amountFactory);
        _scopeText = scopeText;
    }

    public int Amount => _amountFactory();

    public override IEnumerable<string> AssetPaths =>
        base.AssetPaths.Concat(_badge.AssetPaths);

    public DetailedIntentVisualState GetDetailedIntentVisuals(
        IEnumerable<Creature> targets,
        Creature owner)
    {
        Creature[] livingTargets = targets
            .Where(static target => target.IsAlive)
            .Distinct()
            .ToArray();
        Creature? singleTarget = livingTargets.Length == 1
            ? livingTargets[0]
            : null;
        return new DetailedIntentVisualState(
            [DetailedIntentVisualEffect.FromBadge(_badge, _scopeText)],
            singleTarget);
    }
}
