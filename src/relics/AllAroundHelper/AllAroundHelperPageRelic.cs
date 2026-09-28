using System.Linq;
using System.Threading.Tasks;
using LibraryLib.Commands;
using LibraryOfRuina.cards.AllAroundHelper;
using LibraryOfRuina.compat;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.relics.AllAroundHelper;

public sealed class AllAroundHelperPageRelic : ModalPageRelic<AllAroundHelperPageMode>
{
    internal const int ChargeHandThreshold = 5;
    internal const int ChargeEnergyNextTurn = 1;
    internal const int RecognitionCardsPerSwift = 9;
    internal const int RecognitionSwift = 1;

    protected override string IconBaseName => "all_around_helper_page_relic";

    public override RelicRarity Rarity => RelicRarity.Event;

    public override bool IsAllowed(IRunState runState) =>
        AbnormalityPageRewardHelper.CanPageRelicAppear<AllAroundHelperPageRelic>(runState);

    public override bool ShowCounter =>
        CombatManager.Instance.IsInProgress
        && Mode == AllAroundHelperPageMode.RecognitionFunction;

    public override int DisplayAmount =>
        Mode == AllAroundHelperPageMode.RecognitionFunction ? RecognitionCardsPlayedRemainder : 0;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Mode", (int)AllAroundHelperPageMode.None),
        new DynamicVar("HandThreshold", ChargeHandThreshold),
        new EnergyVar(ChargeEnergyNextTurn),
        new CardsVar(RecognitionCardsPerSwift),
        new DynamicVar("Swift", RecognitionSwift)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips => Mode switch
    {
        AllAroundHelperPageMode.Charge =>
        [
            HoverTipFactory.ForEnergy(this)
        ],
        AllAroundHelperPageMode.RecognitionFunction =>
        [
            HoverTipFactory.FromPower<DrawCardsNextTurnPower>()
        ],
        _ => []
    };

    [SavedProperty]
    public AllAroundHelperPageMode Mode { get; private set; }

    protected override AllAroundHelperPageMode SelectedMode
    {
        get => Mode;
        set => Mode = value;
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int RecognitionCardsPlayedRemainder { get; private set; }

    // 保留旧清洁触发标记的存档字段，当前效果不再读取它。
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool CleanTriggeredThisTurn { get; private set; }

    public override Task BeforeCombatStart()
    {
        EnsureValidModeOrFallback(nameof(BeforeCombatStart));
        ResetTransientCombatState();
        UpdateModeUiState();
        return Task.CompletedTask;
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner || Mode != AllAroundHelperPageMode.Clean)
        {
            return;
        }

        await TriggerClean(choiceContext);
        UpdateModeUiState();
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != Owner)
        {
            return;
        }

        if (Mode == AllAroundHelperPageMode.RecognitionFunction)
        {
            RecognitionCardsPlayedRemainder++;
            bool gainedSwift = false;
            while (RecognitionCardsPlayedRemainder >= RecognitionCardsPerSwift)
            {
                RecognitionCardsPlayedRemainder -= RecognitionCardsPerSwift;
                gainedSwift = true;
                await PowerCmdCompat.Apply<DrawCardsNextTurnPower>(
                    Owner.Creature,
                    1m,
                    Owner.Creature,
                    cardPlay.Card);
            }

            if (gainedSwift)
            {
                Flash();
            }
        }

        UpdateModeUiState();
    }

    public override Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (card.Owner == Owner)
        {
            UpdateModeUiState();
        }

        return Task.CompletedTask;
    }

    public override Task AfterCardDiscarded(PlayerChoiceContext choiceContext, CardModel card)
    {
        if (card.Owner == Owner)
        {
            UpdateModeUiState();
        }

        return Task.CompletedTask;
    }

    public override Task AfterCardExhausted(PlayerChoiceContext choiceContext, CardModel card, bool causedByEthereal)
    {
        if (card.Owner == Owner)
        {
            UpdateModeUiState();
        }

        return Task.CompletedTask;
    }

    public override async Task BeforeSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (Mode == AllAroundHelperPageMode.Charge
            && side == Owner.Creature.Side
            && GetCurrentHandCount() >= ChargeHandThreshold)
        {
            Flash();
            await PowerCmdCompat.Apply<EnergyNextTurnPower>(Owner.Creature, ChargeEnergyNextTurn, Owner.Creature, null);
        }
    }

    public override Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
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
            Owner.RunState.CreateCard<AllAroundHelperChargeChoiceCard>(Owner),
            Owner.RunState.CreateCard<AllAroundHelperRecognitionFunctionChoiceCard>(Owner),
            Owner.RunState.CreateCard<AllAroundHelperCleanChoiceCard>(Owner)
        ];
    }

    private void ResetTransientCombatState()
    {
        RecognitionCardsPlayedRemainder = 0;
        CleanTriggeredThisTurn = false;
    }

    protected override void ResetStateOnModeSet(AllAroundHelperPageMode mode) => ResetTransientCombatState();

    protected override void ResetStateOnFallback() => ResetTransientCombatState();

    protected override void UpdateModeUiState()
    {
        DynamicVars["Mode"].BaseValue = (int)Mode;
        Status = Mode switch
        {
            AllAroundHelperPageMode.Charge when CombatManager.Instance.IsInProgress =>
                GetCurrentHandCount() >= ChargeHandThreshold ? RelicStatus.Active : RelicStatus.Disabled,
            AllAroundHelperPageMode.RecognitionFunction when CombatManager.Instance.IsInProgress =>
                RecognitionCardsPlayedRemainder > 0 ? RelicStatus.Active : RelicStatus.Normal,
            AllAroundHelperPageMode.Clean when CombatManager.Instance.IsInProgress =>
                GetCurrentHandCount() > 0
                    ? RelicStatus.Active
                    : RelicStatus.Disabled,
            _ => RelicStatus.Normal
        };
        InvokeDisplayAmountChanged();
    }

    private async Task TriggerClean(PlayerChoiceContext context)
    {
        int handCount = GetCurrentHandCount();
        if (handCount <= 0)
        {
            return;
        }

        Creature? ownerCreature = Owner?.Creature;
        var combatState = ownerCreature?.CombatState;
        if (ownerCreature == null || combatState == null)
        {
            return;
        }

        IReadOnlyList<Creature> targets = combatState.HittableEnemies.ToList();
        if (targets.Count == 0)
        {
            return;
        }

        Flash(targets);
        await CreatureCmdCompat.Damage(
            context,
            targets,
            handCount,
            ValueProp.Unpowered,
            ownerCreature,
            null);
        await LibraryCreatureCmd.ChaoDamage(
            context,
            targets,
            handCount,
            ValueProp.Unpowered,
            ownerCreature,
            null);
    }

    private int GetCurrentHandCount()
    {
        if (Owner.PlayerCombatState == null)
        {
            return 0;
        }

        return PileType.Hand.GetPile(Owner).Cards.Count;
    }
}
