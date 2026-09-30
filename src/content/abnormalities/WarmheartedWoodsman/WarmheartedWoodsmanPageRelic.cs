using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.abnormalities.WarmheartedWoodsman;

public sealed class WarmheartedWoodsmanPageRelic : ModalPageRelic<WarmheartedWoodsmanPageMode>
{
    public const int WarmHeartEnergyThreshold = 4;
    public const int WarmHeartStrongStacks = 3;
    public const int OneTurnDuration = 1;
    public const int HeartHpLossThreshold = 20;
    public const int EnergyReductionPerTurn = 3;
    
    public const int HeartEnergyReduction = 1;
    public const int LoggingHpGapPerTrigger = 20;
    public const int LoggingStrongPerGap = 2;
    public const int LoggingMaxStrong = 8;

    private HashSet<CardModel> _loggingTriggeredCards = [];

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _loggingTriggeredCards = [.. _loggingTriggeredCards];
    }

    private int LoggingStrongGivenThisCombat;

    protected override string IconBaseName => "warmhearted_woodsman_page_relic";

    public override RelicRarity Rarity => RelicRarity.Event;

    public override bool ShowCounter =>
        Mode == WarmheartedWoodsmanPageMode.Heart && CombatManager.Instance.IsInProgress;

    public override int DisplayAmount =>
        Mode == WarmheartedWoodsmanPageMode.Heart
            ? Math.Max(0, EnergyReductionPerTurn - EnergyReductionThisTurn)
            : 0;

    public override bool IsAllowed(IRunState runState) =>
        AbnormalityPageRewardHelper.CanPageRelicAppear<WarmheartedWoodsmanPageRelic>(runState);

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.ForEnergy(this),
        HoverTipFactory.FromPower<LibraryStrongPower>()
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Mode", (int)WarmheartedWoodsmanPageMode.None),
        new DynamicVar("EnergyThreshold", WarmHeartEnergyThreshold),
        new DynamicVar("Strong", WarmHeartStrongStacks),
        new DynamicVar("Turns", OneTurnDuration),
        new DynamicVar("HpLoss", HeartHpLossThreshold),
        new EnergyVar(HeartEnergyReduction),
        new DynamicVar("EnergyReductionPerTurn", EnergyReductionPerTurn),
        new DynamicVar("HpGap", LoggingHpGapPerTrigger),
        new DynamicVar("LoggingStrong", LoggingStrongPerGap),
        new DynamicVar("LoggingMaxStrong", LoggingMaxStrong),
        new DynamicVar("RemainingHpLoss", HeartHpLossThreshold)
    ];

    [SavedProperty]
    public WarmheartedWoodsmanPageMode Mode { get; private set; }

    protected override WarmheartedWoodsmanPageMode SelectedMode
    {
        get => Mode;
        set => Mode = value;
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int HeartHpLossCounter { get; private set; }

    public int EnergyReductionThisTurn { get; private set; }

    public override Task BeforeCombatStart()
    {
        EnsureValidModeOrFallback(nameof(BeforeCombatStart));
        HeartHpLossCounter = 0;
        EnergyReductionThisTurn = 0;
        _loggingTriggeredCards.Clear();
        LoggingStrongGivenThisCombat = 0;
        UpdateModeUiState();
        return Task.CompletedTask;
    }

    public override async Task AfterPlayerTurnStart(
        PlayerChoiceContext choiceContext,
        Player player)
    {
        if (Mode != WarmheartedWoodsmanPageMode.WarmHeart
            || player != Owner
            || Owner.Creature == null
            || !Owner.Creature.IsAlive
            || Owner.PlayerCombatState == null
            || Owner.PlayerCombatState.Energy < WarmHeartEnergyThreshold)
        {
            return;
        }

        Flash();
        await LibraryPowerCmd.Apply<LibraryStrongPower>(
            Owner.Creature,
            WarmHeartStrongStacks,
            turns: -1,
            Owner.Creature,
            null);
    }

    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        if (Owner?.Creature == null
            || !Owner.Creature.IsAlive
            || result.UnblockedDamage <= 0
            || target.Side == Owner.Creature.Side
            || AllyTurnRegistry.IsFriendlyAlly(target)
            || !IsOwnerDamageSource(dealer, cardSource))
        {
            return;
        }

        if (Mode == WarmheartedWoodsmanPageMode.Heart)
        {
            await ResolveHeartHpLoss(choiceContext, result.UnblockedDamage);
        }
        else if (Mode == WarmheartedWoodsmanPageMode.Logging)
        {
            await ResolveLoggingStrong(choiceContext, result, target, cardSource);
        }
    }

    public override Task AfterAttack(PlayerChoiceContext choiceContext, AttackCommand command)
    {
        _loggingTriggeredCards.Clear();
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        HeartHpLossCounter = 0;
        _loggingTriggeredCards.Clear();
        LoggingStrongGivenThisCombat = 0;
        UpdateModeUiState();
        return Task.CompletedTask;
    }

    private async Task ResolveHeartHpLoss(PlayerChoiceContext choiceContext, int hpLoss)
    {
        HeartHpLossCounter += hpLoss;
        while (HeartHpLossCounter >= HeartHpLossThreshold && EnergyReductionThisTurn < EnergyReductionPerTurn)
        {
            HeartHpLossCounter -= HeartHpLossThreshold;
            if (EnergyReductionThisTurn ++ < EnergyReductionPerTurn) ReduceRandomHighestCostHandCard();
            
        }

        UpdateModeUiState();
        await Task.CompletedTask;
    }

    private void ReduceRandomHighestCostHandCard()
    {
        if (Owner?.Creature == null)
        {
            return;
        }

        List<CardModel> candidates = PileType.Hand.GetPile(Owner).Cards
            .Where(static card => !card.EnergyCost.CostsX)
            .Where(static card => card.EnergyCost.GetWithModifiers(CostModifiers.All) > 0)
            .ToList();
        if (candidates.Count == 0)
        {
            return;
        }

        int maxCost = candidates.Max(static card => card.EnergyCost.GetWithModifiers(CostModifiers.All));
        List<CardModel> highest = candidates
            .Where(card => card.EnergyCost.GetWithModifiers(CostModifiers.All) == maxCost)
            .ToList();
        CardModel? selected = Owner.RunState.Rng.CombatCardSelection.NextItem(highest);
        if (selected == null)
        {
            return;
        }

        Flash();
        selected.EnergyCost.AddThisCombat(-HeartEnergyReduction, reduceOnly: true);
        selected.InvokeEnergyCostChanged();
    }

    private async Task ResolveLoggingStrong(
        PlayerChoiceContext choiceContext,
        DamageResult result,
        Creature target,
        CardModel? cardSource)
    {
        if (cardSource != null && !_loggingTriggeredCards.Add(cardSource))
        {
            return;
        }

        int targetHpBeforeHit = target.CurrentHp + result.UnblockedDamage;
        int hpGap = targetHpBeforeHit - Owner.Creature.CurrentHp;
        if (hpGap < LoggingHpGapPerTrigger)
        {
            return;
        }

        int remaining = LoggingMaxStrong - LoggingStrongGivenThisCombat;
        int strong = Math.Min(remaining, hpGap / LoggingHpGapPerTrigger * LoggingStrongPerGap);
        if (strong <= 0)
        {
            return;
        }

        LoggingStrongGivenThisCombat += strong;
        Flash();
        await LibraryPowerCmd.Apply<LibraryStrongPower>(
            new ThrowingPlayerChoiceContext(),
            Owner.Creature,
            strong,
            0,
            IsPermanent: false,
            Owner.Creature,
            cardSource);
    }

    protected override IReadOnlyList<CardModel> CreateModeChoiceCards()
    {
        return
        [
            Owner.RunState.CreateCard<WarmheartedWoodsmanWarmHeartChoiceCard>(Owner),
            Owner.RunState.CreateCard<WarmheartedWoodsmanHeartChoiceCard>(Owner),
            Owner.RunState.CreateCard<WarmheartedWoodsmanLoggingChoiceCard>(Owner)
        ];
    }

    private bool IsOwnerDamageSource(Creature? dealer, CardModel? cardSource)
    {
        if (dealer == null)
        {
            return false;
        }

        if (dealer != Owner.Creature && dealer != Owner.Osty)
        {
            return false;
        }

        return cardSource == null || cardSource.Owner == Owner;
    }

    protected override void ResetStateOnFallback()
    {
        HeartHpLossCounter = 0;
        LoggingStrongGivenThisCombat = 0;
    }

    protected override void UpdateModeUiState()
    {
        DynamicVars["Mode"].BaseValue = (int)Mode;
        DynamicVars["RemainingHpLoss"].BaseValue = Math.Max(0, HeartHpLossThreshold - HeartHpLossCounter);
        Status = Mode switch
        {
            WarmheartedWoodsmanPageMode.Heart when CombatManager.Instance.IsInProgress => RelicStatus.Active,
            WarmheartedWoodsmanPageMode.Logging when CombatManager.Instance.IsInProgress => RelicStatus.Active,
            WarmheartedWoodsmanPageMode.WarmHeart when CombatManager.Instance.IsInProgress => RelicStatus.Active,
            _ => RelicStatus.Normal
        };
        InvokeDisplayAmountChanged();
    }
}

public enum WarmheartedWoodsmanPageMode
{
    None = 0,
    WarmHeart = 1,
    Heart = 2,
    Logging = 3
}
