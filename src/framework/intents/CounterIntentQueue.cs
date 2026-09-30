using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.framework.monsters;
using LibraryOfRuina.interop;
using LibraryOfRuina.patches;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.framework.intents;

public sealed class CounterIntentQueue(MoveState emptyMoveState)
{
    public const int MaxVisibleCounterIntents = 2;

    private readonly List<ICounterIntent> _entries = [];
    private readonly Dictionary<ulong, int> _consumedCountsByPlayerId = [];

    public MoveState EmptyMoveState { get; } = emptyMoveState ?? throw new ArgumentNullException(nameof(emptyMoveState));

    public int Count => _entries.Count;

    public bool IsEmpty => _entries.Count == 0;

    public IEnumerable<string> AssetPaths =>
        CounterIntentVisuals.AssetPaths.Concat(_entries.SelectMany(static entry => ((AbstractIntent)entry).AssetPaths));

    public void Enqueue(ICounterIntent entry)
    {
        _entries.Add(entry ?? throw new ArgumentNullException(nameof(entry)));
    }

    public void EnqueueFromMove(MoveState moveState)
    {
        ArgumentNullException.ThrowIfNull(moveState);

        foreach (ICounterIntent intent in moveState.Intents.OfType<ICounterIntent>())
        {
            int count = intent is ICounterIntentQueueMultiplicity multiplicity
                ? Math.Max(1, multiplicity.CounterQueueCount)
                : 1;
            for (int index = 0; index < count; index++)
            {
                Enqueue(intent);
            }
        }
    }

    public bool TryPeek(out ICounterIntent entry)
    {
        if (_entries.Count == 0)
        {
            entry = null!;
            return false;
        }

        entry = _entries[0];
        return true;
    }

    public bool Contains(ICounterIntent entry)
    {
        return _entries.Contains(entry);
    }

    public bool ShouldDisplay(ICounterIntent entry, Creature? viewer = null)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return GetVisibleEntries(viewer).Any(queuedEntry => ReferenceEquals(queuedEntry, entry));
    }

    public bool Clear()
    {
        var hadEntries = _entries.Count > 0;
        _entries.Clear();
        _consumedCountsByPlayerId.Clear();
        return hadEntries;
    }

    public IReadOnlyList<AbstractIntent> DisplayIntents =>
        GetDisplayIntents(null);

    public IReadOnlyList<AbstractIntent> GetDisplayIntents(Creature? viewer)
    {
        return GetVisibleEntries(viewer).Cast<AbstractIntent>().ToArray();
    }

    public async Task ActivateTurnStartEffects(PlayerChoiceContext ctx, Creature owner)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(owner);

        foreach (var entry in _entries.OfType<ICounterIntentTurnStartEffect>())
        {
            await entry.ActivateCounterIntent(ctx, owner);
        }
    }

    public async Task<bool> TryTriggerCurrent(PlayerChoiceContext ctx, Creature owner, Creature counterTarget)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(counterTarget);

        if (!TryGetCounterKey(counterTarget, out ulong playerId))
        {
            return false;
        }

        int consumed = GetConsumedCounterCount(playerId);
        if (consumed >= _entries.Count)
        {
            return false;
        }

        ICounterIntent entry = _entries[consumed];
        _consumedCountsByPlayerId[playerId] = consumed + 1;
        await entry.PerformCounterIntent(ctx, owner, counterTarget);
        CounterIntentVisualPatch.RefreshCounterIntentDisplay(owner);
        return true;
    }

    private IEnumerable<ICounterIntent> GetVisibleEntries(Creature? viewer)
    {
        int consumed = GetConsumedCounterCount(viewer);
        return _entries.Skip(consumed).Take(MaxVisibleCounterIntents);
    }

    private int GetConsumedCounterCount(Creature? viewer)
    {
        return TryGetCounterKey(viewer, out ulong playerId)
            ? GetConsumedCounterCount(playerId)
            : 0;
    }

    private int GetConsumedCounterCount(ulong playerId)
    {
        return Math.Min(
            _consumedCountsByPlayerId.TryGetValue(playerId, out int count) ? count : 0,
            _entries.Count);
    }

    private static bool TryGetCounterKey(Creature? creature, out ulong playerId)
    {
        if (creature?.Player != null)
        {
            playerId = creature.Player.NetId;
            return true;
        }

        if (creature?.PetOwner != null)
        {
            playerId = creature.PetOwner.NetId;
            return true;
        }

        playerId = 0UL;
        return false;
    }

    public static MoveState CreateEmptyMoveState(string stateId = "EMPTY_COUNTER_MOVE")
    {
        return new MoveState(stateId, _ => Task.CompletedTask);
    }
}

public interface ICounterIntentQueueOwner
{
    CounterIntentQueue CounterIntentQueue { get; }
}

internal interface ICounterIntentQueueMultiplicity
{
    int CounterQueueCount { get; }
}

public abstract class CounterIntentMonsterModel : LorMonsterModel, ICounterIntentQueueOwner
{
    private CounterIntentQueue _counterIntentQueue = CreateCounterIntentQueue();
    private MoveState? _counterIntentMoveState;
    private bool _counterIntentMoveShouldQueue;

    public CounterIntentQueue CounterIntentQueue => _counterIntentQueue;

    public override IEnumerable<string> AssetPaths => base.AssetPaths.Concat(_counterIntentQueue.AssetPaths).Distinct();

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _counterIntentQueue = CreateCounterIntentQueue();
        _counterIntentMoveState = null;
        _counterIntentMoveShouldQueue = false;
    }

    protected void RefreshCounterIntentDisplay()
    {
        CounterIntentVisualPatch.RefreshCounterIntentDisplay(Creature);
    }

    protected bool ClearCounterIntentQueueAndRefresh(bool forceRefresh = false)
    {
        bool cleared = _counterIntentQueue.Clear();
        _counterIntentMoveState = null;
        _counterIntentMoveShouldQueue = false;
        if (cleared || forceRefresh)
        {
            RefreshCounterIntentDisplay();
        }

        return cleared;
    }

    protected bool PrepareCounterIntentsFromCurrentMove(bool refresh = true)
    {
        return SyncCounterIntentsWithCurrentMove(refresh);
    }

    protected virtual bool ShouldQueueCounterIntentsForCurrentMove()
    {
        return Creature.CombatState?.CurrentSide == CombatSide.Player;
    }

    internal bool SyncCounterIntentsWithCurrentMove(bool refresh = true)
    {
        bool shouldQueue = !Creature.IsDead && ShouldQueueCounterIntentsForCurrentMove();
        if (ReferenceEquals(_counterIntentMoveState, NextMove)
            && _counterIntentMoveShouldQueue == shouldQueue)
        {
            return shouldQueue && !_counterIntentQueue.IsEmpty;
        }

        bool hadEntries = !_counterIntentQueue.IsEmpty;
        _counterIntentQueue.Clear();
        _counterIntentMoveState = NextMove;
        _counterIntentMoveShouldQueue = shouldQueue;

        if (!shouldQueue)
        {
            if (refresh && hadEntries)
            {
                RefreshCounterIntentDisplay();
            }

            return false;
        }

        _counterIntentQueue.EnqueueFromMove(NextMove);
        if (refresh)
        {
            RefreshCounterIntentDisplay();
        }

        return !_counterIntentQueue.IsEmpty;
    }

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (side == CombatSide.Player)
        {
            if (Creature.IsDead)
            {
                ClearCounterIntentQueueAndRefresh();
            }
            else if (SyncCounterIntentsWithCurrentMove())
            {
                await _counterIntentQueue.ActivateTurnStartEffects(choiceContext, Creature);
            }
        }
        else if (side == CombatSide.Enemy)
        {
            ClearCounterIntentQueueAndRefresh(forceRefresh: true);
        }

        await base.BeforeSideTurnStart(choiceContext, side, participants, combatState);
    }

    public override async Task BeforeDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        await base.BeforeDamageReceived(choiceContext, target, amount, props, dealer, cardSource);
        PrepareCounterIntentForIncomingDamage(target, amount, props, dealer, cardSource);
    }

    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        await base.AfterDamageReceived(choiceContext, target, result, props, dealer, cardSource);
        await TryTriggerCounterIntent(choiceContext, target, result, props, dealer);
    }

    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        LibraryDamageType type)
    {
        await base.AfterDamageReceived(choiceContext, target, result, props, dealer, cardSource, type);
    }

    private void PrepareCounterIntentForIncomingDamage(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (target != Creature
            || dealer == null
            || dealer.Side != CombatSide.Player
            || dealer.IsDead
            || amount <= 0m
            || !ValuePropCompat.IsPoweredAttack(props)
            || Creature.CombatState?.CurrentSide != CombatSide.Player)
        {
            return;
        }

        SyncCounterIntentsWithCurrentMove(refresh: false);
    }

    private async Task TryTriggerCounterIntent(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer)
    {
        if (target != Creature
            || dealer == null
            || dealer.Side != CombatSide.Player
            || dealer.IsDead
            || result.UnblockedDamage <= 0m
            || !ValuePropCompat.IsPoweredAttack(props)
            || Creature.CombatState?.CurrentSide != CombatSide.Player)
        {
            return;
        }

        if (SyncCounterIntentsWithCurrentMove(refresh: false))
        {
            await _counterIntentQueue.TryTriggerCurrent(choiceContext, Creature, dealer);
        }
    }

    private static CounterIntentQueue CreateCounterIntentQueue()
    {
        return new CounterIntentQueue(CounterIntentQueue.CreateEmptyMoveState());
    }
}
