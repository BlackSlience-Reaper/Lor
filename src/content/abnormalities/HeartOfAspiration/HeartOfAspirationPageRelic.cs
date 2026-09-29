using System;
using System.Threading.Tasks;
using LibraryOfRuina.combat;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
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

namespace LibraryOfRuina.content.abnormalities.HeartOfAspiration;

public sealed class HeartOfAspirationPageRelic : ModalPageRelic<HeartOfAspirationPageMode>
{
    public const int PulseStrongStacks = 4;
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

    protected override HeartOfAspirationPageMode SelectedMode
    {
        get => Mode;
        set => Mode = value;
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool PulseDealtLifeDamageThisTurn { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool ViolentPulseActivated { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool ViolentPulseDeathResolved { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int ViolentPulseTurnsRemaining { get; private set; }

    protected override Task ApplyObtainedChoiceAsync(HeartOfAspirationPageMode mode) => SetModeAsync(mode);

    // 预选只写模式、通知图标变化并刷新界面；清状态与拾取效果由获得后作为 AbnormalityPagePostObtainEffect 执行的 SetModeAsync 完成。
    protected override void ApplyPreselectedMode(HeartOfAspirationPageMode mode) => AssignPreselectedModeOnly(mode);

    public override async Task BeforeCombatStart()
    {
        EnsureValidModeOrFallback(nameof(BeforeCombatStart));
        PulseDealtLifeDamageThisTurn = false;
       
        ViolentPulseDeathResolved = false;
        ViolentPulseTurnsRemaining = 0;
        ViolentPulseActivated = false;
        UpdateModeUiState();

        if (Mode == HeartOfAspirationPageMode.Pulse
            && Owner.Creature is { IsAlive: true } owner)
        {
            Flash();
            await LibraryPowerCmd.Apply<LibraryStrongPower>(
                owner,
                PulseStrongStacks,
                turns: -1,
                owner,
                null);
        }
    }

    public override Task AfterPlayerTurnStart(
        PlayerChoiceContext choiceContext,
        Player player)
    {
        if (Mode != HeartOfAspirationPageMode.Pulse
            || player != Owner
            || Owner.Creature == null
            || Owner.Creature.IsDead)
        {
            return Task.CompletedTask;
        }

        PulseDealtLifeDamageThisTurn = false;
        return Task.CompletedTask;
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

    protected override IReadOnlyList<CardModel> CreateModeChoiceCards()
    {
        return
        [
            Owner.RunState.CreateCard<HeartOfAspirationPulseChoiceCard>(Owner),
            Owner.RunState.CreateCard<HeartOfAspirationAspirationChoiceCard>(Owner),
            Owner.RunState.CreateCard<HeartOfAspirationViolentPulseChoiceCard>(Owner)
        ];
    }

    [AbnormalityPagePostObtainEffect]
    private async Task SetModeAsync(HeartOfAspirationPageMode mode)
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

    protected override void UpdateModeUiState()
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
}

public enum HeartOfAspirationPageMode
{
    None = 0,
    Pulse = 1,
    Aspiration = 2,
    ViolentPulse = 3
}
