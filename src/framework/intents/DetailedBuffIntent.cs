using System;
using System.Linq;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace LibraryOfRuina.framework.intents;

public sealed class DetailedBuffIntent<TPower> : BuffIntent, IDetailedIntentVisuals
    where TPower : PowerModel
{
    private readonly IntentBadge _badge;
    private readonly DetailedBuffTargetScope _scope;
    private readonly Func<Creature, IReadOnlyList<Creature>>? _targetResolver;
    private readonly string? _descriptionKey;
    private readonly Func<int> _amountFactory;

    public DetailedBuffIntent(
        int amount,
        DetailedBuffTargetScope scope = DetailedBuffTargetScope.Self,
        Func<Creature, IReadOnlyList<Creature>>? targetResolver = null,
        string? descriptionKey = null)
        : this(() => amount, scope, targetResolver, descriptionKey)
    {
    }

    public DetailedBuffIntent(
        Func<int> amountFactory,
        DetailedBuffTargetScope scope = DetailedBuffTargetScope.Self,
        Func<Creature, IReadOnlyList<Creature>>? targetResolver = null,
        string? descriptionKey = null)
    {
        _amountFactory = amountFactory ?? throw new ArgumentNullException(nameof(amountFactory));
        _badge = IntentBadge.FromPower<TPower>(_amountFactory);
        _scope = scope;
        _targetResolver = targetResolver;
        _descriptionKey = descriptionKey;
    }

    public int Amount => _amountFactory();

    public override IEnumerable<string> AssetPaths =>
        base.AssetPaths.Concat(_badge.AssetPaths);

    public DetailedIntentVisualState GetDetailedIntentVisuals(IEnumerable<Creature> targets, Creature owner)
    {
        IReadOnlyList<Creature> resolvedTargets = ResolveTargets(owner);
        string? scopeText = GetScopeText(_scope, resolvedTargets, owner);

        return new DetailedIntentVisualState(
            new[]
            {
                DetailedIntentVisualEffect.FromBadge(_badge, scopeText)
            });
    }

    protected override LocString GetIntentDescription(IEnumerable<Creature> targets, Creature owner)
    {
        if (_descriptionKey == null)
        {
            return base.GetIntentDescription(targets, owner);
        }

        IReadOnlyList<Creature> resolvedTargets = ResolveTargets(owner);

        LocString desc = BadgedIntentDescription.Create(_descriptionKey, owner, "BUFF");
        desc.Add("Amount", Amount);
        desc.Add("TargetName", GetTargetNameForDescription(_scope, resolvedTargets, owner));
        desc.Add("PowerId", _badge.Power.Id.Entry);
        BadgedIntentDescription.AddBadgeVariables(desc, _badge);
        return desc;
    }

    private IReadOnlyList<Creature> ResolveTargets(Creature owner)
    {
        if (_targetResolver != null)
        {
            return _targetResolver(owner)
                .Where(t => t is { IsAlive: true })
                .Distinct()
                .ToArray();
        }

        switch (_scope)
        {
            case DetailedBuffTargetScope.Self:
                return new[] { owner };
            case DetailedBuffTargetScope.AllEnemies:
                return owner.CombatState?.Enemies
                    .Where(e => e.IsAlive)
                    .ToArray() ?? Array.Empty<Creature>();
            case DetailedBuffTargetScope.OtherEnemies:
                return owner.CombatState?.Enemies
                    .Where(e => e.IsAlive && e != owner)
                    .ToArray() ?? Array.Empty<Creature>();
            default:
                return Array.Empty<Creature>();
        }
    }

    private static string? GetScopeText(
        DetailedBuffTargetScope scope,
        IReadOnlyList<Creature> resolvedTargets,
        Creature owner)
    {
        switch (scope)
        {
            case DetailedBuffTargetScope.Self:
                return null;
            case DetailedBuffTargetScope.AllEnemies:
                return DetailedIntentScopeText.AllEnemies;
            case DetailedBuffTargetScope.OtherEnemies:
                return DetailedIntentScopeText.OtherEnemies;
            case DetailedBuffTargetScope.RandomEnemy:
                return DetailedIntentScopeText.RandomEnemy;
        }

        if (resolvedTargets.Count == 1)
        {
            return resolvedTargets[0] == owner
                ? null
                : $"->{resolvedTargets[0].Name}";
        }

        if (resolvedTargets.Count > 0 && resolvedTargets.All(t => t.IsMonster))
        {
            return resolvedTargets.Contains(owner)
                ? DetailedIntentScopeText.AllEnemies
                : DetailedIntentScopeText.OtherEnemies;
        }

        return DetailedIntentScopeText.Target;
    }

    private static string GetTargetNameForDescription(
        DetailedBuffTargetScope scope,
        IReadOnlyList<Creature> resolvedTargets,
        Creature owner)
    {
        if (resolvedTargets.Count == 1)
        {
            return resolvedTargets[0].Name;
        }

        switch (scope)
        {
            case DetailedBuffTargetScope.RandomEnemy:
                return "随机敌人";
            case DetailedBuffTargetScope.AllEnemies:
                return "所有敌人";
            case DetailedBuffTargetScope.OtherEnemies:
                return "其他敌人";
            case DetailedBuffTargetScope.Self:
                return owner.Name;
            default:
                return resolvedTargets.Count > 0
                    ? resolvedTargets[0].Name
                    : owner.Name;
        }
    }
}
