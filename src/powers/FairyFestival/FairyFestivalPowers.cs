using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.backgrounds.FairyFestival;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.audio;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.monsters.FairyFestival;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.powers.FairyFestival;

public sealed class FairyFestivalReservedFoodPower : LibraryOfRuinaPowerModel
{
    // 储备粮触发女王短暂饱腹时的畸块生命阈值。
    public const int DevourHpThreshold = 1;

    protected override string LegacyPowerId => "FAIRY_FESTIVAL_RESERVED_FOOD_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Threshold", DevourHpThreshold)
    ];
}

public sealed class FairyMassCarePower : LibraryOfRuinaPowerModel, LibraryOfRuina.infra.helpers.IFinalHpLossClamp
{
    private const decimal HealRatio = 0.10m;
    private const int PreservedHp = 1;

    protected override string LegacyPowerId => "FAIRY_MASS_CARE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    internal bool IsHealthBarLockActive =>
        Owner.CurrentHp <= PreservedHp && ShouldPreserveAtOneHp();

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new FairyMassCareHealVar(),
        new DynamicVar("PreservedHp", PreservedHp)
    ];

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, CombatStateLike combatState)
    {
        if (side != CombatSide.Enemy || Owner.IsDead)
        {
            return;
        }

        if (Owner.CurrentHp <= PreservedHp && Owner.GetPower<FairyFestivalReservedFoodPower>() != null)
        {
            return;
        }
        int healAmount = GetHealAmount(Owner);
        Flash();
        await CreatureCmd.Heal(Owner, healAmount);
    }

    public decimal ClampFinalHpLoss(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (target != Owner || amount <= 0m || !ShouldPreserveAtOneHp())
        {
            return amount;
        }

        if (target.CurrentHp - amount > 0m)
        {
            return amount;
        }

        return target.CurrentHp <= PreservedHp ? 0m : target.CurrentHp - PreservedHp;
    }

    public override Task AfterModifyingHpLostAfterOsty()
    {
        Flash();
        return Task.CompletedTask;
    }

    // 直接死亡等绕过生命损失计算的路径继续由死亡钩子保留一血。
    public override bool ShouldDieLate(Creature creature)
    {
        return creature != Owner || !ShouldPreserveAtOneHp();
    }

    public override Task AfterPreventingDeath(Creature creature)
    {
        if (creature != Owner || !ShouldPreserveAtOneHp())
        {
            return Task.CompletedTask;
        }

        Flash();
        return CreatureCmd.SetCurrentHp(Owner, PreservedHp);
    }

    private bool ShouldPreserveAtOneHp()
    {
        return Owner.CombatState?.Enemies.Any(static creature =>
            creature.IsAlive && creature.Monster is FairyQueen) == true;
    }

    private static int GetHealAmount(Creature owner)
    {
        return (int)(owner.MaxHp * HealRatio);
    }

    private sealed class FairyMassCareHealVar : DynamicVar
    {
        public FairyMassCareHealVar()
            : base("Heal", 0m)
        {
        }

        protected override decimal GetBaseValueForIConvertible()
        {
            return _owner is FairyMassCarePower { IsMutable: true, Owner: not null } power ? GetHealAmount(power.Owner) : base.GetBaseValueForIConvertible();
        }

        public override string ToString()
        {
            return GetBaseValueForIConvertible().ToString();
        }
    }
}

public sealed class FairyQueenMomentarySatietyPower : LibraryOfRuinaPowerModel
{
    private const int VulnerableAmount = 3;
    private const int FlawAmount = 5;
    private const int StrengthDownAmount = 10;
    private const int FlawTurns = 2;
    private const int StrengthDownTurns = 1;

    protected override string LegacyPowerId => "FAIRY_QUEEN_MOMENTARY_SATIETY_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Threshold", FairyFestivalReservedFoodPower.DevourHpThreshold),
        new PowerVar<VulnerablePower>(VulnerableAmount),
        new DynamicVar("Flaw", FlawAmount),
        new DynamicVar("FlawTurns", FlawTurns),
        new PowerVar<StrengthPower>("StrengthDown", StrengthDownAmount),
        new DynamicVar("StrengthDownTurns", StrengthDownTurns)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<VulnerablePower>(),
        HoverTipFactory.FromPower<LibraryDisarmPower>(),
        HoverTipFactory.FromPower<LibraryWeakPower>()
    ];

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, CombatStateLike combatState)
    {
        if (side != CombatSide.Enemy || Owner.IsDead || Owner.Monster is not FairyQueen queen)
        {
            return;
        }

        Creature? food = FairyFestivalCombatHelper.FindReservedFoodAtOneHp(Owner);
        if (food == null)
        {
            return;
        }

        Flash();
        await queen.Devour(food);
        await PowerCmdCompat.Apply<VulnerablePower>(Owner, VulnerableAmount, Owner, null);
        await LibraryPowerCmd.Apply<LibraryDisarmPower>(
            new ThrowingPlayerChoiceContext(),
            Owner,
            FlawAmount,
            FlawTurns - 1,
            IsPermanent: false,
            Owner,
            null);
        await LibraryPowerCmd.Apply<LibraryWeakPower>(
            new ThrowingPlayerChoiceContext(),
            Owner,
            StrengthDownAmount,
            StrengthDownTurns - 1,
            IsPermanent: false,
            Owner,
            null);
    }
}

public sealed class FairyQueenStarvedFrenzyPower : LibraryOfRuinaPowerModel
{
    private const int StrengthAmount = 2;
    private const int GuardAmount = 1;

    internal bool IsDevouring { get; private set; }

    protected override string LegacyPowerId => "FAIRY_QUEEN_STARVED_FRENZY_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Threshold", FairyQueen.StarvedFrenzyHpThresholdPercent),
        new PowerVar<StrengthPower>(StrengthAmount),
        new DynamicVar("Guard", GuardAmount)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<StrengthPower>(),
        HoverTipFactory.FromPower<LibraryEndurancePower>()
    ];

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, CombatStateLike combatState)
    {
        if (Owner.IsDead || Owner.Monster is not FairyQueen queen)
        {
            return;
        }

        if (side == CombatSide.Player)
        {
            if (queen.TryConsumeStarvedAloneReward())
            {
                Flash();
                LocalOggOneShotPlayer.Play(FairyQueen.ChangeSfxPath, -1.5f);
                await PowerCmdCompat.Apply<StrengthPower>(Owner, StrengthAmount, Owner, null);
                await LibraryPowerCmd.Apply<LibraryEndurancePower>(
                    new ThrowingPlayerChoiceContext(),
                    Owner,
                    GuardAmount,
                    0,
                    true,
                    Owner,
                    null);
            }

            return;
        }

        if (side != CombatSide.Enemy)
        {
            return;
        }

        List<Creature> livingAllies = FairyFestivalCombatHelper.GetLivingFairyMasses(Owner).ToList();
        if (livingAllies.Count == 0)
        {
            FairyFestivalBackgroundController.SetStarvedBackground(true);
            queen.MarkStarvedAlonePending();
            return;
        }

        if (!queen.ShouldTriggerStarvedFrenzy())
        {
            return;
        }

        Flash();
        IsDevouring = true;
        try
        {
            foreach (Creature ally in livingAllies)
            {
                await queen.Devour(ally, forceKill: true);
            }
        }
        finally
        {
            IsDevouring = false;
        }
    }
}

internal static class FairyFestivalCombatHelper
{
    internal static int GetDevourForecastThreshold(Creature mass)
    {
        if (mass.Monster is not FairyMass || mass.IsDead || mass.MaxHp <= 0)
        {
            return 0;
        }

        int threshold = 0;
        foreach (Creature enemy in mass.CombatState?.Enemies ?? [])
        {
            if (!enemy.IsAlive || enemy.Monster is not FairyQueen queen)
            {
                continue;
            }

            if (enemy.GetPower<FairyQueenStarvedFrenzyPower>() is { } frenzy
                && (queen.IsStarvedFrenzyReady || frenzy.IsDevouring))
            {
                return mass.MaxHp;
            }

            if (mass.GetPower<FairyFestivalReservedFoodPower>() != null
                && enemy.GetPower<FairyQueenMomentarySatietyPower>() != null)
            {
                threshold = FairyFestivalReservedFoodPower.DevourHpThreshold;
            }
        }

        return threshold;
    }

    public static Creature? FindReservedFoodAtOneHp(Creature queen)
    {
        return GetLivingFairyMasses(queen).FirstOrDefault(static creature =>
            creature.CurrentHp <= FairyFestivalReservedFoodPower.DevourHpThreshold
            && creature.GetPower<FairyFestivalReservedFoodPower>() != null);
    }

    public static IEnumerable<Creature> GetLivingFairyMasses(Creature queen)
    {
        return queen.CombatState?.Enemies.Where(static creature =>
            creature.IsAlive && creature.Monster is FairyMass) ?? [];
    }
}
