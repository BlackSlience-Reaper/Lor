using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using LibraryLib.Light;
using LibraryOfRuina.cards.SocialFloorLiberation;
using LibraryOfRuina.encounters.SocialFloorLiberation;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace LibraryOfRuina.powers.SocialFloorLiberation;

public abstract class SocialFloorPowerModel : LibraryOfRuinaPowerModel
{
    protected abstract string IconFileName { get; }

    public override string PackedIconPath =>
        ImageHelper.GetImagePath("powers/" + IconFileName);

    public override string ResolvedBigIconPath => PackedIconPath;
}

public sealed class SocialFloorCouragePower : SocialFloorPowerModel
{
    public const int EnergyMaximum = 5;
    public const int StrongStacks = 10;
    public const int TotalActivations = 2;

    private static readonly AsyncLocal<ulong?> ExactEnergyResetPlayer = new();

    protected override string LegacyPowerId => "SOCIAL_FLOOR_COURAGE_POWER";

    protected override string IconFileName => "social_floor_courage_power.png";

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    [SavedProperty]
    public string SerializedHolderNetId { get; private set; } = "0";

    public ulong HolderNetId =>
        SocialFloorPlayerMechanics.ParseHolderNetId(SerializedHolderNetId);

    [SavedProperty]
    public int PendingTurnStartActivations { get; private set; }

    [SavedProperty]
    public bool IsEnergyOverrideActive { get; private set; }

    [SavedProperty]
    public bool RemoveAtNextPlayerTurnEnd { get; private set; }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new EnergyVar("EnergyMaximum", EnergyMaximum),
        new PowerVar<LibraryStrongPower>("Strong", StrongStacks),
        new DynamicVar("Activations", TotalActivations)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<LibraryWeakPower>(),
        HoverTipFactory.FromPower<LibraryDisarmPower>(),
        HoverTipFactory.FromPower<LibraryStrongPower>()
    ];

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        BindHolder(Owner.Player);
        return Task.CompletedTask;
    }

    public void BindHolder(Player? player)
    {
        if (player != null)
        {
            SerializedHolderNetId =
                SocialFloorPlayerMechanics.FormatHolderNetId(player.NetId);
        }
    }

    public void RestoreState(
        Player player,
        int pendingTurnStartActivations,
        bool isEnergyOverrideActive,
        bool removeAtNextPlayerTurnEnd)
    {
        ArgumentNullException.ThrowIfNull(player);
        BindHolder(player);
        PendingTurnStartActivations = Math.Clamp(
            pendingTurnStartActivations,
            0,
            TotalActivations);
        IsEnergyOverrideActive = isEnergyOverrideActive;
        RemoveAtNextPlayerTurnEnd =
            isEnergyOverrideActive && removeAtNextPlayerTurnEnd;
    }

    public async Task ActivateImmediately(
        PlayerChoiceContext choiceContext,
        CardModel? cardSource)
    {
        Player? player = Owner.Player;
        if (player == null || Owner.IsDead)
        {
            return;
        }

        BindHolder(player);
        PendingTurnStartActivations = TotalActivations - 1;
        RemoveAtNextPlayerTurnEnd = false;
        IsEnergyOverrideActive = true;
        await Activate(choiceContext, player, cardSource);
    }

    internal Task RestoreActiveEffects()
    {
        if (!IsEnergyOverrideActive
            || Owner.IsDead
            || Owner.Player is not { } player
            || player.NetId != HolderNetId)
        {
            return Task.CompletedTask;
        }

        return Activate(new ThrowingPlayerChoiceContext(), player, null);
    }

    public override decimal ModifyMaxEnergy(Player player, decimal amount) =>
        IsActiveFor(player) ? EnergyMaximum : amount;

    public override bool TryModifyPowerAmountReceived(
        PowerModel canonicalPower,
        Creature target,
        decimal amount,
        Creature? applier,
        out decimal modifiedAmount)
    {
        modifiedAmount = amount;
        if (!IsEnergyOverrideActive
            || target != Owner
            || amount <= 0m
            || canonicalPower is not (LibraryWeakPower or LibraryDisarmPower))
        {
            return false;
        }

        modifiedAmount = 0m;
        return true;
    }

    public override async Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (side != CombatSide.Player
            || PendingTurnStartActivations <= 0
            || Owner.IsDead
            || Owner.Player is not { } player
            || player.NetId != HolderNetId)
        {
            return;
        }

        PendingTurnStartActivations--;
        IsEnergyOverrideActive = true;
        RemoveAtNextPlayerTurnEnd = PendingTurnStartActivations == 0;
        await Activate(new ThrowingPlayerChoiceContext(), player, null);
    }

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Player || !RemoveAtNextPlayerTurnEnd)
        {
            return;
        }

        IsEnergyOverrideActive = false;
        RemoveAtNextPlayerTurnEnd = false;
        await PowerCmd.Remove(this);
    }

    public override Task AfterRemoved(Creature oldOwner)
    {
        IsEnergyOverrideActive = false;
        PendingTurnStartActivations = 0;
        RemoveAtNextPlayerTurnEnd = false;
        return Task.CompletedTask;
    }

    internal static bool IsExactEnergyResetActive(Player player) =>
        ExactEnergyResetPlayer.Value == player.NetId;

    private bool IsActiveFor(Player player) =>
        IsEnergyOverrideActive
        && player.NetId == HolderNetId
        && player.Creature == Owner
        && Owner.IsAlive;

    private async Task Activate(
        PlayerChoiceContext choiceContext,
        Player player,
        CardModel? cardSource)
    {
        if (player.PlayerCombatState is not { } playerCombatState)
        {
            return;
        }

        Flash();
        await RemoveAll<LibraryWeakPower>();
        await RemoveAll<LibraryDisarmPower>();

        using (BeginExactEnergyReset(player))
        {
            await PlayerCmd.SetEnergy(
                Math.Max(playerCombatState.Energy, playerCombatState.MaxEnergy),
                player);
        }

        if (LibraryLight.TryGetState(player, out LibraryLightState? light)
            && light != null)
        {
            await light.Reset(this);
        }

        await LibraryPowerCmd.Apply<LibraryStrongPower>(
            choiceContext,
            Owner,
            StrongStacks,
            turns: 0,
            IsPermanent: false,
            Owner,
            cardSource);
    }

    private async Task RemoveAll<TPower>()
        where TPower : PowerModel
    {
        foreach (TPower power in Owner.GetPowerInstances<TPower>().ToArray())
        {
            await PowerCmd.Remove(power);
        }
    }

    private static IDisposable BeginExactEnergyReset(Player player)
    {
        ulong? previous = ExactEnergyResetPlayer.Value;
        ExactEnergyResetPlayer.Value = player.NetId;
        return new EnergyResetScope(previous);
    }

    private sealed class EnergyResetScope(ulong? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            ExactEnergyResetPlayer.Value = previous;
        }
    }
}

public sealed class SocialFloorScaredyCatPower : SocialFloorPowerModel
{
    public const int CardLimit = 5;

    protected override string LegacyPowerId => "SOCIAL_FLOOR_SCAREDY_CAT_POWER";

    protected override string IconFileName => "social_floor_scaredy_cat_power.png";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    [SavedProperty]
    public string SerializedHolderNetId { get; private set; } = "0";

    public ulong HolderNetId =>
        SocialFloorPlayerMechanics.ParseHolderNetId(SerializedHolderNetId);

    [SavedProperty]
    public int CardsSubmittedThisTurn { get; private set; }

    [SavedProperty]
    public bool CourageCardGranted { get; private set; }

    [SavedProperty]
    public bool IsHolderActive { get; private set; } = true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("CardLimit", CardLimit)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<SocialFloorCourageCard>()
    ];

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        BindHolder(Owner.Player);
        return Task.CompletedTask;
    }

    public void BindHolder(Player? player)
    {
        if (player == null)
        {
            return;
        }

        SerializedHolderNetId =
            SocialFloorPlayerMechanics.FormatHolderNetId(player.NetId);
        IsHolderActive = true;
    }

    public void MarkCourageCardGranted()
    {
        CourageCardGranted = true;
    }

    public void InvalidateHolder()
    {
        IsHolderActive = false;
    }

    public void RestoreState(
        Player player,
        int cardsSubmittedThisTurn,
        bool courageCardGranted,
        bool isHolderActive)
    {
        ArgumentNullException.ThrowIfNull(player);
        BindHolder(player);
        CardsSubmittedThisTurn = Math.Clamp(
            cardsSubmittedThisTurn,
            0,
            CardLimit);
        CourageCardGranted = courageCardGranted;
        IsHolderActive = isHolderActive;
    }

    public override bool ShouldPlay(CardModel card, AutoPlayType autoPlayType)
    {
        if (!IsHolderActive
            || Owner.IsDead
            || Owner.CombatState?.CurrentSide != CombatSide.Player
            || card.Owner.NetId != HolderNetId)
        {
            return true;
        }

        return CardsSubmittedThisTurn < CardLimit;
    }

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (IsHolderActive
            && cardPlay.IsFirstInSeries
            && cardPlay.Player.NetId == HolderNetId)
        {
            CardsSubmittedThisTurn = Math.Min(
                CardLimit,
                CardsSubmittedThisTurn + 1);
            if (cardPlay.Player.Creature.CombatState?.Encounter is
                SocialFloorLiberationEncounter encounter)
            {
                encounter.MarkLionCardsSubmitted(
                    cardPlay.Player.NetId,
                    CardsSubmittedThisTurn);
            }
        }

        return Task.CompletedTask;
    }

    public override Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (side == CombatSide.Player)
        {
            CardsSubmittedThisTurn = 0;
            if (Owner?.Player is { } player
                && player.Creature.CombatState?.Encounter is
                    SocialFloorLiberationEncounter encounter)
            {
                encounter.MarkLionCardsSubmitted(player.NetId, 0);
            }
        }

        return Task.CompletedTask;
    }
}

public sealed class SocialFloorCowardPower : SocialFloorPowerModel
{
    public const int DebuffStacks = 9;

    protected override string LegacyPowerId => "SOCIAL_FLOOR_COWARD_POWER";

    protected override string IconFileName => "social_floor_coward_power.png";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    [SavedProperty]
    public string SerializedHolderNetId { get; private set; } = "0";

    public ulong HolderNetId =>
        SocialFloorPlayerMechanics.ParseHolderNetId(SerializedHolderNetId);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Stacks", DebuffStacks)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<StrengthPower>(),
        HoverTipFactory.FromPower<LibraryDisarmPower>(),
        HoverTipFactory.FromPower<LibraryVulnerablePower>()
    ];

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        BindHolder(Owner.Player);
        return Task.CompletedTask;
    }

    public void BindHolder(Player? player)
    {
        if (player != null)
        {
            SerializedHolderNetId =
                SocialFloorPlayerMechanics.FormatHolderNetId(player.NetId);
        }
    }

    public override async Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (side != CombatSide.Player
            || Owner.IsDead
            || Owner.Player is not { } player
            || player.NetId != HolderNetId)
        {
            return;
        }

        Flash();
        PlayerChoiceContext context = new ThrowingPlayerChoiceContext();
        await LibraryPowerCmd.Apply<LibraryWeakPower>(
            context,
            Owner,
            DebuffStacks,
            turns: 0,
            IsPermanent: false,
            Owner,
            null);
        await LibraryPowerCmd.Apply<LibraryDisarmPower>(
            context,
            Owner,
            DebuffStacks,
            turns: 0,
            IsPermanent: false,
            Owner,
            null);
        await LibraryPowerCmd.Apply<LibraryVulnerablePower>(
            context,
            Owner,
            DebuffStacks,
            turns: 0,
            IsPermanent: false,
            Owner,
            null);
    }
}

public sealed class SocialFloorOzmaPower : SocialFloorPowerModel
{
    public const int AttackOrPowerCostReduction = 1;
    public const int SkillCostIncrease = 1;

    protected override string LegacyPowerId => "SOCIAL_FLOOR_OZMA_POWER";

    protected override string IconFileName => "social_floor_ozma_power.png";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    [SavedProperty]
    public string SerializedHolderNetId { get; private set; } = "0";

    public ulong HolderNetId =>
        SocialFloorPlayerMechanics.ParseHolderNetId(SerializedHolderNetId);

    [SavedProperty]
    public bool IsHolderActive { get; private set; } = true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new EnergyVar(
            "AttackOrPowerCostReduction",
            AttackOrPowerCostReduction),
        new EnergyVar("SkillCostIncrease", SkillCostIncrease),
        new EnergyVar(
            "InitialCost",
            SocialFloorMagicalPowderCard.DefaultInternalCost)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<SocialFloorMagicalPowderCard>()
    ];

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        BindHolder(Owner.Player);
        return Task.CompletedTask;
    }

    public void BindHolder(Player? player)
    {
        if (player == null)
        {
            return;
        }

        SerializedHolderNetId =
            SocialFloorPlayerMechanics.FormatHolderNetId(player.NetId);
        IsHolderActive = true;
    }

    public void InvalidateHolder()
    {
        IsHolderActive = false;
    }

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (!IsHolderActive
            || !cardPlay.IsFirstInSeries
            || cardPlay.Player.NetId != HolderNetId
            || cardPlay.Card is SocialFloorMagicalPowderCard)
        {
            return Task.CompletedTask;
        }

        int adjustment = cardPlay.Card.Type switch
        {
            CardType.Attack or CardType.Power =>
                -AttackOrPowerCostReduction,
            CardType.Skill => SkillCostIncrease,
            _ => 0
        };
        if (adjustment != 0
            && SocialFloorPlayerMechanics.TryGetActivePowder(
                cardPlay.Player,
                out SocialFloorMagicalPowderCard? powder))
        {
            powder!.AdjustInternalCost(adjustment);
            if (cardPlay.Player.Creature.CombatState?.Encounter is
                SocialFloorLiberationEncounter encounter)
            {
                encounter.MarkPowderCost(powder.InternalCost);
            }
        }

        return Task.CompletedTask;
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyMaxEnergy))]
[HarmonyAfter("LibraryOfRuinaLib")]
[HarmonyPriority(Priority.Last)]
internal static class SocialFloorCourageMaxEnergyPatch
{
    private static void Postfix(Player player, ref decimal __result)
    {
        if (player.Creature.GetPower<SocialFloorCouragePower>() is
            { IsEnergyOverrideActive: true } courage
            && courage.HolderNetId == player.NetId)
        {
            __result = SocialFloorCouragePower.EnergyMaximum;
        }
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyEnergyGain))]
[HarmonyAfter("LibraryOfRuinaLib")]
[HarmonyPriority(Priority.Last)]
internal static class SocialFloorCourageEnergyResetPatch
{
    private static void Postfix(
        Player player,
        decimal originalAmount,
        ref decimal __result)
    {
        if (SocialFloorCouragePower.IsExactEnergyResetActive(player))
        {
            __result = originalAmount;
        }
    }
}
