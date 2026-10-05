using LibraryLib.Models;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.specialguests.Iori;

public abstract class IoriPowerBase : LibraryOfRuinaPowerModel
{
    protected abstract string PowerId { get; }

    protected override string LegacyPowerId => PowerId;

    public override string PackedIconPath =>
        ImageHelper.GetImagePath(
            "powers/" + PowerId.ToLowerInvariant() + ".png");

    public override string ResolvedBigIconPath => PackedIconPath;
}

public sealed class IoriProbabilityFluctuationPassivePower : IoriPowerBase
{
    protected override string PowerId =>
        "IORI_PROBABILITY_FLUCTUATION_PASSIVE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("MinimumCost", 0),
        new DynamicVar("MaximumCost", 3),
    ];

    public override Task AfterAutoPrePlayPhaseEntered(
        PlayerChoiceContext choiceContext,
        Player player)
    {
        _ = choiceContext;
        if (Owner.IsDead || player.Creature.CombatState == null)
        {
            return Task.CompletedTask;
        }

        foreach (CardModel card in PileType.Hand
                     .GetPile(player)
                     .Cards
                     .ToArray())
        {
            if (card.EnergyCost.Canonical < 0)
            {
                continue;
            }

            int cost = player.RunState.Rng.CombatEnergyCosts.NextInt(4);
            card.EnergyCost.SetThisTurnOrUntilPlayed(cost);
            NCard.FindOnTable(card)?.PlayRandomizeCostAnim();
        }

        Flash();
        return Task.CompletedTask;
    }
}

public sealed class IoriDimensionalWalkPassivePower : IoriPowerBase
{
    protected override string PowerId =>
        "IORI_DIMENSIONAL_WALK_PASSIVE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("ThresholdPercent", 50),
    ];
}

public sealed class IoriStanceShiftPassivePower : IoriPowerBase, ILibraryAbstractModel
{
    protected override string PowerId =>
        "IORI_STANCE_SHIFT_PASSIVE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    public override bool TryModifyPowerAmountReceived(
        PowerModel canonicalPower,
        Creature target,
        decimal amount,
        Creature? applier,
        out decimal modifiedAmount)
    {
        _ = applier;
        modifiedAmount = amount;
        if (target != Owner
            || Owner.Monster is not IoriMonsterBase
            {
                CurrentStance: IoriStance.Defense,
            }
            || canonicalPower.GetTypeForAmount(amount) != PowerType.Debuff)
        {
            return false;
        }

        modifiedAmount = 0m;
        return true;
    }

#if STS2_0_111_0
    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
#else
    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
#endif
    {
        _ = amount;
        _ = cardSource;
        return dealer == Owner
               && target is { IsPlayer: true }
               && Owner.Monster is IoriMonsterBase
               {
                   CurrentStance: IoriStance.Slash,
               }
               && ValuePropCompat.IsPoweredAttack(props)
            ? 1.25m
            : 1m;
    }

    /// <summary>
    /// Supplies Iori's exact type-power point through the vanilla hook, which
    /// is also the path used by intent preview.
    /// </summary>
#if STS2_0_111_0
    public override decimal ModifyDamageAdditive(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
#else
    public override decimal ModifyDamageAdditive(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
#endif
    {
        _ = target;
        _ = amount;
        _ = cardSource;
        if (dealer != Owner
            || Owner.Monster is not IoriMonsterBase iori
            || !ValuePropCompat.IsPoweredAttack(props))
        {
            return 0m;
        }

        LibraryDamageType stanceType = ResolveStanceDamageType(iori);
        int typePower = ResolveTypePowerAmount(iori);
        return stanceType != LibraryDamageType.None
               && iori.ResolveActiveOrPreviewDamageType() == stanceType
            ? typePower
            : 0m;
    }

    /// <summary>
    /// The exact point above is already present when LibraryOfRuinaLib invokes
    /// both vanilla and Ruina hooks. Cancel the displayed generic type Power's
    /// inferred contribution so the point is counted once.
    /// </summary>
    public decimal ModifyDamageAdditive(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay,
        LibraryDamageType inferredType)
    {
        _ = target;
        _ = amount;
        _ = cardSource;
        if (dealer != Owner
            || Owner.Monster is not IoriMonsterBase iori
            || !ValuePropCompat.IsPoweredAttack(props))
        {
            return 0m;
        }

        LibraryDamageType stanceType = ResolveStanceDamageType(iori);
        if (stanceType == LibraryDamageType.None)
        {
            return 0m;
        }

        int inferredContribution = inferredType == stanceType
            ? ResolveTypePowerAmount(iori)
            : 0;
        return -inferredContribution;
    }

    private static LibraryDamageType ResolveStanceDamageType(
        IoriMonsterBase iori) => iori.CurrentStance switch
    {
        IoriStance.Slash => LibraryDamageType.Slash,
        IoriStance.Pierce => LibraryDamageType.Pierce,
        IoriStance.Blunt => LibraryDamageType.Blunt,
        _ => LibraryDamageType.None,
    };

    private static int ResolveTypePowerAmount(
        IoriMonsterBase iori) => iori.CurrentStance switch
    {
        IoriStance.Slash =>
            iori.Creature.GetPower<LibraryStrongSlashPower>()?.Amount ?? 0,
        IoriStance.Pierce =>
            iori.Creature.GetPower<LibraryStrongPiercePower>()?.Amount ?? 0,
        IoriStance.Blunt =>
            iori.Creature.GetPower<LibraryStrongBluntPower>()?.Amount ?? 0,
        _ => 0,
    };

    public override decimal ModifyBlockAdditive(
        Creature target,
        decimal block,
        ValueProp props,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        _ = block;
        return target == Owner
               && Owner.Monster is IoriMonsterBase
               {
                   CurrentStance: IoriStance.Defense,
               }
               && props.HasFlag(ValueProp.Move)
               && cardSource == null
            ? 1m
            : 0m;
    }
}

/// <summary>
/// Marker state for one active stance.  Each stance carries its own icon and
/// description so the four stance effects are never aggregated into a single
/// power; the stance-shift passive only schedules the switching.
/// </summary>
public abstract class IoriStancePowerBase : IoriPowerBase
{
    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;
}

public sealed class IoriSlashStancePower : IoriStancePowerBase
{
    protected override string PowerId => "IORI_SLASH_STANCE_POWER";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Strong", 2),
        new DynamicVar("TypePower", 1),
        new DynamicVar("SlashDamagePercent", 25),
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<LibraryStrongPower>(),
        HoverTipFactory.FromPower<LibraryStrongSlashPower>(),
    ];
}

public sealed class IoriPierceStancePower : IoriStancePowerBase
{
    protected override string PowerId => "IORI_PIERCE_STANCE_POWER";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Strong", 2),
        new DynamicVar("TypePower", 1),
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new[]
        {
            HoverTipFactory.FromPower<LibraryStrongPower>(),
            HoverTipFactory.FromPower<LibraryStrongPiercePower>(),
        }
        .Concat(
            HoverTipFactory
                .FromPowerWithPowerHoverTips<PainfulStabsPower>());
}

public sealed class IoriBluntStancePower : IoriStancePowerBase
{
    protected override string PowerId => "IORI_BLUNT_STANCE_POWER";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Strong", 2),
        new DynamicVar("TypePower", 1),
        new DynamicVar("Chains", 2),
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new[]
        {
            HoverTipFactory.FromPower<LibraryStrongPower>(),
            HoverTipFactory.FromPower<LibraryStrongBluntPower>(),
        }
        .Concat(
            HoverTipFactory
                .FromPowerWithPowerHoverTips<ChainsOfBindingPower>());
}

public sealed class IoriDefenseStancePower : IoriStancePowerBase
{
    private sealed class DefenseThornsVar()
        : DynamicVar("DefenseThorns", 7m)
    {
        protected override decimal GetBaseValueForIConvertible() =>
            new IoriAscensionValue(7, 9).Resolve();

        public override string ToString() =>
            GetBaseValueForIConvertible().ToString();
    }

    protected override string PowerId => "IORI_DEFENSE_STANCE_POWER";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("TypePower", 1),
        new DynamicVar("Endurance", 2),
        new DefenseThornsVar(),
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<LibraryDefensePowerUpPower>(),
        HoverTipFactory.FromPower<LibraryEndurancePower>(),
        HoverTipFactory.FromPower<ThornsPower>(),
    ];
}

public sealed class IoriCardPlayPainPower : LibraryOfRuinaPowerModel
{
    private sealed class DamageVar() : DynamicVar("Damage", 0m)
    {
        protected override decimal GetBaseValueForIConvertible() =>
            _owner is IoriCardPlayPainPower power
                ? power.Amount
                : base.GetBaseValueForIConvertible();

        public override string ToString() =>
            GetBaseValueForIConvertible().ToString();
    }

    protected override string LegacyPowerId => "IORI_CARD_PLAY_PAIN_POWER";

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override string PackedIconPath =>
        ImageHelper.GetImagePath("powers/iori_card_play_pain_power.png");

    public override string ResolvedBigIconPath => PackedIconPath;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(),
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.Static(StaticHoverTip.Block),
    ];

    public override async Task AfterCardPlayed(
        PlayerChoiceContext context,
        CardPlay cardPlay)
    {
        if (Amount <= 0
            || Owner.IsDead
            || cardPlay.Card.Owner.Creature != Owner)
        {
            return;
        }

        Flash();
        await CreatureCmdCompat.Damage(
            context,
            Owner,
            Amount,
            ValueProp.Unpowered,
            Applier ?? Owner,
            null);
    }
}

internal static class IoriPassiveInstaller
{
    internal static async Task EnsureFor(IoriMonsterBase iori)
    {
        Creature owner = iori.Creature;
        await PowerCmdCompat.Ensure<IoriProbabilityFluctuationPassivePower>(
            owner,
            1,
            owner,
            null,
            silent: true);
        await PowerCmdCompat.Ensure<IoriDimensionalWalkPassivePower>(
            owner,
            1,
            owner,
            null,
            silent: true);
        await PowerCmdCompat.Ensure<IoriStanceShiftPassivePower>(
            owner,
            1,
            owner,
            null,
            silent: true);
    }
}
