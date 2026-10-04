using LibraryLib.Models;
using System;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.liberation.Art;

public sealed class ArtFloorAtonementCrownPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "ART_FLOOR_ATONEMENT_CROWN_POWER";

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;
}

public sealed class ArtFloorPetalPower : LibraryOfRuinaPowerModel
{
    public const int Threshold = 3;

    protected override string LegacyPowerId => "ART_FLOOR_PETAL_POWER";

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Threshold", Threshold)
    ];
}

public sealed class ArtFloorSuffocatingAtonementPower : LibraryPowerModel, ILibraryAbstractModel
{
    protected override string? LegacyPowerId => "ART_FLOOR_SUFFOCATING_ATONEMENT_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    public override decimal ModifyHpLostAfterOstyLate(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        return ShouldBlockNonCrownDamage(target, amount, dealer, cardSource) ? 0m : amount;
    }

    public decimal ModifyHpLostAfterOstyLate(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        LibraryDamageType type)
    {
        return ShouldBlockNonCrownDamage(target, amount, dealer, cardSource) ? 0m : amount;
    }

    private bool ShouldBlockNonCrownDamage(
        Creature target,
        decimal amount,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (target != Owner
            || Owner.Monster is not ArtFloorNostalgicScentBoss
            || amount <= 0m)
        {
            return false;
        }

        Creature? playerSource = ResolvePlayerDamageSource(dealer, cardSource);
        return playerSource is { IsPlayer: true }
            && !playerSource.HasPower<ArtFloorAtonementCrownPower>();
    }

    private static Creature? ResolvePlayerDamageSource(Creature? dealer, CardModel? cardSource)
    {
        if (dealer is { IsPlayer: true })
        {
            return dealer;
        }

        if (cardSource?.IsMutable == true)
        {
            return cardSource.Owner?.Creature;
        }

        return null;
    }
}

public sealed class ArtFloorUnfadingFlowerPower : LibraryPowerModel
{
    private const int FirstTriggerRound = 2;
    private const int PetalsPerTurn = 1;

    protected override string? LegacyPowerId => "ART_FLOOR_UNFADING_FLOWER_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("FirstTurn", FirstTriggerRound),
        new DynamicVar("Petals", PetalsPerTurn),
        new DynamicVar("Threshold", ArtFloorPetalPower.Threshold)
    ];

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (side != CombatSide.Enemy
            || Owner.IsDead
            || Owner.Monster is not ArtFloorNostalgicScentBoss boss
            || combatState.RoundNumber < FirstTriggerRound)
        {
            return;
        }

        Flash();
        ArtFloorPetalPower? petal = await PowerCmdCompat.Apply<ArtFloorPetalPower>(
            choiceContext,
            Owner,
            PetalsPerTurn,
            Owner,
            null,
            silent: true);
        petal ??= Owner.GetPower<ArtFloorPetalPower>();
        if (petal == null || petal.Amount < ArtFloorPetalPower.Threshold)
        {
            return;
        }

        await PowerCmdCompat.ModifyAmount(choiceContext, petal, -petal.Amount, Owner, null, silent: true);
        await boss.QueuePetalEgo();
    }
}

public sealed class ArtFloorFragrancePower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "ART_FLOOR_FRAGRANCE_POWER";

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("StackDivisor", 2)
    ];

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (!TurnParticipants.IsOwnTurn(Owner, side, participants) || Owner.IsDead || Amount <= 0)
        {
            return;
        }

        Flash();
        await CreatureCmd.LoseMaxHp(choiceContext, Owner, Amount, isFromCard: false);
        int nextAmount = Math.Max(0, Amount / 2);
        if (nextAmount <= 0)
        {
            await PowerCmd.Remove(this);
        }
        else
        {
            await PowerCmdCompat.ModifyAmount(choiceContext, this, nextAmount - Amount, Owner, null, silent: true);
        }
    }
}

public sealed class ArtFloorCollapsePower : LibraryPowerModel, ILibraryAbstractModel
{
    protected override string? LegacyPowerId => "ART_FLOOR_COLLAPSE_POWER";

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("DamageMultiplier", 2)
    ];

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        SkipNextDurationTick = false;
        return Task.CompletedTask;
    }

    public override decimal ModifyHpLostAfterOstyLate(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        return target == Owner && amount > 0m ? amount * 2m : amount;
    }

    public decimal ModifyHpLostAfterOstyLate(
        Creature target,
        decimal num,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        LibraryDamageType type)
    {
        return target == Owner && num > 0m ? num * 2m : num;
    }

    public override Task AfterModifyingHpLostAfterOsty()
    {
        Flash();
        return Task.CompletedTask;
    }

    public Task AfterModifyingHpLostAfterOsty(LibraryDamageType type)
    {
        Flash();
        return Task.CompletedTask;
    }

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (TurnParticipants.IsOwnTurn(Owner, side, participants))
        {
            await PowerCmd.TickDownDuration(this);
        }
    }
}

public sealed class ArtFloorNextTurnCollapsePower : LibraryPowerModel
{
    protected override string? LegacyPowerId => "ART_FLOOR_NEXT_TURN_COLLAPSE_POWER";

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (!TurnParticipants.IsPlayerTurnFor(Owner, side, participants) || Owner.IsDead || Amount <= 0)
        {
            return;
        }

        decimal collapseAmount = Amount;
        Creature? applier = Applier;
        Flash();
        await PowerCmd.Remove(this);
        await PowerCmdCompat.Apply<ArtFloorCollapsePower>(
            choiceContext,
            Owner,
            collapseAmount,
            applier,
            null);
    }
}

public sealed class ArtFloorClayDollPower : LibraryPowerModel, ILibraryAbstractModel
{
    protected override string? LegacyPowerId => "ART_FLOOR_CLAY_DOLL_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    public override bool ShouldOwnerDeathTriggerFatal() => false;

    public override decimal ModifyHpLostAfterOstyLate(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        return ShouldBlockDebuffLifeLoss(target, amount, props, dealer, cardSource) ? 0m : amount;
    }

    public decimal ModifyHpLostAfterOstyLate(
        Creature target,
        decimal num,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        LibraryDamageType type)
    {
        return ShouldBlockDebuffLifeLoss(target, num, props, dealer, cardSource) ? 0m : num;
    }

    private bool ShouldBlockDebuffLifeLoss(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        return target == Owner
            && amount > 0m
            && !ValuePropCompat.IsCardOrMonsterMove(props)
            && cardSource == null
            && (dealer == null || dealer == Owner);
    }
}

public sealed class ArtFloorDustToDustPower : LibraryPowerModel, ILibraryAbstractModel
{
    protected override string? LegacyPowerId => "ART_FLOOR_DUST_TO_DUST_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override bool IsVisibleInternal => false;

    public Task AfterCurrentChaoValueChanged(Creature target, decimal amount, LibraryDamageType type)
    {
        if (target != Owner || Owner.IsDead || Owner is not LibraryCreature lc || lc.CurrentChaoValue > 0)
        {
            return Task.CompletedTask;
        }

        Flash();
        return CreatureCmd.Kill(Owner, force: true);
    }

    public Task AfterStun(Creature creature)
    {
        if (creature != Owner || Owner.IsDead)
        {
            return Task.CompletedTask;
        }

        Flash();
        return CreatureCmd.Kill(Owner, force: true);
    }
}

public sealed class ArtFloorDustbornWinterStasisPower : LibraryPowerModel, LibraryOfRuina.infra.helpers.IFinalHpLossClamp
{
    protected override string? LegacyPowerId => "ART_FLOOR_DUSTBORN_WINTER_STASIS_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override bool IsVisibleInternal => false;

    public decimal ClampFinalHpLoss(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        return target == Owner && amount > 0m ? 0m : amount;
    }

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Enemy)
        {
            await PowerCmd.Remove(this);
        }
    }
}
