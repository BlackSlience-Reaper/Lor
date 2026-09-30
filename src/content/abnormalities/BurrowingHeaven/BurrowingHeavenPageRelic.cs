using System;
using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.relics;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.abnormalities.BurrowingHeaven;

public sealed class BurrowingHeavenPageRelic : ModalPageRelic<BurrowingHeavenPageMode>
{
    internal const int WitheringBloodWingsReflectPercent = 100;
    internal const int OthersGazeHandCards = 1;
    internal const int OthersGazeDraw = 4;
    internal const int OthersGazeEnergyRemaining = 1;
    internal const int OthersGazeEnergyNextTurn = 3;
    internal const int AttentionAndFocusDamageTakenPercent = 10;
    internal const int AttentionAndFocusDamageDealtPercent = 40;
    private const decimal AttentionAndFocusDamageTakenMultiplier = 1.10m;
    private const decimal AttentionAndFocusDamageDealtMultiplier = 1.40m;

    protected override string IconBaseName => "burrowing_heaven_page_relic";
    

    public override RelicRarity Rarity => RelicRarity.Event;

    public override bool IsAllowed(IRunState runState) =>
        AbnormalityPageRewardHelper.CanPageRelicAppear<BurrowingHeavenPageRelic>(runState);

    protected override IEnumerable<IHoverTip> ExtraHoverTips => Mode switch
    {
        BurrowingHeavenPageMode.OthersGaze =>
        [
            HoverTipFactory.FromPower<DrawCardsNextTurnPower>(),
            HoverTipFactory.FromPower<EnergyNextTurnPower>(),
            HoverTipFactory.ForEnergy(this)
        ],
        _ => []
    };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Mode", (int)BurrowingHeavenPageMode.None),
        new DynamicVar("ReflectPercent", WitheringBloodWingsReflectPercent),
        new DynamicVar("HandCards", OthersGazeHandCards),
        new CardsVar("Draw", OthersGazeDraw),
        new EnergyVar("EnergyRemaining", OthersGazeEnergyRemaining),
        new EnergyVar("EnergyNextTurn", OthersGazeEnergyNextTurn),
        new DynamicVar("DamageTakenPercent", AttentionAndFocusDamageTakenPercent),
        new DynamicVar("DamageDealtPercent", AttentionAndFocusDamageDealtPercent)
    ];

    [SavedProperty]
    public BurrowingHeavenPageMode Mode { get; private set; }

    protected override BurrowingHeavenPageMode SelectedMode
    {
        get => Mode;
        set => Mode = value;
    }

    public override Task BeforeCombatStart()
    {
        EnsureValidModeOrFallback(nameof(BeforeCombatStart));
        UpdateModeUiState();
        return Task.CompletedTask;
    }

    public override async Task BeforeSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (Mode != BurrowingHeavenPageMode.OthersGaze
            || Owner.Creature == null
            || !TurnParticipants.IsOwnTurn(Owner.Creature, side, participants)
            || !Owner.Creature.IsAlive)
        {
            UpdateModeUiState();
            return;
        }

        bool triggered = false;
        if (PileType.Hand.GetPile(Owner).Cards.Count == OthersGazeHandCards)
        {
            await PowerCmdCompat.Apply<DrawCardsNextTurnPower>(
                Owner.Creature,
                OthersGazeDraw,
                Owner.Creature,
                null);
            triggered = true;
        }

        if (Owner.PlayerCombatState?.Energy == OthersGazeEnergyRemaining)
        {
            await PowerCmdCompat.Apply<EnergyNextTurnPower>(
                Owner.Creature,
                OthersGazeEnergyNextTurn,
                Owner.Creature,
                null);
            triggered = true;
        }

        if (triggered)
        {
            Flash();
        }

        UpdateModeUiState();
    }

    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (Mode != BurrowingHeavenPageMode.WitheringBloodWings
            || target != Owner.Creature
            || dealer == null
            || dealer.Side == target.Side
            || AllyTurnRegistry.IsFriendlyAlly(dealer)
            || result.BlockedDamage <= 0
            || !result.WasFullyBlocked
            || !ValuePropCompat.IsPoweredAttack(props))
        {
            return;
        }

        int damage = Math.Max(1, (int)Math.Ceiling(result.BlockedDamage * WitheringBloodWingsReflectPercent / 100m));
        Flash();
        await LibraryCreatureCmd.ChaoDamage(
            choiceContext,
            [dealer],
            damage,
            ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move,
            Owner.Creature,
            null,
            null,
            LibraryDamageType.Slash);
    }

    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource, CardPlay? cardPlay)
    {
        return ModifyPhysicalDamageMultiplier(target, dealer, props, cardSource);
    }

    public override decimal ModifyChaoDamageMultiplicative(
        Creature? target,
        decimal num,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay,
        LibraryDamageType type)
    {
        if (Mode != BurrowingHeavenPageMode.AttentionAndFocus)
        {
            return 1m;
        }

        if (IsOwnerDamageSource(dealer, cardSource, props) && target?.Side != Owner.Creature.Side
            && !AllyTurnRegistry.IsFriendlyAlly(target))
        {
            return AttentionAndFocusDamageDealtMultiplier;
        }

        return 1m;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        UpdateModeUiState();
        return Task.CompletedTask;
    }

    private decimal ModifyPhysicalDamageMultiplier(
        Creature? target,
        Creature? dealer,
        ValueProp props,
        CardModel? cardSource)
    {
        if (Mode != BurrowingHeavenPageMode.AttentionAndFocus)
        {
            return 1m;
        }

        if (target == Owner.Creature
            && dealer != null
            && dealer.Side != Owner.Creature.Side
            && ValuePropCompat.IsPoweredAttack(props))
        {
            return AttentionAndFocusDamageTakenMultiplier;
        }

        if (IsOwnerDamageSource(dealer, cardSource, props) && target?.Side != Owner.Creature.Side
            && !AllyTurnRegistry.IsFriendlyAlly(target))
        {
            return AttentionAndFocusDamageDealtMultiplier;
        }

        return 1m;
    }

    protected override IReadOnlyList<CardModel> CreateModeChoiceCards()
    {
        return
        [
            Owner.RunState.CreateCard<BurrowingHeavenWitheringBloodWingsChoiceCard>(Owner),
            Owner.RunState.CreateCard<BurrowingHeavenOthersGazeChoiceCard>(Owner),
            Owner.RunState.CreateCard<BurrowingHeavenAttentionAndFocusChoiceCard>(Owner)
        ];
    }

    protected override void UpdateModeUiState()
    {
        DynamicVars["Mode"].BaseValue = (int)Mode;
        Status = Mode != BurrowingHeavenPageMode.None && CombatManager.Instance.IsInProgress
            ? RelicStatus.Active
            : RelicStatus.Normal;
        InvokeDisplayAmountChanged();
    }

    private bool IsOwnerDamageSource(Creature? dealer, CardModel? cardSource, ValueProp props)
    {
        if (dealer == null || !ValuePropCompat.IsPoweredAttack(props))
        {
            return false;
        }

        if (dealer != Owner.Creature && dealer != Owner.Osty)
        {
            return false;
        }

        return cardSource == null || cardSource.Owner == Owner;
    }
}

public enum BurrowingHeavenPageMode
{
    None = 0,
    WitheringBloodWings = 1,
    OthersGaze = 2,
    AttentionAndFocus = 3
}
