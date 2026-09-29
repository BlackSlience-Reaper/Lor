using System;
using System.Linq;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace LibraryOfRuina.framework.intents;

public sealed class DetailedBuffGroupIntent : BuffIntent, IDetailedIntentVisuals
{
    private readonly IReadOnlyList<DetailedIntentVisualEffect> _effects;

    public DetailedBuffGroupIntent(params DetailedIntentVisualEffect[] effects)
    {
        _effects = effects?.Where(static effect => effect != null).ToArray()
            ?? throw new ArgumentNullException(nameof(effects));
    }

    public override IEnumerable<string> AssetPaths =>
        base.AssetPaths.Concat(_effects.SelectMany(static effect => effect.AssetPaths));

    public DetailedIntentVisualState GetDetailedIntentVisuals(IEnumerable<Creature> targets, Creature owner)
    {
        return new DetailedIntentVisualState(_effects);
    }
}
