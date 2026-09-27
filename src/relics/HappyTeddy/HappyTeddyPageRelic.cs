using System.Threading.Tasks;
using LibraryLib.Commands;
using LibraryLib.Powers;
using MegaCrit.Sts2.Core.HoverTips;
using LibraryOfRuina.combat;
using LibraryOfRuina.cards.HappyTeddy;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.relics.HappyTeddy;

public sealed class HappyTeddyPageRelic : RelicModel
{
    // 思念的拥抱：每次攻击击破敌方已有格挡时获得的格挡。
    internal const int LongingEmbraceBlockGain = 8;

    // 思念的拥抱：战斗开始时获得的斩击威力增强层数。
    internal const int LongingEmbraceSlashPower = 3;

    // 思念的拥抱：斩击威力增强持续整场战斗。
    private const int LongingEmbraceSlashPowerTurns = -1;
    internal const int HappyMemoryCostReduction = 1;
    internal const int ExpressAffectionTurnInterval = 3;
    internal const int ExpressAffectionBlockPercent = 20;

    private CardModel? _expressAffectionActiveCard;
    private int _expressAffectionUnblockedDamage;

    //private bool _expressAffectionTriggerTurn;

    protected override string IconBaseName => Mode switch
    {
        HappyTeddyPageMode.LongingEmbrace => "happy_teddy_page_longing_embrace_relic",
        HappyTeddyPageMode.HappyMemory => "happy_teddy_page_happy_memory_relic",
        HappyTeddyPageMode.ExpressAffection => "happy_teddy_page_express_affection_relic",
        _ => "happy_teddy_page_relic"
    };

    public override RelicRarity Rarity => RelicRarity.Event;

    public override bool IsAllowed(IRunState runState) =>
        AbnormalityPageRewardHelper.CanPageRelicAppear<HappyTeddyPageRelic>(runState);

    public override bool ShowCounter => Mode == HappyTeddyPageMode.ExpressAffection;

    public override int DisplayAmount =>
        Mode == HappyTeddyPageMode.ExpressAffection ? GetExpressAffectionDisplayAmount() : 0;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Mode", (int)HappyTeddyPageMode.None),
        new BlockVar(LongingEmbraceBlockGain, ValueProp.Unpowered),
        new DynamicVar("SlashPower", LongingEmbraceSlashPower),
        new DynamicVar("CostReduction", HappyMemoryCostReduction),
        new DynamicVar("TurnInterval", ExpressAffectionTurnInterval),
        new DynamicVar("BlockPercent", ExpressAffectionBlockPercent)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        Mode == HappyTeddyPageMode.LongingEmbrace
            ? [HoverTipFactory.FromPower<LibraryStrongSlashPower>()]
            : [];

    [SavedProperty]
    public HappyTeddyPageMode Mode { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool HappyMemoryPendingFirstDraw { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int ExpressAffectionTurnsSeen { get; private set; }

    public override async Task AfterObtained()
    {
        if (!IsKnownMode(Mode))
        {
            FallbackToDefaultModeAfterLoad(nameof(AfterObtained));
            return;
        }

        if (Mode != HappyTeddyPageMode.None)
        {
            UpdateModeUiState();
            RefreshInventoryIcon();
            return;
        }

        IReadOnlyList<CardModel> options = CreateModeChoiceCards();
        CardModel? chosenCard = await CardSelectCmd.FromChooseACardScreen(
            new BlockingPlayerChoiceContext(),
            options,
            Owner,
            canSkip: true);

        if (chosenCard == null)
        {
            await AbnormalityPageRewardHelper.SkipObtainedPageRelic(this, nameof(AfterObtained));
            return;
        }

        SetMode(ResolveModeFromChoiceCard(chosenCard));
    }

    public override Task AfterRoomEntered(AbstractRoom room)
    {
        EnsureValidModeOrFallback(nameof(AfterRoomEntered));
        UpdateModeUiState();
        RefreshInventoryIcon();
        return Task.CompletedTask;
    }

    public override async Task BeforeCombatStart()
    {
        EnsureValidModeOrFallback(nameof(BeforeCombatStart));
        ResetTransientCombatState();

        HappyMemoryPendingFirstDraw = Mode == HappyTeddyPageMode.HappyMemory;
        if (Mode == HappyTeddyPageMode.LongingEmbrace)
        {
            Flash();
            await LibraryPowerCmd.Apply<LibraryStrongSlashPower>(
                Owner.Creature,
                LongingEmbraceSlashPower,
                turns: LongingEmbraceSlashPowerTurns,
                Owner.Creature,
                null);
        }

        UpdateModeUiState();
    }

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (Mode != HappyTeddyPageMode.ExpressAffection
            || cardPlay.Card.Owner != Owner
            || cardPlay.Card.Type != CardType.Attack
            || !IsExpressAffectionRound())
        {
            return Task.CompletedTask;
        }

        _expressAffectionActiveCard = cardPlay.Card;
        _expressAffectionUnblockedDamage = 0;
        Flash();
        UpdateModeUiState();
        return Task.CompletedTask;
    }

    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player == Owner
            && Mode == HappyTeddyPageMode.ExpressAffection
            && CombatManager.Instance.IsInProgress)
        {
            //_expressAffectionTriggerTurn = ExpressAffectionTurnsSeen >= ExpressAffectionTurnInterval;
            ExpressAffectionTurnsSeen++;
            if (ExpressAffectionTurnsSeen > ExpressAffectionTurnInterval)
            {
                ExpressAffectionTurnsSeen = 1;
            }
            UpdateModeUiState();
        }

        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        if (!ReferenceEquals(_expressAffectionActiveCard, cardPlay.Card))
        {
            return;
        }

        int block = _expressAffectionUnblockedDamage * ExpressAffectionBlockPercent / 100;
        _expressAffectionActiveCard = null;
        _expressAffectionUnblockedDamage = 0;
        if (block > 0)
        {
            Flash();
            await CreatureCmd.GainBlock(Owner.Creature, block, ValueProp.Unpowered, null, fast: true);
        }

        UpdateModeUiState();
    }

    public override Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (Mode != HappyTeddyPageMode.HappyMemory || !HappyMemoryPendingFirstDraw || card.Owner != Owner)
        {
            return Task.CompletedTask;
        }

        HappyMemoryPendingFirstDraw = false;
        card.EnergyCost.AddThisTurn(-HappyMemoryCostReduction, reduceOnly: true);
        Flash();
        UpdateModeUiState();
        return Task.CompletedTask;
    }

    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        if (Mode == HappyTeddyPageMode.ExpressAffection
            && ReferenceEquals(_expressAffectionActiveCard, cardSource)
            && result.UnblockedDamage > 0
            && target.Side != Owner.Creature.Side
            && !AllyTurnRegistry.IsFriendlyAlly(target)
            && IsOwnerAttackSource(dealer, cardSource, props))
        {
            _expressAffectionUnblockedDamage += result.UnblockedDamage;
        }

        if (Mode != HappyTeddyPageMode.LongingEmbrace
            || !result.WasBlockBroken
            || !IsOwnerAttackSource(dealer, cardSource, props)
            || target.Side == Owner.Creature.Side
            || AllyTurnRegistry.IsFriendlyAlly(target))
        {
            return;
        }

        Flash([target]);
        await CreatureCmd.GainBlock(Owner.Creature, LongingEmbraceBlockGain, ValueProp.Unpowered, null, fast: true);
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        ResetTransientCombatState();
        UpdateModeUiState();
        return Task.CompletedTask;
    }

    private IReadOnlyList<CardModel> CreateModeChoiceCards()
    {
        return
        [
            Owner.RunState.CreateCard<HappyTeddyLongingEmbraceChoiceCard>(Owner),
            Owner.RunState.CreateCard<HappyTeddyHappyMemoryChoiceCard>(Owner),
            Owner.RunState.CreateCard<HappyTeddyExpressAffectionChoiceCard>(Owner)
        ];
    }

    private static HappyTeddyPageMode ResolveModeFromChoiceCard(CardModel? card)
    {
        return card switch
        {
            HappyTeddyLongingEmbraceChoiceCard => HappyTeddyPageMode.LongingEmbrace,
            HappyTeddyHappyMemoryChoiceCard => HappyTeddyPageMode.HappyMemory,
            HappyTeddyExpressAffectionChoiceCard => HappyTeddyPageMode.ExpressAffection,
            _ => throw AbnormalityPageRewardHelper.UnexpectedPageChoiceCard(card)
        };
    }

    private static bool IsKnownMode(HappyTeddyPageMode mode)
    {
        return mode is HappyTeddyPageMode.None
            or HappyTeddyPageMode.LongingEmbrace
            or HappyTeddyPageMode.HappyMemory
            or HappyTeddyPageMode.ExpressAffection;
    }

    private static bool IsConcreteMode(HappyTeddyPageMode mode)
    {
        return mode is HappyTeddyPageMode.LongingEmbrace
            or HappyTeddyPageMode.HappyMemory
            or HappyTeddyPageMode.ExpressAffection;
    }

    private void SetMode(HappyTeddyPageMode mode)
    {
        Mode = mode;
        RelicIconChanged();
        UpdateModeUiState();
        RefreshInventoryIcon();
    }

    private void EnsureValidModeOrFallback(string context)
    {
        if (IsConcreteMode(Mode))
        {
            return;
        }

        FallbackToDefaultModeAfterLoad(context);
    }

    private void FallbackToDefaultModeAfterLoad(string context)
    {
        HappyTeddyPageMode oldMode = Mode;
        Mode = HappyTeddyPageMode.LongingEmbrace;
        ResetTransientCombatState();
        Log.Warn("[LibraryOfRuina.PageRelic] HappyTeddyPageRelic recovered loaded Mode "
            + (int)oldMode
            + " during "
            + context
            + "; fallback to LongingEmbrace.");
        RelicIconChanged();
        UpdateModeUiState();
        RefreshInventoryIcon();
    }

    private void ResetTransientCombatState()
    {
        _expressAffectionActiveCard = null;
        _expressAffectionUnblockedDamage = 0;
        
        HappyMemoryPendingFirstDraw = false;
    }

    private void UpdateModeUiState()
    {
        DynamicVars["Mode"].BaseValue = (int)Mode;
        Status = Mode switch
        {
            HappyTeddyPageMode.HappyMemory when CombatManager.Instance.IsInProgress =>
                HappyMemoryPendingFirstDraw ? RelicStatus.Active : RelicStatus.Disabled,
            HappyTeddyPageMode.ExpressAffection when CombatManager.Instance.IsInProgress =>
                IsExpressAffectionRound() ? RelicStatus.Active : RelicStatus.Normal,
            _ => RelicStatus.Normal
        };
        InvokeDisplayAmountChanged();
    }

    private bool IsExpressAffectionRound() =>
        CombatManager.Instance.IsInProgress && ExpressAffectionTurnsSeen == ExpressAffectionTurnInterval;

    private int GetExpressAffectionDisplayAmount() => ExpressAffectionTurnsSeen;

    private bool IsOwnerAttackSource(Creature? dealer, CardModel? cardSource, ValueProp props)
    {
        if (dealer == null || cardSource == null || !ValuePropCompat.IsPoweredAttack(props))
        {
            return false;
        }

        if (dealer != Owner.Creature && dealer != Owner.Osty)
        {
            return false;
        }

        return cardSource.Owner == Owner && cardSource.Type == CardType.Attack;
    }

    private void RefreshInventoryIcon()
    {
        NRelicInventory? inventory = NRun.Instance?.GlobalUi?.RelicInventory;
        if (inventory == null)
        {
            return;
        }

        foreach (NRelicInventoryHolder holder in inventory.RelicNodes)
        {
            if (!ReferenceEquals(holder.Relic.Model, this))
            {
                continue;
            }

            holder.Relic.Icon.Texture = Icon;
            holder.Relic.Outline.Texture = IconOutline;
            break;
        }
    }
}
