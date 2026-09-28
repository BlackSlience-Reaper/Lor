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
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace LibraryOfRuina.relics.PriceOfSilence;

public sealed class PriceOfSilencePageRelic : ModalPageRelic<PriceOfSilencePageMode>
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

    protected override PriceOfSilencePageMode SelectedMode
    {
        get => Mode;
        set => Mode = value;
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int CardsPlayedThisTurn { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int CardsPlayedThisCombat { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int SilenceCooldownRemaining { get; private set; }

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

    protected override IReadOnlyList<CardModel> CreateModeChoiceCards()
    {
        return
        [
            Owner.RunState.CreateCard<PriceOfSilenceTimeChoiceCard>(Owner),
            Owner.RunState.CreateCard<PriceOfSilenceThirteenthTollChoiceCard>(Owner),
            Owner.RunState.CreateCard<PriceOfSilenceSilenceChoiceCard>(Owner)
        ];
    }

    protected override void ResetStateOnFallback()
    {
        CardsPlayedThisTurn = 0;
        CardsPlayedThisCombat = 0;
        SilenceCooldownRemaining = 0;
    }

    protected override void UpdateModeUiState()
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
}

public enum PriceOfSilencePageMode
{
    None = 0,
    Time = 1,
    ThirteenthToll = 2,
    Silence = 3
}
