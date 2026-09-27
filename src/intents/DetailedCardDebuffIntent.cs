using System;
using System.Linq;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace LibraryOfRuina.intents;

public sealed class DetailedCardDebuffIntent<TCard> : DebuffIntent, IDetailedIntentVisuals
    where TCard : CardModel
{
    private readonly Func<CardModel> _cardFactory;
    private readonly string? _scopeText;

    public DetailedCardDebuffIntent(
        Action<TCard>? configureCard = null,
        string? scopeText = null,
        bool strong = false)
        : base(strong)
    {
        _scopeText = scopeText;
        _cardFactory = () =>
        {
            TCard card = (TCard)ModelDb.Card<TCard>().ToMutable();
            configureCard?.Invoke(card);
            return card;
        };
    }

    public override IEnumerable<string> AssetPaths =>
        base.AssetPaths.Concat(ModelDb.Card<TCard>().AllPortraitPaths);

    public DetailedIntentVisualState GetDetailedIntentVisuals(IEnumerable<Creature> targets, Creature owner)
    {
        IReadOnlyList<Creature> targetList = targets
            .Where(static target => target is { IsAlive: true })
            .Distinct()
            .ToArray();

        Creature? singleTarget = targetList.Count == 1 ? targetList[0] : null;
        string? scopeText = DetailedIntentScopeText.Normalize(_scopeText ?? ResolveScopeText(targetList, owner));

        return new DetailedIntentVisualState(
            new[]
            {
                new DetailedIntentVisualEffect
                {
                    Placement = DetailedIntentEffectPlacement.FloatingCardAboveIntent,
                    Kind = DetailedIntentEffectKind.Debuff,
                    CardFactory = _cardFactory,
                    UseFloatingPreviewCard = true,
                    ScopeText = scopeText
                }
            },
            singleTarget);
    }

    private static string? ResolveScopeText(IReadOnlyList<Creature> targets, Creature owner)
    {
        if (targets.Count == 1)
        {
            return DetailedIntentScopeText.Target;
        }

        if (targets.Count > 0 && targets.All(static target => target.IsMonster))
        {
            return targets.Contains(owner)
                ? DetailedIntentScopeText.AllEnemies
                : DetailedIntentScopeText.OtherEnemies;
        }

        return null;
    }
}
