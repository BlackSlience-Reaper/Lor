using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.cards.PriceOfSilence;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace LibraryOfRuina.relics.PriceOfSilence;

public sealed class PriceOfSilencePageRelic : LibraryRelicModel
{
    public const int TimeEnergy = 2;
    public const int TimeDraw = 2;
    public const int TimeCardLimit = 12;
    public const int TimeEnergyLoss = 1;
    public const int ThirteenthCard = 13;
    public const int ThirteenthDraw = 2;
    public const int SilenceWeak = 48;
    public const int SilenceWeakTurns = 1;
    public const int SilenceCooldown = 6;

    protected override string IconBaseName => "price_of_silence_page_relic";

    public override RelicRarity Rarity => RelicRarity.Event;

    public override bool HasRightClick => Mode == PriceOfSilencePageMode.Silence;

    public override bool ShowCounter =>
        (CombatManager.Instance.IsInProgress
        && Mode is PriceOfSilencePageMode.Time) ||
        Mode == PriceOfSilencePageMode.ThirteenthToll
        || (Mode == PriceOfSilencePageMode.Silence
        && SilenceCooldownRemaining != 0);

    public override int DisplayAmount => Mode switch
    {
        PriceOfSilencePageMode.Time => Math.Max(0, TimeCardLimit - CardsPlayedThisTurn),
        PriceOfSilencePageMode.ThirteenthToll => CardsPlayedThisCombat % ThirteenthCard,
        PriceOfSilencePageMode.Silence => Math.Max(0, SilenceCooldownRemaining),
        _ => 0
    };

    public override bool IsAllowed(IRunState runState) =>
        AbnormalityPageRewardHelper.CanPageRelicAppear<PriceOfSilencePageRelic>(runState);

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.ForEnergy(this),
        HoverTipFactory.FromPower<LibraryWeakPower>()
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Mode", (int)PriceOfSilencePageMode.None),
        new EnergyVar(TimeEnergy),
        new DynamicVar("Cards", TimeDraw),
        new DynamicVar("CardLimit", TimeCardLimit),
        new DynamicVar("EnergyLoss", TimeEnergyLoss),
        new DynamicVar("Thirteenth", ThirteenthCard),
        new DynamicVar("Draw", ThirteenthDraw),
        new DynamicVar("Weak", SilenceWeak),
        new DynamicVar("Turns", SilenceWeakTurns),
        new DynamicVar("Cooldown", SilenceCooldown),
        new DynamicVar("Remaining", 0)
    ];

    [SavedProperty]
    public PriceOfSilencePageMode Mode { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int CardsPlayedThisTurn { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int CardsPlayedThisCombat { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int SilenceCooldownRemaining { get; private set; }

    public override async Task AfterObtained()
    {
        if (!IsKnownMode(Mode))
        {
            FallbackToDefaultModeAfterLoad(nameof(AfterObtained));
            return;
        }

        if (Mode != PriceOfSilencePageMode.None)
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

    public override Task BeforeCombatStart()
    {
        EnsureValidModeOrFallback(nameof(BeforeCombatStart));
        CardsPlayedThisTurn = 0;
        //SilenceCooldownRemaining = 0;
        UpdateModeUiState();
        return Task.CompletedTask;
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner)
        {
            return;
        }

        CardsPlayedThisTurn = 0;
        if (SilenceCooldownRemaining > 0)
        {
            SilenceCooldownRemaining--;
        }

        if (Mode == PriceOfSilencePageMode.Time
            && Owner.Creature != null
            && Owner.Creature.IsAlive)
        {
            Flash();
            await PlayerCmd.GainEnergy(TimeEnergy, Owner);
            await CardPileCmd.Draw(choiceContext, TimeDraw, Owner);
        }

        UpdateModeUiState();
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != Owner)
        {
            return;
        }

        if (Mode == PriceOfSilencePageMode.Time)
        {
            CardsPlayedThisTurn++;
            if (CardsPlayedThisTurn > TimeCardLimit)
            {
                Flash();
                await PlayerCmd.LoseEnergy(TimeEnergyLoss, Owner);
            }
        }
        else if (Mode == PriceOfSilencePageMode.ThirteenthToll)
        {
            CardsPlayedThisCombat++;
            if (CardsPlayedThisCombat % ThirteenthCard == 0)
            {
                Flash();
                await CardPileCmd.Draw(context, ThirteenthDraw, Owner);
            }
        }

        UpdateModeUiState();
    }

    public override bool TryModifyEnergyCostInCombat(
        CardModel card,
        decimal originalCost,
        out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (Mode != PriceOfSilencePageMode.ThirteenthToll
            || card.Owner != Owner
            || originalCost <= 0m
            || !CombatManager.Instance.IsInProgress)
        {
            return false;
        }

        if ((CardsPlayedThisCombat + 1) % ThirteenthCard != 0)
        {
            return false;
        }

        modifiedCost = 0m;
        return true;
    }

    public override bool CanHandleRightClickLocal(LibraryRightClickContext context) =>
        CanUseSilenceRightClick();

    public override bool CanExecuteRightClick(LibraryRightClickExecutionContext context) =>
        CanUseSilenceRightClick();

    public override async Task OnRightClick(LibraryRightClickExecutionContext context)
    {
        if (!CanUseSilenceRightClick())
        {
            return;
        }

        Flash();
        if (Owner?.Creature is not { CombatState: { } combatState } ownerCreature)
        {
            return;
        }

        foreach (Creature target in combatState.Creatures
            .Where(creature => creature.IsAlive && creature != ownerCreature))
        {
            await LibraryPowerCmd.Apply<LibraryWeakPower>(
                new ThrowingPlayerChoiceContext(),
                target,
                SilenceWeak,
                SilenceWeakTurns - 1,
                IsPermanent: false,
                Owner.Creature,
                null);
               
        }

        SilenceCooldownRemaining = SilenceCooldown;
        UpdateModeUiState();
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        CardsPlayedThisTurn = 0;
        SilenceCooldownRemaining = 0;
        UpdateModeUiState();
        return Task.CompletedTask;
    }

    private bool CanUseSilenceRightClick()
    {
        return Mode == PriceOfSilencePageMode.Silence
            && SilenceCooldownRemaining <= 0
            && Owner?.Creature != null
            && Owner.Creature.IsAlive
            && Owner.Creature.CombatState != null
            && CombatManager.Instance.IsInProgress;
    }

    private IReadOnlyList<CardModel> CreateModeChoiceCards()
    {
        return
        [
            Owner.RunState.CreateCard<PriceOfSilenceTimeChoiceCard>(Owner),
            Owner.RunState.CreateCard<PriceOfSilenceThirteenthTollChoiceCard>(Owner),
            Owner.RunState.CreateCard<PriceOfSilenceSilenceChoiceCard>(Owner)
        ];
    }

    private static PriceOfSilencePageMode ResolveModeFromChoiceCard(CardModel? card)
    {
        return card switch
        {
            PriceOfSilenceTimeChoiceCard => PriceOfSilencePageMode.Time,
            PriceOfSilenceThirteenthTollChoiceCard => PriceOfSilencePageMode.ThirteenthToll,
            PriceOfSilenceSilenceChoiceCard => PriceOfSilencePageMode.Silence,
            _ => throw AbnormalityPageRewardHelper.UnexpectedPageChoiceCard(card)
        };
    }

    private static bool IsKnownMode(PriceOfSilencePageMode mode)
    {
        return mode is PriceOfSilencePageMode.None
            or PriceOfSilencePageMode.Time
            or PriceOfSilencePageMode.ThirteenthToll
            or PriceOfSilencePageMode.Silence;
    }

    private static bool IsConcreteMode(PriceOfSilencePageMode mode)
    {
        return mode is PriceOfSilencePageMode.Time
            or PriceOfSilencePageMode.ThirteenthToll
            or PriceOfSilencePageMode.Silence;
    }

    private void SetMode(PriceOfSilencePageMode mode)
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
        PriceOfSilencePageMode oldMode = Mode;
        Mode = PriceOfSilencePageMode.Time;
        CardsPlayedThisTurn = 0;
        CardsPlayedThisCombat = 0;
        SilenceCooldownRemaining = 0;
        Log.Warn("[LibraryOfRuina.PageRelic] PriceOfSilencePageRelic recovered loaded Mode "
            + (int)oldMode
            + " during "
            + context
            + "; fallback to Time.");
        RelicIconChanged();
        UpdateModeUiState();
        RefreshInventoryIcon();
    }

    private void UpdateModeUiState()
    {
        DynamicVars["Mode"].BaseValue = (int)Mode;
        DynamicVars["Remaining"].BaseValue = DisplayAmount;
        Status = Mode switch
        {
            PriceOfSilencePageMode.None => RelicStatus.Normal,
            PriceOfSilencePageMode.Silence when SilenceCooldownRemaining > 0 => RelicStatus.Disabled,
            _ => RelicStatus.Normal
        };
        InvokeDisplayAmountChanged();
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

public enum PriceOfSilencePageMode
{
    None = 0,
    Time = 1,
    ThirteenthToll = 2,
    Silence = 3
}
