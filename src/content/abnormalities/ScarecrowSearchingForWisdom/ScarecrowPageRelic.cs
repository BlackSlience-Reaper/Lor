using LibraryLib.Models;
using System.Linq;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.relics;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.abnormalities.ScarecrowSearchingForWisdom;

public sealed class ScarecrowPageRelic : ModalPageRelic<ScarecrowPageMode>, ILibraryAbstractModel
{
    internal const int RakeCardsToCopy = 2;
    //internal const int RakeCopiedCardCostIncrease = 1;
    internal const int HarvestDamageBonus = 10;
    internal const int HarvestChaoThresholdPercent = 50;
    internal const int TornWisdomCardsPerTrigger = 8;
    internal const int TornWisdomEnergyGain = 2;
    private const string RakeSelectionPromptLocKey = "SCARECROW_PAGE_RELIC.rakeSelectionScreenPrompt";
    private const string TornWisdomSelectionPromptLocKey = "SCARECROW_PAGE_RELIC.tornWisdomSelectionScreenPrompt";

    protected override string IconBaseName => "scarecrow_page_relic";
    

    public override RelicRarity Rarity => RelicRarity.Event;

    public override bool ShowCounter => Mode == ScarecrowPageMode.TornWisdom;

    public override int DisplayAmount =>
        Mode == ScarecrowPageMode.TornWisdom
            ? TornWisdomPlayedCardCounter
            : 0;

    public override bool IsAllowed(IRunState runState) =>
        AbnormalityPageRewardHelper.CanPageRelicAppear<ScarecrowPageRelic>(runState);

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<ScarecrowWisdomPower>(),
        ..HoverTipFactory.FromCardWithCardHoverTips<ScarecrowWisdomStatusCard>(),
        HoverTipFactory.ForEnergy(this)
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Mode", (int)ScarecrowPageMode.None),
        new DynamicVar("Cards", RakeCardsToCopy),
        //new DynamicVar("CostIncrease", RakeCopiedCardCostIncrease),
        new DamageVar(HarvestDamageBonus, ValueProp.Unpowered),
        new DynamicVar("ChaoThresholdPercent", HarvestChaoThresholdPercent),
        new DynamicVar("PlayedCards", TornWisdomCardsPerTrigger),
        new EnergyVar(TornWisdomEnergyGain)
    ];

    [SavedProperty]
    public ScarecrowPageMode Mode { get; private set; }

    protected override ScarecrowPageMode SelectedMode
    {
        get => Mode;
        set => Mode = value;
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int TornWisdomPlayedCardCounter { get; private set; }

    protected override async Task ApplyObtainedChoiceAsync(ScarecrowPageMode mode)
    {
        SetMode(mode);
        if (Mode == ScarecrowPageMode.Rake)
        {
            await CopyDeckCardsOnPickup();
        }
    }

    public override Task BeforeCombatStart()
    {
        EnsureValidModeOrFallback(nameof(BeforeCombatStart));
        UpdateModeUiState();
        InvokeDisplayAmountChanged();
        return Task.CompletedTask;
    }

#if STS2_0_111_0
    public override decimal ModifyDamageAdditive(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource, CardPlay? cardPlay)
#else
    public override decimal ModifyDamageAdditive(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
#endif
    {
        if (!IsHarvestBonusActive(target, props, dealer))
        {
            return 0m;
        }

        return HarvestDamageBonus;
    }

    public decimal ModifyChaoDamageAdditive(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay,
        LibraryDamageType type)
    {
        return IsHarvestBonusActive(target, props, dealer)
            ? HarvestDamageBonus
            : 0m;
    }

    private bool IsHarvestBonusActive(Creature? target, ValueProp props, Creature? dealer)
    {
        return Mode == ScarecrowPageMode.Harvest
            && target is LibraryCreature
            {
                HasChaoResistance: true,
                MaxChaoValue: > 0
            } libraryTarget
            && dealer == Owner.Creature
            && target.Side != Owner.Creature.Side
            && !AllyTurnRegistry.IsFriendlyAlly(target)
            && ValuePropCompat.IsPoweredAttack(props)
            && libraryTarget.CurrentChaoValue * 100m
                > libraryTarget.MaxChaoValue * HarvestChaoThresholdPercent;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        if (Mode != ScarecrowPageMode.TornWisdom || cardPlay.Card.Owner != Owner)
        {
            return;
        }

        TornWisdomPlayedCardCounter++;
        if (TornWisdomPlayedCardCounter < TornWisdomCardsPerTrigger)
        {
            UpdateModeUiState();
            InvokeDisplayAmountChanged();
            return;
        }

        TornWisdomPlayedCardCounter = 0;
        CardModel? selected = (await CardSelectCmd.FromHandForDiscard(
            context,
            Owner,
            new CardSelectorPrefs(new LocString("relics", TornWisdomSelectionPromptLocKey), 1),
            null,
            this)).FirstOrDefault();

        if (selected != null)
        {
            Flash();
            await CardCmd.Discard(context, selected);
            await PlayerCmd.GainEnergy(DynamicVars.Energy.IntValue, Owner);
        }

        UpdateModeUiState();
        InvokeDisplayAmountChanged();
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        UpdateModeUiState();
        InvokeDisplayAmountChanged();
        return Task.CompletedTask;
    }

    protected override IReadOnlyList<CardModel> CreateModeChoiceCards()
    {
        return
        [
            Owner.RunState.CreateCard<ScarecrowRakeChoiceCard>(Owner),
            Owner.RunState.CreateCard<ScarecrowHarvestChoiceCard>(Owner),
            Owner.RunState.CreateCard<ScarecrowTornWisdomChoiceCard>(Owner)
        ];
    }

    [AbnormalityPagePostObtainEffect((int)ScarecrowPageMode.Rake)]
    private async Task CopyDeckCardsOnPickup()
    {
        var selectedCards = (await CardSelectCmd.FromDeckGeneric(
                Owner,
                new CardSelectorPrefs(new LocString("relics", RakeSelectionPromptLocKey), RakeCardsToCopy),
                card => card.Type != CardType.Quest))
            .Take(RakeCardsToCopy)
            .ToList();

        foreach (var selectedCard in selectedCards)
        {
            var copy = Owner.RunState.CloneCard(selectedCard);
            // if (!copy.EnergyCost.CostsX && copy.EnergyCost.GetWithModifiers(CostModifiers.None) >= 0)
            // {
            //     copy.EnergyCost.SetCustomBaseCost(copy.EnergyCost.GetWithModifiers(CostModifiers.None) + RakeCopiedCardCostIncrease);
            // }
            CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(copy, PileType.Deck));
        }
    }

    protected override void ResetStateOnFallback() => TornWisdomPlayedCardCounter = 0;

    protected override void UpdateModeUiState()
    {
        DynamicVars["Mode"].BaseValue = (int)Mode;
        Status = Mode == ScarecrowPageMode.TornWisdom && CombatManager.Instance.IsInProgress
            ? RelicStatus.Active
            : RelicStatus.Normal;
    }
}
