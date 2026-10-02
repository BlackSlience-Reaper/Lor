using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.powers;
using LibraryLib.Entities.Creatures;
using LibraryLib.Utils.Resistance;
using System;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.liberation.Natural;

public sealed class NaturalFloorEverythingIsEmptyPower : NaturalFloorGreenPassivePower
{
    protected override string LegacyPowerId => "NATURAL_FLOOR_EVERYTHING_IS_EMPTY_POWER";
}

public sealed class NaturalFloorNihilImmunityPower : NaturalFloorGreenPassivePower
{
    // 无谓之举：所有形态下被施加的灾厄层数降低百分比。
    private const int DoomStackReductionPercent = 50;

    // 无谓之举：非魔法少女来源的单次伤害上限。
    private const int NonMagicalGirlDamageCap = 1;

    // 无谓之举：非魔法少女来源的单次混乱伤害上限，包含所有形态。
    private const int NonMagicalGirlChaoDamageCap = 1;

    protected override string LegacyPowerId => "NATURAL_FLOOR_NIHIL_IMMUNITY_POWER";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("DoomReduction", DoomStackReductionPercent),
        new DynamicVar("DamageCap", NonMagicalGirlDamageCap),
        new DynamicVar("ChaoDamageCap", NonMagicalGirlChaoDamageCap)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [HoverTipFactory.FromPower<DoomPower>()];

#if STS2_0_111_0
    public override decimal ModifyDamageCap(
        Creature? target, ValueProp props, Creature? dealer,
        CardModel? cardSource, CardPlay? cardPlay)
#else
    public override decimal ModifyDamageCap(
        Creature? target, ValueProp props, Creature? dealer,
        CardModel? cardSource)
#endif
    =>
        IsNonMagicalGirlDamage(target, dealer) ? NonMagicalGirlDamageCap : decimal.MaxValue;

    public override decimal ModifyHpLostAfterOstyLate(
        Creature target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource)
    {
        // 抗性在伤害上限之后结算；保留正数命中的一点生命损失，完全格挡仍为零。
        return IsNonMagicalGirlDamage(target, dealer) && amount > 0m
            ? NonMagicalGirlDamageCap
            : amount;
    }

    public override decimal ModifyChaoDamageCap(
        Creature? target, ValueProp props, Creature? dealer,
        CardModel? cardSource, CardPlay? cardPlay, LibraryDamageType type)
    {
        if (target != Owner)
        {
            return decimal.MaxValue;
        }

        if (dealer?.Monster is not NaturalFloorMagicalGirl)
        {
            // 混乱抗性在上限之后结算，反算输入上限，使最终混乱伤害至多为一点。
            decimal multiplier = LibraryDamageCalculate.CalculateChaoAmount(
                1m, target as LibraryCreature, props, type);
            return multiplier > 0m ? NonMagicalGirlChaoDamageCap / multiplier : 0m;
        }

        if (Owner.Monster is NaturalFloorNihilBoss { Form: not NaturalFloorNihilForm.Wrath })
        {
            return 0m;
        }

        return decimal.MaxValue;
    }

    private bool IsNonMagicalGirlDamage(Creature? target, Creature? dealer) =>
        target == Owner && dealer?.Monster is not NaturalFloorMagicalGirl;

    public override bool TryModifyPowerAmountReceived(PowerModel canonicalPower, Creature target,
        decimal amount, Creature? applier, out decimal modifiedAmount)
    {
        modifiedAmount = amount;
        if (target == Owner && amount > 0 && canonicalPower is LibraryBurnPower or LibraryBleedingPower)
        {
            modifiedAmount = 0m;
            return true;
        }

        if (target == Owner && amount > 0 && canonicalPower is DoomPower)
        {
            modifiedAmount = amount * (1m - DoomStackReductionPercent / 100m);
            return true;
        }

        return false;
    }
}

public abstract class NaturalFloorNihilFormPower : NaturalFloorGreenPassivePower
{
    internal abstract NaturalFloorNihilForm Form { get; }

    private int DamageReductionPercent => NaturalFloorNihilMoves.DamageReduction(Form);

    private int FormHpLossPercent => NaturalFloorNihilMoves.TransitionHpLossPercent(Form);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Reduction", DamageReductionPercent),
        new DynamicVar("LossPercent", FormHpLossPercent),
        new DynamicVar("Interval", NaturalFloorNihilMoves.WrathGroupInterval),
        new DynamicVar("Heal", NaturalFloorNihilMoves.TyrantHealPercent),
        new DynamicVar("Bleed", NaturalFloorNihilMoves.GreedHitBleed),
        new DynamicVar("Block", NaturalFloorNihilMoves.GreedBlockedGirlBlock),
        new DynamicVar("Shard", NaturalFloorNihilMoves.ShardCount),
        new DynamicVar("Swords", NaturalFloorNihilMoves.SealedSwordCount),
        new DynamicVar("HitPlayers", NaturalFloorNihilMoves.TyrantPlayerHitThreshold)
    ];

#if STS2_0_111_0
    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount,
        ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
#else
    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount,
        ValueProp props, Creature? dealer, CardModel? cardSource)
#endif
    {
        return GetIncomingDamageMultiplier(target, dealer);
    }

    public override decimal ModifyChaoDamageMultiplicative(Creature? target, decimal amount,
        ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay, LibraryDamageType type)
    {
        return GetIncomingDamageMultiplier(target, dealer);
    }

    private decimal GetIncomingDamageMultiplier(Creature? target, Creature? dealer)
    {
        if (target != Owner
            || Owner.Monster is not NaturalFloorNihilBoss boss || boss.Form != Form)
        {
            return 1m;
        }

        if (dealer?.Monster is not NaturalFloorMagicalGirl girl || girl.Kind == boss.VulnerableGirl)
        {
            return 1m;
        }

        return 1m - DamageReductionPercent / 100m;
    }
}

public sealed class NaturalFloorNihilGreedPower : NaturalFloorNihilFormPower
{
    protected override string LegacyPowerId => "NATURAL_FLOOR_NIHIL_GREED_POWER";

    internal override NaturalFloorNihilForm Form => NaturalFloorNihilForm.Greed;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [HoverTipFactory.FromCard<NaturalFloorNihilHappinessShard>(), HoverTipFactory.FromPower<LibraryBleedingPower>()];
}

public sealed class NaturalFloorNihilHatredPower : NaturalFloorNihilFormPower
{
    protected override string LegacyPowerId => "NATURAL_FLOOR_NIHIL_HATRED_POWER";

    internal override NaturalFloorNihilForm Form => NaturalFloorNihilForm.Hatred;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        base.CanonicalVars.Append(new HatredThresholdVar());

    internal void RefreshCounter() => InvokeDisplayAmountChanged();

    public override int DisplayAmount
    {
        get
        {
            // The Owner getter asserts mutability, so Owner?.X alone still throws on canonical models;
            // mutable instances have no Owner until applied.
            if (IsMutable && Owner?.Monster is NaturalFloorNihilBoss boss)
            {
                return Math.Min(boss.HatredHitCount, boss.HatredThreshold);
            }

            return 0;
        }
    }

    public override PowerStackType StackType => PowerStackType.Counter;

    private sealed class HatredThresholdVar() : DynamicVar(
        "Threshold", NaturalFloorNihilMoves.HatredHitThreshold(playerCount: 1))
    {
        protected override decimal GetBaseValueForIConvertible()
        {
            if (_owner is NaturalFloorNihilHatredPower { IsMutable: true } power
                && power.Owner?.Monster is NaturalFloorNihilBoss boss)
            {
                return boss.HatredThreshold;
            }

            return BaseValue;
        }

        public override string ToString() =>
            GetBaseValueForIConvertible().ToString();
    }
}

public sealed class NaturalFloorNihilDespairPower : NaturalFloorNihilFormPower
{
    protected override string LegacyPowerId => "NATURAL_FLOOR_NIHIL_DESPAIR_POWER";

    internal override NaturalFloorNihilForm Form => NaturalFloorNihilForm.Despair;
}

public sealed class NaturalFloorNihilWrathPower : NaturalFloorNihilFormPower
{
    protected override string LegacyPowerId => "NATURAL_FLOOR_NIHIL_WRATH_POWER";

    internal override NaturalFloorNihilForm Form => NaturalFloorNihilForm.Wrath;
}

public sealed class NaturalFloorNihilPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "NATURAL_FLOOR_NIHIL_POWER";

    public override string PackedIconPath => NaturalFloorAssets.NaturalFloorNihilIcon;

    public override string ResolvedBigIconPath => PackedIconPath;

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Stacks", NaturalFloorNihilMoves.NihilDebuffStacks)];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [HoverTipFactory.FromPower<LibraryWeakPower>(), HoverTipFactory.FromPower<LibraryVulnerablePower>()];

    public override async Task BeforeSideTurnStart(PlayerChoiceContext context, CombatSide side,
        IReadOnlyList<Creature> participants, CombatStateLike combatState)
    {
        if (TurnParticipants.IsPlayerTurnFor(Owner, side, participants) && Owner.IsAlive)
        {
            await LibraryPowerCmd.Apply<LibraryWeakPower>(Owner, NaturalFloorNihilMoves.NihilDebuffStacks, NaturalFloorNihilMoves.NihilDebuffTurns - 1, Owner, null);
            await LibraryPowerCmd.Apply<LibraryVulnerablePower>(Owner, NaturalFloorNihilMoves.NihilDebuffStacks, NaturalFloorNihilMoves.NihilDebuffTurns - 1, Owner, null);
        }
    }
}

public sealed class NaturalFloorNihilHatredStatus : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "NATURAL_FLOOR_NIHIL_HATRED_STATUS";

    public override string PackedIconPath => NaturalFloorAssets.NihilHatredIcon;

    public override string ResolvedBigIconPath => PackedIconPath;

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Stacks", NaturalFloorNihilMoves.HatredVulnerable), new DynamicVar("Turns", NaturalFloorNihilMoves.HatredVulnerableTurns)];

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<LibraryVulnerablePower>()];

    public override async Task BeforeSideTurnStart(PlayerChoiceContext context, CombatSide side,
        IReadOnlyList<Creature> participants, CombatStateLike combatState)
    {
        if (TurnParticipants.IsPlayerTurnFor(Owner, side, participants) && Owner.IsAlive && Amount > 0)
        {
            await LibraryPowerCmd.Apply<LibraryVulnerablePower>(Owner, NaturalFloorNihilMoves.HatredVulnerable, NaturalFloorNihilMoves.HatredVulnerableTurns - 1, Applier, null);
            await PowerCmd.ModifyAmount(context, this, -1, Owner, null);
        }
    }
}

public sealed class NaturalFloorLovePower : NaturalFloorGreenPassivePower
{
    protected override string LegacyPowerId => "NATURAL_FLOOR_LOVE_POWER";

    public override PowerStackType StackType => PowerStackType.Counter;

    internal void RefreshCounter() => InvokeDisplayAmountChanged();

    public override int DisplayAmount =>
        Math.Min(((IsMutable ? Owner?.Monster : null) as NaturalFloorMagicalGirl)?.LoveHitCount ?? 0,
            NaturalFloorNihilMoves.LoveHitThreshold);

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Threshold", NaturalFloorNihilMoves.LoveHitThreshold)];
}

public sealed class NaturalFloorFrozenHatredPower : NaturalFloorGreenPassivePower
{
    protected override string LegacyPowerId => "NATURAL_FLOOR_FROZEN_HATRED_POWER";
}

public sealed class NaturalFloorFrozenDespairPower : NaturalFloorGreenPassivePower
{
    protected override string LegacyPowerId => "NATURAL_FLOOR_FROZEN_DESPAIR_POWER";
}

public sealed class NaturalFloorFrozenGreedPower : NaturalFloorGreenPassivePower
{
    protected override string LegacyPowerId => "NATURAL_FLOOR_FROZEN_GREED_POWER";
}

public sealed class NaturalFloorFrozenWrathPower : NaturalFloorGreenPassivePower
{
    protected override string LegacyPowerId => "NATURAL_FLOOR_FROZEN_WRATH_POWER";
}
