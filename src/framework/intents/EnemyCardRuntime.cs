using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace LibraryOfRuina.framework.intents;

public sealed class EnemyCardRuntime
{
    private readonly List<EnemyCardSpec> _masterDeck = [];
    private readonly List<EnemyCardSpec> _currentPlan = [];
    private readonly Func<IReadOnlyList<EnemyCardSpec>>? _defaultPlanFactory;

    public EnemyCardRuntime(MonsterModel owner, int masterEnergy, Func<IReadOnlyList<EnemyCardSpec>>? defaultPlanFactory = null)
    {
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        MasterEnergy = Math.Max(0, masterEnergy);
        Energy = MasterEnergy;
        _defaultPlanFactory = defaultPlanFactory;
    }

    public MonsterModel Owner { get; }

    public EnemyCardPile DrawPile { get; } = new("Draw");

    public EnemyCardPile Hand { get; } = new("Hand");

    public EnemyCardPile DiscardPile { get; } = new("Discard");

    public EnemyCardPile ExhaustPile { get; } = new("Exhaust");

    public int MasterEnergy { get; private set; }

    public int Energy { get; private set; }

    public int CurrentPlanIndex { get; private set; }

    public IReadOnlyList<EnemyCardSpec> CurrentPlan => _currentPlan;

    public EnemyCardSpec? CardInUse { get; private set; }

    public event Action<EnemyCardRuntime>? Changed;

    public void InitializeDeck(IEnumerable<EnemyCardSpec> cards)
    {
        _masterDeck.Clear();
        _masterDeck.AddRange(cards ?? throw new ArgumentNullException(nameof(cards)));
        DrawPile.ReplaceWith(_masterDeck);
        Hand.Clear();
        DiscardPile.Clear();
        ExhaustPile.Clear();
        CurrentPlanIndex = 0;
        CardInUse = null;
        Refresh();
    }

    public void Prep()
    {
        Energy = MasterEnergy;
        CurrentPlanIndex = 0;
        CardInUse = null;
        Refresh();
    }

    public void Recharge()
    {
        Energy = MasterEnergy;
        Refresh();
    }

    public bool UseEnergy(int amount)
    {
        amount = Math.Max(0, amount);
        if (amount > Energy)
        {
            return false;
        }

        Energy -= amount;
        Refresh();
        return true;
    }

    public void SetMasterEnergy(int amount, bool recharge = true)
    {
        MasterEnergy = Math.Max(0, amount);
        if (recharge)
        {
            Energy = MasterEnergy;
        }
        else
        {
            Energy = Math.Min(Energy, MasterEnergy);
        }

        Refresh();
    }

    public void DrawToHand(int count)
    {
        for (int i = 0; i < count; i++)
        {
            EnemyCardSpec? card = DrawPile.DrawTop();
            if (card == null)
            {
                ReshuffleDiscardIntoDraw();
                card = DrawPile.DrawTop();
            }

            if (card == null)
            {
                break;
            }

            Hand.AddToBottom(card);
        }

        Refresh();
    }

    public void PlanFixedSequence(IEnumerable<EnemyCardSpec> specs, bool replaceHand = true)
    {
        _currentPlan.Clear();
        _currentPlan.AddRange(specs ?? throw new ArgumentNullException(nameof(specs)));
        CurrentPlanIndex = 0;
        CardInUse = null;

        if (replaceHand)
        {
            Hand.ReplaceWith(_currentPlan);
        }

        MasterEnergy = Math.Max(0, _currentPlan.Sum(static card => Math.Max(0, card.Cost)));
        Energy = MasterEnergy;
        Refresh();
    }

    public void RefreshDefaultPlan()
    {
        if (_defaultPlanFactory == null)
        {
            Refresh();
            return;
        }

        PlanFixedSequence(_defaultPlanFactory(), replaceHand: true);
    }

    public Task ExecutePlannedCards(IReadOnlyList<Creature> targets)
    {
        return PlayPlannedHandLikeDownfall(targets);
    }

    public async Task PlayPlannedHandLikeDownfall(IReadOnlyList<Creature> targets)
    {
        if (_currentPlan.Count == 0)
        {
            RefreshDefaultPlan();
        }

        for (int i = CurrentPlanIndex; i < _currentPlan.Count; i++)
        {
            EnemyCardSpec card = _currentPlan[i];
            CurrentPlanIndex = i;
            await UseCard(card, targets);
        }

        CurrentPlanIndex = _currentPlan.Count;
        DiscardHandAtEndOfTurn();
        Refresh();
    }

    public async Task UseCard(EnemyCardSpec card, IReadOnlyList<Creature> targets)
    {
        ArgumentNullException.ThrowIfNull(card);
        IReadOnlyList<Creature> targetList = targets ?? Array.Empty<Creature>();

        CardInUse = card;
        Hand.Remove(card);

        if (!UseEnergy(card.Cost))
        {
            Energy = 0;
            Refresh();
        }

        await card.Execute(this, targetList);
        MoveUsedCard(card);
        CardInUse = null;
        CurrentPlanIndex = Math.Min(CurrentPlanIndex + 1, _currentPlan.Count);
        Refresh();
        await Cmd.CustomScaledWait(0.05f, 0.12f);
    }

    public void DiscardHandAtEndOfTurn()
    {
        foreach (EnemyCardSpec card in Hand.Cards.ToArray())
        {
            Hand.Remove(card);
            DiscardPile.AddToBottom(card);
        }

        Refresh();
    }

    public void Refresh()
    {
        NotifyChangedSafely();
        if (GetRefreshableCreatureNode() is NCreature creatureNode)
        {
            TaskHelper.RunSafely(creatureNode.RefreshIntents());
        }
    }

    private NCreature? GetRefreshableCreatureNode()
    {
        Creature ownerCreature = Owner.Creature;
        if (ownerCreature.CombatState == null)
        {
            return null;
        }

        try
        {
            if (!CombatManager.Instance.IsInProgress)
            {
                return null;
            }
        }
        catch
        {
            return null;
        }

        NCreature? creatureNode = NCombatRoom.Instance?.GetCreatureNode(ownerCreature);
        if (creatureNode == null
            || !GodotObject.IsInstanceValid(creatureNode)
            || !creatureNode.IsInsideTree()
            || !creatureNode.IsNodeReady())
        {
            return null;
        }

        return creatureNode;
    }

    private void NotifyChangedSafely()
    {
        if (Changed == null)
        {
            return;
        }

        foreach (Action<EnemyCardRuntime> handler in Changed.GetInvocationList().Cast<Action<EnemyCardRuntime>>())
        {
            try
            {
                handler(this);
            }
            catch (Exception ex)
            {
                Log.Warn("[LibraryOfRuina.EnemyCards] Skipped enemy card runtime change handler: " + ex);
            }
        }
    }

    private void MoveUsedCard(EnemyCardSpec card)
    {
        switch (card.Destination)
        {
            case EnemyCardDestination.Exhaust:
                ExhaustPile.AddToBottom(card);
                break;
            case EnemyCardDestination.Retain:
                Hand.AddToBottom(card);
                break;
            case EnemyCardDestination.Discard:
            default:
                DiscardPile.AddToBottom(card);
                break;
        }
    }

    private void ReshuffleDiscardIntoDraw()
    {
        if (DiscardPile.Count == 0)
        {
            return;
        }

        DrawPile.AddRangeToBottom(DiscardPile.Cards);
        DiscardPile.Clear();
    }
}
