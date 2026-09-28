using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.cards.BigBird;
using LibraryOfRuina.compat;
using LibraryOfRuina.powers.BigBird;
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
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.relics.BigBird;

public sealed class BigBirdPageRelic : ModalPageRelic<BigBirdPageMode>
{
    public const int WatchfulEyeCooldown = 2;
    public const int WatchfulEyeEnergyPenalty = 1;
    public const int EverBurningLampCooldown = 6;
    public const int SalvationStrong = 3;
    public const int SalvationDamage = 3;

    protected override string IconBaseName => "big_bird_page_relic";
    

    public override RelicRarity Rarity => RelicRarity.Event;

    public override bool HasRightClick =>
        Mode is BigBirdPageMode.WatchfulEye or BigBirdPageMode.EverBurningLamp;

    public override bool ShowCounter =>
        CombatManager.Instance.IsInProgress
        && ((Mode == BigBirdPageMode.WatchfulEye && WatchfulEyeCooldownRemaining > 0)
            || (Mode == BigBirdPageMode.EverBurningLamp && EverBurningLampCooldownRemaining > 0));

    public override int DisplayAmount => Mode switch
    {
        BigBirdPageMode.WatchfulEye => Math.Max(0, WatchfulEyeCooldownRemaining),
        BigBirdPageMode.EverBurningLamp => Math.Max(0, EverBurningLampCooldownRemaining),
        _ => 0
    };

    public override bool IsAllowed(IRunState runState) =>
        AbnormalityPageRewardHelper.CanPageRelicAppear<BigBirdPageRelic>(runState);

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.ForEnergy(this),
        HoverTipFactory.FromPower<LibraryStrongPower>()
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Mode", (int)BigBirdPageMode.None),
        new EnergyVar("Energy", WatchfulEyeEnergyPenalty),
        new DynamicVar("WatchfulCooldown", WatchfulEyeCooldown),
        new DynamicVar("LampCooldown", EverBurningLampCooldown),
        new DynamicVar("Strong", SalvationStrong),
        new DynamicVar("Damage", SalvationDamage),
        new DynamicVar("Remaining", 0)
    ];

    [SavedProperty]
    public BigBirdPageMode Mode { get; private set; }

    protected override BigBirdPageMode SelectedMode
    {
        get => Mode;
        set => Mode = value;
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int WatchfulEyeCooldownRemaining { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int EverBurningLampCooldownRemaining { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int PendingExtraTurns { get; private set; }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int PendingWatchfulEyeTargetCombatId { get; private set; }

    public override async Task BeforeCombatStart()
    {
        EnsureValidModeOrFallback(nameof(BeforeCombatStart));
        PendingExtraTurns = 0;
        PendingWatchfulEyeTargetCombatId = 0;
        if (Mode == BigBirdPageMode.Salvation && Owner?.Creature is { } ownerCreature)
        {
            await LibraryPowerCmd.Apply<LibraryStrongPower>(
                ownerCreature,
                SalvationStrong,
                turns: -1,
                ownerCreature,
                null);
        }

        UpdateModeUiState();
    }

    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner)
        {
            return Task.CompletedTask;
        }

        if (WatchfulEyeCooldownRemaining > 0)
        {
            WatchfulEyeCooldownRemaining--;
        }

        if (EverBurningLampCooldownRemaining > 0)
        {
            EverBurningLampCooldownRemaining--;
        }

        UpdateModeUiState();
        return Task.CompletedTask;
    }

    public override async Task AfterEnergyReset(Player player)
    {
        if (Mode != BigBirdPageMode.WatchfulEye
            || PendingWatchfulEyeTargetCombatId == 0
            || player.Creature.CombatId != (uint)PendingWatchfulEyeTargetCombatId
            || Owner?.Creature == null)
        {
            return;
        }

        await PowerCmdCompat.SetAmount<BigBirdEnergySealPower>(
            player.Creature,
            WatchfulEyeEnergyPenalty,
            Owner.Creature,
            null);
        await PlayerCmd.LoseEnergy(WatchfulEyeEnergyPenalty, player);
        PendingWatchfulEyeTargetCombatId = 0;
        UpdateModeUiState();
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        if (Mode != BigBirdPageMode.Salvation
            || cardPlay.Card.Owner != Owner
            || Owner?.Creature?.CombatState == null)
        {
            return;
        }

        Flash();
        Creature[] enemies = Owner.Creature.CombatState.HittableEnemies
            .Where(static creature => creature.IsAlive)
            .ToArray();
        foreach (Creature enemy in enemies)
        {
            await CreatureCmdCompat.Damage(
                context,
                enemy,
                SalvationDamage,
                ValueProp.Unpowered,
                Owner.Creature,
                cardPlay.Card,
                cardPlay);
        }

        await LibraryCreatureCmd.ChaoDamage(
            context,
            enemies,
            SalvationDamage,
            ValueProp.Unblockable | ValueProp.Unpowered,
            Owner.Creature,
            cardPlay.Card,
            cardPlay);
    }

    public override bool CanHandleRightClickLocal(LibraryRightClickContext context) =>
        CanUseRightClick();

    public override bool CanExecuteRightClick(LibraryRightClickExecutionContext context) =>
        CanUseRightClick();

    public override async Task OnRightClick(LibraryRightClickExecutionContext context)
    {
        if (!CanUseRightClick())
        {
            return;
        }

        if (Mode == BigBirdPageMode.WatchfulEye)
        {
            await ActivateWatchfulEye(context.ChoiceContext);
        }
        else if (Mode == BigBirdPageMode.EverBurningLamp)
        {
            ActivateEverBurningLamp();
        }

        UpdateModeUiState();
    }

    public override bool ShouldTakeExtraTurn(Player player)
    {
        return player == Owner
            && Mode == BigBirdPageMode.EverBurningLamp
            && PendingExtraTurns > 0;
    }

    public override Task AfterTakingExtraTurn(Player player)
    {
        if (player != Owner || PendingExtraTurns <= 0)
        {
            return Task.CompletedTask;
        }

        PendingExtraTurns--;
        Flash();
        UpdateModeUiState();
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        PendingExtraTurns = 0;
        PendingWatchfulEyeTargetCombatId = 0;
        UpdateModeUiState();
        return Task.CompletedTask;
    }

    private bool CanUseRightClick()
    {
        return Owner?.Creature != null
            && Owner.Creature.IsAlive
            && Owner.Creature.CombatState != null
            && CombatManager.Instance.IsInProgress
            && ((Mode == BigBirdPageMode.WatchfulEye && WatchfulEyeCooldownRemaining <= 0)
                || (Mode == BigBirdPageMode.EverBurningLamp && EverBurningLampCooldownRemaining <= 0));
    }

    private async Task ActivateWatchfulEye(PlayerChoiceContext choiceContext)
    {
        Player? owner = Owner;
        Creature? ownerCreature = owner?.Creature;
        if (owner == null || owner.PlayerCombatState == null || ownerCreature?.CombatState == null)
        {
            return;
        }

        Flash();
        await PlayerCmd.SetEnergy(owner.PlayerCombatState.MaxEnergy, owner);

        IReadOnlyList<Player> livingPlayers = ownerCreature.CombatState.Players
            .Where(static player => player.Creature.IsAlive)
            .ToArray();
        Player? sealedPlayer = livingPlayers.Count > 0
            ? owner.RunState.Rng.CombatTargets.NextItem(livingPlayers)
            : null;
        if (sealedPlayer?.Creature.CombatId is { } combatId)
        {
            PendingWatchfulEyeTargetCombatId = combatId <= int.MaxValue ? (int)combatId : 0;
        }

        WatchfulEyeCooldownRemaining = WatchfulEyeCooldown;
    }

    private void ActivateEverBurningLamp()
    {
        Flash();
        PendingExtraTurns = Math.Max(PendingExtraTurns, 1);
        EverBurningLampCooldownRemaining = EverBurningLampCooldown;
    }

    protected override IReadOnlyList<CardModel> CreateModeChoiceCards()
    {
        return
        [
            Owner.RunState.CreateCard<BigBirdWatchfulEyeChoiceCard>(Owner),
            Owner.RunState.CreateCard<BigBirdEverBurningLampChoiceCard>(Owner),
            Owner.RunState.CreateCard<BigBirdSalvationChoiceCard>(Owner)
        ];
    }

    protected override void ResetStateOnFallback()
    {
        WatchfulEyeCooldownRemaining = 0;
        EverBurningLampCooldownRemaining = 0;
        PendingExtraTurns = 0;
    }

    protected override void UpdateModeUiState()
    {
        DynamicVars["Mode"].BaseValue = (int)Mode;
        DynamicVars["Remaining"].BaseValue = DisplayAmount;
        Status = ShowCounter && DisplayAmount > 0
            ? RelicStatus.Disabled
            : RelicStatus.Normal;
        InvokeDisplayAmountChanged();
    }
}

public enum BigBirdPageMode
{
    None = 0,
    WatchfulEye = 1,
    EverBurningLamp = 2,
    Salvation = 3
}
