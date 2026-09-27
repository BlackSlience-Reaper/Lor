using System;
using System.Threading.Tasks;
using LibraryOfRuina.combat;
using LibraryOfRuina.cards.HeartOfAspiration;
using LibraryOfRuina.compat;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
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
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.relics.HeartOfAspiration;

public sealed class HeartOfAspirationPageRelic : LibraryRelicModel
{
    public const int PulseStrongStacks = 4;
    //public const int PulseStrongTurns = 1;
    public const int PulseHpLoss = 1;
    public const int AspirationMaxHpPercent = 30;
    public const int ViolentPulseBuffStacks = 8;
    public const int ViolentPulseDeathTurns = 5;

    protected override string IconBaseName => "heart_of_aspiration_page_relic";
    

    public override RelicRarity Rarity => RelicRarity.Event;

    public override bool ShowCounter =>
        Mode == HeartOfAspirationPageMode.ViolentPulse
        && ViolentPulseActivated
        && !ViolentPulseDeathResolved
        && ViolentPulseTurnsRemaining > 0
        && CombatManager.Instance.IsInProgress;

    public override bool HasRightClick => Mode == HeartOfAspirationPageMode.ViolentPulse;

    public override int DisplayAmount => ShowCounter ? ViolentPulseTurnsRemaining : 0;

    public override bool IsAllowed(IRunState runState) =>
        AbnormalityPageRewardHelper.CanPageRelicAppear<HeartOfAspirationPageRelic>(runState);

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<LibraryStrongPower>(),
        HoverTipFactory.FromPower<LibraryEndurancePower>(),
        HoverTipFactory.FromPower<LibraryProtectionPower>(),
        HoverTipFactory.FromPower<LibraryQuicknessPower>()
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Mode", (int)HeartOfAspirationPageMode.None),
        new DynamicVar("Strong", PulseStrongStacks),
        new DynamicVar("HpLoss", PulseHpLoss),
        new DynamicVar("MaxHpPercent", AspirationMaxHpPercent),
        new DynamicVar("RightClickBuffs", ViolentPulseBuffStacks),
        new DynamicVar("DeathTurns", ViolentPulseDeathTurns),
        new DynamicVar("RemainingTurns", 0)
    ];

    [SavedProperty]
    public HeartOfAspirationPageMode Mode { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool PulseDealtLifeDamageThisTurn { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool ViolentPulseActivated { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool ViolentPulseDeathResolved { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int ViolentPulseTurnsRemaining { get; private set; }

    public override async Task AfterObtained()
    {
        if (!IsKnownMode(Mode))
        {
            FallbackToDefaultModeAfterLoad(nameof(AfterObtained));
            return;
        }

        if (Mode != HeartOfAspirationPageMode.None)
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

        await SetMode(ResolveModeFromChoiceCard(chosenCard));
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
        PulseDealtLifeDamageThisTurn = false;
       
        ViolentPulseDeathResolved = false;
        ViolentPulseTurnsRemaining = 0;
        ViolentPulseActivated = false;
        UpdateModeUiState();
        return Task.CompletedTask;
    }

    public override async Task AfterPlayerTurnStart(
        PlayerChoiceContext choiceContext,
        Player player)
    {
        if (Mode != HeartOfAspirationPageMode.Pulse
            || player != Owner
            || Owner.Creature == null
            || Owner.Creature.IsDead)
        {
            return;
        }

        PulseDealtLifeDamageThisTurn = false;
        Flash();
        await LibraryPowerCmd.Apply<LibraryStrongPower>(
            Owner.Creature,
            PulseStrongStacks,
            0,
            Owner.Creature,
            null);
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        ViolentPulseDeathResolved = false;
        ViolentPulseTurnsRemaining = 0;
        ViolentPulseActivated = false;
        return base.AfterCombatEnd(room);
    }

    public override Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        if (Mode == HeartOfAspirationPageMode.Pulse
            && result.UnblockedDamage > 0m
            && target.Side != Owner.Creature.Side
            && !AllyTurnRegistry.IsFriendlyAlly(target)
            && IsOwnerDamageSource(dealer, cardSource))
        {
            PulseDealtLifeDamageThisTurn = true;
        }

        return Task.CompletedTask;
    }

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (Owner.Creature == null || Owner.Creature.IsDead)
        {
            return;
        }

        if (side == Owner.Creature.Side)
        {
            await ResolvePulseHpLoss(choiceContext);
            await TickViolentPulseDeath(choiceContext);
        }
    }
   
    public override async Task AfterSideTurnEndLate(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        await MaintainViolentPulseQuickness(choiceContext, silent: true);
    }

    public override bool CanHandleRightClickLocal(LibraryRightClickContext context)
    {
        return CanExecuteViolentPulse();
    }

    public override bool CanExecuteRightClick(LibraryRightClickExecutionContext context)
    {
        return CanExecuteViolentPulse();
    }

    public override async Task OnRightClick(LibraryRightClickExecutionContext context)
    {
        if (!CanExecuteViolentPulse())
        {
            return;
        }

        await ActivateViolentPulse(context.ChoiceContext);
    }

    private bool CanExecuteViolentPulse()
    {
        return Mode == HeartOfAspirationPageMode.ViolentPulse
            && !ViolentPulseActivated
            && !ViolentPulseDeathResolved
            && Owner?.Creature != null
            && Owner.Creature.IsAlive
            && CombatManager.Instance.IsInProgress;
    }

    private async Task ActivateViolentPulse(PlayerChoiceContext choiceContext)
    {
        ViolentPulseActivated = true;
        ViolentPulseDeathResolved = false;
        ViolentPulseTurnsRemaining = ViolentPulseDeathTurns;
        Flash();

        Creature owner = Owner.Creature;
        await LibraryPowerCmd.Apply<LibraryStrongPower>(owner, ViolentPulseBuffStacks, turns: -1, owner, null);
        await LibraryPowerCmd.Apply<LibraryEndurancePower>(owner, ViolentPulseBuffStacks, turns: -1, owner, null);
        await LibraryPowerCmd.Apply<LibraryProtectionPower>(owner, ViolentPulseBuffStacks, turns: -1, owner, null);
        await MaintainViolentPulseQuickness(choiceContext, silent: false);
        UpdateModeUiState();
    }

    private async Task MaintainViolentPulseQuickness(PlayerChoiceContext choiceContext, bool silent)
    {
        Creature? owner = Owner?.Creature;
        if (Mode != HeartOfAspirationPageMode.ViolentPulse
            || !ViolentPulseActivated
            || ViolentPulseDeathResolved
            || owner == null
            || !owner.IsAlive
            || !CombatManager.Instance.IsInProgress)
        {
            return;
        }

        LibraryQuicknessPower? quickness = owner.GetPower<LibraryQuicknessPower>();
        if (quickness == null)
        {
            await PowerCmdCompat.Apply<LibraryQuicknessPower>(
                choiceContext,
                owner,
                ViolentPulseBuffStacks,
                owner,
                null,
                silent);
            return;
        }

        int amountDelta = ViolentPulseBuffStacks - quickness.Amount;
        if (amountDelta != 0)
        {
            await PowerCmdCompat.ModifyAmount(
                choiceContext,
                quickness,
                amountDelta,
                owner,
                null,
                silent);
        }
    }

    private async Task ResolvePulseHpLoss(PlayerChoiceContext choiceContext)
    {
        if (Mode != HeartOfAspirationPageMode.Pulse)
        {
            return;
        }

        if (!PulseDealtLifeDamageThisTurn)
        {
            Flash();
            await CreatureCmdCompat.Damage(
                choiceContext,
                Owner.Creature,
                PulseHpLoss,
                ValueProp.Unblockable | ValueProp.Unpowered,
                null,
                null);
        }

        PulseDealtLifeDamageThisTurn = false;
    }

    private async Task TickViolentPulseDeath(PlayerChoiceContext choiceContext)
    {
        if (Mode != HeartOfAspirationPageMode.ViolentPulse
            || !ViolentPulseActivated
            || ViolentPulseDeathResolved)
        {
            return;
        }

        ViolentPulseTurnsRemaining = Math.Max(0, ViolentPulseTurnsRemaining - 1);
        UpdateModeUiState();
        if (ViolentPulseTurnsRemaining > 0)
        {
            return;
        }

        ViolentPulseDeathResolved = true;
        Status = RelicStatus.Disabled;
        InvokeDisplayAmountChanged();
        Flash();
        await CreatureCmdCompat.Damage(
            choiceContext,
            Owner.Creature,
            999999,
            ValueProp.Unblockable | ValueProp.Unpowered,
            null,
            null);
    }

    private IReadOnlyList<CardModel> CreateModeChoiceCards()
    {
        return
        [
            Owner.RunState.CreateCard<HeartOfAspirationPulseChoiceCard>(Owner),
            Owner.RunState.CreateCard<HeartOfAspirationAspirationChoiceCard>(Owner),
            Owner.RunState.CreateCard<HeartOfAspirationViolentPulseChoiceCard>(Owner)
        ];
    }

    private static HeartOfAspirationPageMode ResolveModeFromChoiceCard(CardModel? card)
    {
        return card switch
        {
            HeartOfAspirationPulseChoiceCard => HeartOfAspirationPageMode.Pulse,
            HeartOfAspirationAspirationChoiceCard => HeartOfAspirationPageMode.Aspiration,
            HeartOfAspirationViolentPulseChoiceCard => HeartOfAspirationPageMode.ViolentPulse,
            _ => throw AbnormalityPageRewardHelper.UnexpectedPageChoiceCard(card)
        };
    }

    private static bool IsKnownMode(HeartOfAspirationPageMode mode)
    {
        return mode is HeartOfAspirationPageMode.None
            or HeartOfAspirationPageMode.Pulse
            or HeartOfAspirationPageMode.Aspiration
            or HeartOfAspirationPageMode.ViolentPulse;
    }

    private static bool IsConcreteMode(HeartOfAspirationPageMode mode)
    {
        return mode is HeartOfAspirationPageMode.Pulse
            or HeartOfAspirationPageMode.Aspiration
            or HeartOfAspirationPageMode.ViolentPulse;
    }

    [AbnormalityPagePostObtainEffect]
    private async Task SetMode(HeartOfAspirationPageMode mode)
    {
        Mode = mode;
        RelicIconChanged();
        if (mode == HeartOfAspirationPageMode.Aspiration)
        {
            await ApplyAspirationMaxHp();
        }

        UpdateModeUiState();
        RefreshInventoryIcon();
    }

    private async Task ApplyAspirationMaxHp()
    {
        int amount = Math.Max(1, (int)Math.Ceiling(Owner.Creature.MaxHp * AspirationMaxHpPercent / 100m));
        Flash();
        await CreatureCmd.GainMaxHp(Owner.Creature, amount);
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
        HeartOfAspirationPageMode oldMode = Mode;
        Mode = HeartOfAspirationPageMode.Pulse;
        Log.Warn("[LibraryOfRuina.PageRelic] HeartOfAspirationPageRelic recovered loaded Mode "
            + (int)oldMode
            + " during "
            + context
            + "; fallback to Pulse.");
        RelicIconChanged();
        UpdateModeUiState();
        RefreshInventoryIcon();
    }

    private bool IsOwnerDamageSource(Creature? dealer, CardModel? cardSource)
    {
        if (dealer == null || Owner.Creature == null)
        {
            return false;
        }

        if (dealer != Owner.Creature && dealer != Owner.Osty)
        {
            return false;
        }

        return cardSource == null || cardSource.Owner == Owner;
    }

    private void UpdateModeUiState()
    {
        DynamicVars["Mode"].BaseValue = (int)Mode;
        DynamicVars["RemainingTurns"].BaseValue = ViolentPulseTurnsRemaining;
        Status = Mode switch
        {
            HeartOfAspirationPageMode.None => RelicStatus.Normal,
            HeartOfAspirationPageMode.ViolentPulse when ViolentPulseDeathResolved => RelicStatus.Disabled,
            HeartOfAspirationPageMode.ViolentPulse when ViolentPulseActivated => RelicStatus.Active,
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

public enum HeartOfAspirationPageMode
{
    None = 0,
    Pulse = 1,
    Aspiration = 2,
    ViolentPulse = 3
}
