using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryLib.Models;
using LibraryLib.Utils.Resistance;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.relics;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.liberation.Language;

public sealed class NothingTherePageRelic : ModalPageRelic<NothingTherePageMode>
{
    public const int GoodbyeDamageMultiplier = 2;

    private CardModel? _goodbyeActiveCard;
    private CardModel? _helloActiveCard;

    public override RelicRarity Rarity => RelicRarity.Event;

    public override string PackedIconPath => LanguageFloorAssets.LiberationEncounterRunHistoryIcon;

    protected override string PackedIconOutlinePath =>
        LanguageFloorAssets.LiberationEncounterOutlineRunHistoryIcon;

    protected override string BigIconPath => LanguageFloorAssets.LiberationEncounterRunHistoryIcon;

    public override bool IsAllowed(IRunState runState)
    {
        _ = runState;
        return false;
    }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Mode", (int)NothingTherePageMode.None),
        new DynamicVar("DamageMultiplier", GoodbyeDamageMultiplier)
    ];

    [SavedProperty]
    public NothingTherePageMode Mode { get; private set; }

    protected override NothingTherePageMode SelectedMode
    {
        get => Mode;
        set => Mode = value;
    }

    protected override bool RefreshIconOnModeChange => false;

    protected override bool RefreshUiBeforeModeChoice => true;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool HelloUsedThisTurn { get; private set; }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _goodbyeActiveCard = null;
        _helloActiveCard = null;
    }

    public override Task AfterRoomEntered(AbstractRoom room)
    {
        _ = room;
        EnsureValidModeOrFallback(nameof(AfterRoomEntered));
        ResetTransientCardState();
        UpdateModeUiState();
        return Task.CompletedTask;
    }

    public override Task BeforeCombatStart()
    {
        EnsureValidModeOrFallback(nameof(BeforeCombatStart));
        HelloUsedThisTurn = false;
        ResetTransientCardState();
        UpdateModeUiState();
        return Task.CompletedTask;
    }

    public override Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        _ = choiceContext;
        _ = combatState;
        if (side == Owner.Creature.Side
            && participants.Contains(Owner.Creature))
        {
            HelloUsedThisTurn = false;
            _helloActiveCard = null;
            UpdateModeUiState();
        }

        return Task.CompletedTask;
    }

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (Mode == NothingTherePageMode.Hello
            && !HelloUsedThisTurn
            && cardPlay.Player == Owner
            && cardPlay.Card.Type == CardType.Attack
            && cardPlay.IsFirstInSeries)
        {
            HelloUsedThisTurn = true;
            _helloActiveCard = cardPlay.Card;
            Flash();
            UpdateModeUiState();
        }

        return Task.CompletedTask;
    }

    public override Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource) =>
        HealFromHelloAttack(
            choiceContext,
            dealer,
            result,
            props,
            target,
            cardSource);

    public override Task AfterCardPlayed(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        _ = choiceContext;
        if (ReferenceEquals(_helloActiveCard, cardPlay.Card)
            && cardPlay.IsLastInSeries)
        {
            _helloActiveCard = null;
        }

        UpdateModeUiState();
        return Task.CompletedTask;
    }

    public override async Task BeforeSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (Mode != NothingTherePageMode.Goodbye
            || side != Owner.Creature.Side
            || !participants.Contains(Owner.Creature)
            || Owner.Creature.IsDead
            || Owner.Creature.CombatState is not { } combatState)
        {
            return;
        }

        CardModel? lastAttack = CombatManager.Instance.History
            .CardPlaysFinished
            .LastOrDefault(entry =>
                entry.CardPlay.Player == Owner
                && entry.CardPlay.Card.Type == CardType.Attack
                && entry.CardPlay.IsLastInSeries
                && !entry.CardPlay.Card.IsDupe
                && entry.HappenedThisTurn(combatState))
            ?.CardPlay.Card;
        if (lastAttack == null || combatState.HittableEnemies.Count == 0)
        {
            UpdateModeUiState();
            return;
        }

        CardModel copy = lastAttack.CreateDupe(Owner);
        _goodbyeActiveCard = copy;
        Flash();
        try
        {
            await CardCmd.AutoPlay(choiceContext, copy, null);
        }
        finally
        {
            _goodbyeActiveCard = null;
            if (!copy.HasBeenRemovedFromState)
            {
                copy.RemoveFromState();
            }

            UpdateModeUiState();
        }
    }

    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        _ = target;
        _ = amount;
        _ = cardPlay;
        return ShouldDoubleGoodbyeDamage(props, dealer, cardSource)
            ? GoodbyeDamageMultiplier
            : 1m;
    }

    public override decimal ModifyChaoDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay,
        LibraryDamageType type)
    {
        _ = target;
        _ = amount;
        _ = cardPlay;
        _ = type;
        return ShouldDoubleGoodbyeDamage(props, dealer, cardSource)
            ? GoodbyeDamageMultiplier
            : 1m;
    }

    public override decimal ModifyHpLostAfterOsty(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource) =>
        PreventShellHpLoss(target, amount, props, dealer, cardSource);

    public override Task AfterModifyingHpLostAfterOsty()
    {
        if (Mode == NothingTherePageMode.Shell)
        {
            Flash();
        }

        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _ = room;
        HelloUsedThisTurn = false;
        ResetTransientCardState();
        Status = RelicStatus.Normal;
        InvokeDisplayAmountChanged();
        return Task.CompletedTask;
    }

    private async Task HealFromHelloAttack(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        _ = choiceContext;
        if (Mode != NothingTherePageMode.Hello
            || !ReferenceEquals(_helloActiveCard, cardSource)
            || result.UnblockedDamage <= 0
            || target.Side == Owner.Creature.Side
            || AllyTurnRegistry.IsFriendlyAlly(target)
            || !IsOwnerAttackSource(dealer, cardSource, props)
            || Owner.Creature.IsDead)
        {
            return;
        }

        await CreatureCmd.Heal(Owner.Creature, result.UnblockedDamage);
    }

    private bool ShouldDoubleGoodbyeDamage(
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource) =>
        Mode == NothingTherePageMode.Goodbye
        && ReferenceEquals(_goodbyeActiveCard, cardSource)
        && IsOwnerAttackSource(dealer, cardSource, props);

    private bool IsOwnerAttackSource(
        Creature? dealer,
        CardModel? cardSource,
        ValueProp props) =>
        cardSource?.Owner == Owner
        && cardSource.Type == CardType.Attack
        && (dealer == Owner.Creature || dealer == Owner.Osty)
        && ValuePropCompat.IsPoweredAttack(props);

    private decimal PreventShellHpLoss(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource) =>
        Mode == NothingTherePageMode.Shell
        && target == Owner.Creature
        && amount > 0m
        && !ValuePropCompat.IsCardOrMonsterMove(props)
        && cardSource == null
        && (dealer == null || dealer == Owner.Creature)
            ? 0m
            : amount;

    protected override IReadOnlyList<CardModel> CreateModeChoiceCards() =>
    [
        Owner.RunState.CreateCard<NothingThereGoodbyeChoiceCard>(Owner),
        Owner.RunState.CreateCard<NothingThereHelloChoiceCard>(Owner),
        Owner.RunState.CreateCard<NothingThereShellChoiceCard>(Owner)
    ];

    private void ResetTransientCardState()
    {
        _goodbyeActiveCard = null;
        _helloActiveCard = null;
    }

    protected override void ResetStateOnModeSet(NothingTherePageMode mode)
    {
        HelloUsedThisTurn = false;
        ResetTransientCardState();
    }

    protected override void ResetStateOnFallback() => ResetStateOnModeSet(FallbackMode);

    protected override void UpdateModeUiState()
    {
        DynamicVars["Mode"].BaseValue = (int)Mode;
        Status = Mode switch
        {
            NothingTherePageMode.Goodbye
                when CombatManager.Instance.IsInProgress
                    && HasGoodbyeAttackThisTurn() => RelicStatus.Active,
            NothingTherePageMode.Hello
                when CombatManager.Instance.IsInProgress
                    && !HelloUsedThisTurn => RelicStatus.Active,
            NothingTherePageMode.Shell
                when CombatManager.Instance.IsInProgress => RelicStatus.Active,
            _ => RelicStatus.Normal
        };
        InvokeDisplayAmountChanged();
    }

    private bool HasGoodbyeAttackThisTurn()
    {
        if (Owner.Creature.CombatState is not { } combatState)
        {
            return false;
        }

        return CombatManager.Instance.History.CardPlaysFinished.Any(entry =>
            entry.CardPlay.Player == Owner
            && entry.CardPlay.Card.Type == CardType.Attack
            && !entry.CardPlay.Card.IsDupe
            && entry.HappenedThisTurn(combatState));
    }
}

public enum NothingTherePageMode
{
    None = 0,
    Goodbye = 1,
    Hello = 2,
    Shell = 3
}
