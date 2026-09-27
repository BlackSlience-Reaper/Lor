using System;
using System.Linq;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.intents;

internal interface ITargetedMonsterAttackProvider
{
    bool UsesTargetedAttackContract(Creature owner);

    IReadOnlyList<Creature> GetTargetedAttackTargets(Creature owner);

    string GetTargetedAttackTargetName(Creature owner);
}

public interface ITargetedIntentIndicator
{
}

public interface IUsesVanillaPlayerTargetIntentVisual
{
    bool UsesVanillaPlayerTargetIntentVisual { get; }
}

public readonly record struct IntentTargetLineTarget(Creature Target, string? DebugLabel = null);

public interface IIntentTargetLineProvider
{
    IReadOnlyList<IntentTargetLineTarget> GetIntentTargetLineTargets(
        Creature owner,
        IReadOnlyList<Creature>? fallbackTargets);
}

internal static class TargetedMonsterAttackHelper
{
    private static readonly Dictionary<Creature, Stack<IReadOnlyList<Creature>>> ForcedTargetStacks = [];

    public static bool TryGetProvider(Creature? owner, out ITargetedMonsterAttackProvider? provider)
    {
        provider = owner?.Monster as ITargetedMonsterAttackProvider;
        return provider != null;
    }

    public static IDisposable ForceTargets(Creature? owner, IEnumerable<Creature>? targets)
    {
        if (owner == null)
        {
            return EmptyTargetScope.Instance;
        }

        IReadOnlyList<Creature> targetList = targets?
            .Where(static target => target is { IsAlive: true })
            .Distinct()
            .ToArray()
            ?? Array.Empty<Creature>();

        if (!ForcedTargetStacks.TryGetValue(owner, out Stack<IReadOnlyList<Creature>>? stack))
        {
            stack = new Stack<IReadOnlyList<Creature>>();
            ForcedTargetStacks[owner] = stack;
        }

        stack.Push(targetList);
        return new ForcedTargetScope(owner, stack);
    }

    public static bool HasForcedTargets(Creature? owner)
    {
        return owner != null
            && ForcedTargetStacks.TryGetValue(owner, out Stack<IReadOnlyList<Creature>>? stack)
            && stack.Count > 0;
    }

    public static IReadOnlyList<Creature> GetTargetList(MonsterModel attacker, IReadOnlyList<Creature>? fallbackTargets = null)
    {
        return GetTargetList(attacker.Creature, fallbackTargets);
    }

    public static IReadOnlyList<Creature> GetTargetList(Creature? owner, IReadOnlyList<Creature>? fallbackTargets = null)
    {
        if (owner != null
            && ForcedTargetStacks.TryGetValue(owner, out Stack<IReadOnlyList<Creature>>? forcedStack)
            && forcedStack.Count > 0)
        {
            return forcedStack.Peek();
        }

        if (owner != null
            && TryGetProvider(owner, out ITargetedMonsterAttackProvider? provider)
            && provider != null)
        {
            if (!provider.UsesTargetedAttackContract(owner))
            {
                return GetFallbackTargets(owner, fallbackTargets);
            }

            IReadOnlyList<Creature> providerTargets = provider.GetTargetedAttackTargets(owner);
            return providerTargets;
        }

        return GetFallbackTargets(owner, fallbackTargets);
    }

    private static IReadOnlyList<Creature> GetFallbackTargets(Creature? owner, IReadOnlyList<Creature>? fallbackTargets)
    {
        if (fallbackTargets != null && fallbackTargets.Count > 0)
        {
            return fallbackTargets
                .Where(target => target != null && target.IsAlive)
                .ToArray();
        }

        if (owner?.CombatState == null)
        {
            return Array.Empty<Creature>();
        }

        return owner.CombatState
            .GetOpponentsOf(owner)
            .Where(static target => target.IsAlive)
            .ToArray();
    }

    public static Creature? GetPrimaryTarget(Creature? owner, IReadOnlyList<Creature>? fallbackTargets = null)
    {
        return GetTargetList(owner, fallbackTargets).FirstOrDefault();
    }

    public static Creature? GetPrimaryTarget(MonsterModel attacker, IReadOnlyList<Creature>? fallbackTargets = null)
    {
        return GetTargetList(attacker, fallbackTargets).FirstOrDefault();
    }

    public static string GetPrimaryTargetName(Creature? owner, IReadOnlyList<Creature>? fallbackTargets = null, string fallbackName = "Unknown Target")
    {
        if (owner != null
            && TryGetProvider(owner, out ITargetedMonsterAttackProvider? provider)
            && provider != null)
        {
            string name = provider.GetTargetedAttackTargetName(owner);
            if (provider.UsesTargetedAttackContract(owner))
            {
                return name;
            }
        }

        return GetPrimaryTarget(owner, fallbackTargets)?.Name ?? fallbackName;
    }

    public static string GetPrimaryTargetName(MonsterModel attacker, IReadOnlyList<Creature>? fallbackTargets = null, string fallbackName = "Unknown Target")
    {
        return GetPrimaryTargetName(attacker.Creature, fallbackTargets, fallbackName);
    }

    private sealed class ForcedTargetScope : IDisposable
    {
        private readonly Creature _owner;
        private readonly Stack<IReadOnlyList<Creature>> _stack;
        private bool _disposed;

        public ForcedTargetScope(Creature owner, Stack<IReadOnlyList<Creature>> stack)
        {
            _owner = owner;
            _stack = stack;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_stack.Count > 0)
            {
                _stack.Pop();
            }

            if (_stack.Count == 0)
            {
                ForcedTargetStacks.Remove(_owner);
            }
        }
    }

    private sealed class EmptyTargetScope : IDisposable
    {
        public static readonly EmptyTargetScope Instance = new();

        public void Dispose()
        {
        }
    }
}
