using System.Linq;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace LibraryOfRuina.intents.SpiderBud;

public sealed class SpiderBudWebDebuffIntent : DebuffIntent, IDetailedIntentVisuals
{
    public enum WebDebuffType
    {
        Bind,
        Flaw,
    }

    private readonly WebDebuffType _debuffType;

    
    public SpiderBudWebDebuffIntent()
        : this(WebDebuffType.Bind)
    {
    }

    public SpiderBudWebDebuffIntent(WebDebuffType debuffType)
        : base(strong: true)
    {
        _debuffType = debuffType;
    }

    public override IEnumerable<string> AssetPaths =>
        base.AssetPaths.Concat(CreateEffect().AssetPaths);

    public DetailedIntentVisualState GetDetailedIntentVisuals(IEnumerable<Creature> targets, Creature owner)
    {
        DetailedIntentVisualEffect effect = CreateEffect();

        return new DetailedIntentVisualState(
            new[]
            {
                effect,
            });
    }

    private DetailedIntentVisualEffect CreateEffect()
    {
        return _debuffType switch
        {
            WebDebuffType.Bind => DetailedIntentVisualEffect.FromBadge(IntentBadge.Bind(3)),
            WebDebuffType.Flaw => DetailedIntentVisualEffect.FromBadge(IntentBadge.Flaw(1, 2)),
            _ => DetailedIntentVisualEffect.FromBadge(IntentBadge.Bind(3)),
        };
    }
}
