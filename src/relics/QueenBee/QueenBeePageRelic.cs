using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.cards.QueenBee;
using LibraryOfRuina.combat;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.relics;
using LibraryOfRuina.interop;
using LibraryOfRuina.powers.QueenBee;
using MegaCrit.Sts2.Core.Combat;
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

namespace LibraryOfRuina.relics.QueenBee;

public sealed class QueenBeePageRelic : ModalPageRelic<QueenBeePageMode>
{
    internal const int SporeBurnAmount = 8;
    internal const int SporeBleedAmount = 8;
    internal const int ThreatChaosDamage = 4;
    internal const int LoyaltyHpLossPerTrigger = 4;
    internal const int LoyaltyStrength = 1;
    internal const int LoyaltyTurns = 2;

    //private readonly HashSet<CardModel> _processedChaosActions = new();

    protected override string IconBaseName => "queen_bee_page_relic";

    public override RelicRarity Rarity => RelicRarity.Event;

    public override bool IsAllowed(IRunState runState) =>
        AbnormalityPageRewardHelper.CanPageRelicAppear<QueenBeePageRelic>(runState);

    public override bool ShowCounter =>
        CombatManager.Instance.IsInProgress && Mode == QueenBeePageMode.Loyalty;

    public override int DisplayAmount => PendingLoyaltyStrength;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Mode", (int)QueenBeePageMode.None),
        new DynamicVar("SporeBurn", SporeBurnAmount),
        new DynamicVar("SporeBleed", SporeBleedAmount),
        new DynamicVar("ChaosDamage", ThreatChaosDamage),
        new DynamicVar("HpLoss", LoyaltyHpLossPerTrigger),
        new DynamicVar("Strength", LoyaltyStrength),
        new DynamicVar("Turns", LoyaltyTurns)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips => Mode switch
    {
        QueenBeePageMode.Spore =>
        [
            HoverTipFactory.FromPower<LibraryBurnPower>(),
            HoverTipFactory.FromPower<LibraryBleedingPower>()
        ],
        QueenBeePageMode.WorkerBee =>
        [
            HoverTipFactory.FromPower<QueenBeeThreatPower>()
        ],
        QueenBeePageMode.Loyalty =>
        [
            HoverTipFactory.FromPower<LibraryStrongPower>()
        ],
        _ => []
    };

    [SavedProperty]
    public QueenBeePageMode Mode { get; private set; }

    protected override QueenBeePageMode SelectedMode
    {
        get => Mode;
        set => Mode = value;
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool MarkedThisTurn { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int ThreatCombatId { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int PendingLoyaltyStrength { get; private set; }

    /// <summary>
    /// Remainder of accumulated HP loss. Actual applied HP loss is always integer:
    /// Creature.LoseHpInternal truncates its decimal amount with (int)amount before
    /// updating CurrentHp, so a plain int is exact. (The game's [SavedProperty]
    /// serializer also only accepts int/enum/bool/string; a raw decimal throws
    /// JsonException during checksum/save and can freeze combat.)
    /// </summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int HpLossRemainder { get; private set; }

    public override async Task BeforeCombatStart()
    {
        EnsureValidModeOrFallback(nameof(BeforeCombatStart));
        ResetTransientCombatState();
        UpdateModeUiState();
    }

    public override async Task AfterPlayerTurnStart(
        PlayerChoiceContext choiceContext,
        Player player)
    {
        if (player != Owner)
        {
            return;
        }

        MarkedThisTurn = false;
        

        if (Mode == QueenBeePageMode.Loyalty && PendingLoyaltyStrength > 0)
        {
            int pending = PendingLoyaltyStrength;
            PendingLoyaltyStrength = 0;
            IReadOnlyList<Creature> players =
                Owner.Creature.CombatState?.PlayerCreatures
                    .Where(static playerCreature => playerCreature.IsAlive)
                    .ToArray()
                ?? [];
            if (players.Count > 0)
            {
                await LibraryPowerCmd.Apply<LibraryStrongPower>(
                    new ThrowingPlayerChoiceContext(),
                    players,
                    pending * LoyaltyStrength,
                    LoyaltyTurns - 1,
                    IsPermanent: false,
                    Owner.Creature,
                    null);
            }
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
        if (Mode != QueenBeePageMode.Spore
            || target != Owner.Creature
            || dealer == null
            || dealer == Owner.Creature
            || !dealer.IsAlive
            || result.UnblockedDamage <= 0
            || !ValuePropCompat.IsPoweredAttack(props))
        {
            return;
        }

        Flash([dealer]);
        await PowerCmdCompat.Apply<LibraryBurnPower>(
            dealer,
            SporeBurnAmount,
            Owner.Creature,
            null);
        await PowerCmdCompat.Apply<LibraryBleedingPower>(
            dealer,
            SporeBleedAmount,
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
        if (Mode != QueenBeePageMode.WorkerBee)
        {
            return;
        }

        bool ownerAttack = IsOwnerAttackSource(dealer, cardSource, props);
        if (ownerAttack
            && !MarkedThisTurn
            && target.Side != Owner.Creature.Side
            && !AllyTurnRegistry.IsFriendlyAlly(target)
            && target.IsAlive)
        {
            MarkedThisTurn = true;
            ThreatCombatId = (int?)target.CombatId ?? 0;
            Flash([target]);
            await PowerCmdCompat.Apply<QueenBeeThreatPower>(
                target,
                1m,
                Owner.Creature,
                cardSource,
                silent: true);
        }

        if (dealer == null
            || cardSource == null
            || !dealer.IsPlayer
            || target.GetPower<QueenBeeThreatPower>() == null)
        {
            return;
        }

        await LibraryCreatureCmd.ChaoDamage(
            choiceContext,
            [target],
            ThreatChaosDamage,
            ValueProp.Unblockable | ValueProp.Unpowered,
            dealer,
            cardSource,
            type: LibraryDamageType.None);
    }

    /// <summary>
    /// Hook.ModifyHpLost only includes a listener in the AfterModifying callback set
    /// when its Modify* hook actually changed the truncated HP-loss amount. A pure
    /// accumulator that returns the amount unchanged therefore never receives
    /// AfterModifyingHpLostAfterOsty, so Loyalty must accumulate from the real
    /// post-damage HP change instead (negative delta = actual HP lost).
    /// </summary>
    public override Task AfterCurrentHpChanged(
        Creature creature,
        decimal delta)
    {
        if (Mode != QueenBeePageMode.Loyalty
            || creature != Owner?.Creature
            || delta >= 0m)
        {
            UpdateModeUiState();
            return Task.CompletedTask;
        }

        int hpLost = (int)(-delta);
        if (hpLost <= 0)
        {
            return Task.CompletedTask;
        }

        HpLossRemainder += hpLost;
        if (HpLossRemainder < LoyaltyHpLossPerTrigger)
        {
            UpdateModeUiState();
            return Task.CompletedTask;
        }

        int triggers = HpLossRemainder / LoyaltyHpLossPerTrigger;
        HpLossRemainder -= triggers * LoyaltyHpLossPerTrigger;
        PendingLoyaltyStrength += triggers;
        UpdateModeUiState();
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        ResetTransientCombatState();
        UpdateModeUiState();
        return Task.CompletedTask;
    }

    protected override IReadOnlyList<CardModel> CreateModeChoiceCards()
    {
        return
        [
            Owner.RunState.CreateCard<QueenBeeSporeChoiceCard>(Owner),
            Owner.RunState.CreateCard<QueenBeeWorkerBeeChoiceCard>(Owner),
            Owner.RunState.CreateCard<QueenBeeLoyaltyChoiceCard>(Owner)
        ];
    }

    private void ResetTransientCombatState()
    {
        MarkedThisTurn = false;
        ThreatCombatId = 0;
        PendingLoyaltyStrength = 0;
        HpLossRemainder = 0;
        
    }

    protected override void ResetStateOnModeSet(QueenBeePageMode mode) => ResetTransientCombatState();

    protected override void ResetStateOnFallback() => ResetTransientCombatState();

    protected override void UpdateModeUiState()
    {
        DynamicVars["Mode"].BaseValue = (int)Mode;
        Status = Mode == QueenBeePageMode.Loyalty
            && CombatManager.Instance.IsInProgress
            && PendingLoyaltyStrength > 0
                ? RelicStatus.Active
                : RelicStatus.Normal;
        InvokeDisplayAmountChanged();
    }

    private bool IsOwnerAttackSource(
        Creature? dealer,
        CardModel? cardSource,
        ValueProp props)
    {
        if (dealer == null
            || cardSource == null
            || !ValuePropCompat.IsPoweredAttack(props))
        {
            return false;
        }

        if (dealer != Owner.Creature && dealer != Owner.Osty)
        {
            return false;
        }

        return cardSource.Owner == Owner && cardSource.Type == CardType.Attack;
    }
}
