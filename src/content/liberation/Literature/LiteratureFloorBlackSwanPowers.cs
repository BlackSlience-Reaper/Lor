using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.interop;
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

namespace LibraryOfRuina.content.liberation.Literature;

public sealed class LiteratureFloorBlackSwanNettleGarmentPassivePower :
    LiteratureFloorGreenPassivePower
{
    protected override string LegacyPowerId =>
        "LITERATURE_FLOOR_BLACK_SWAN_NETTLE_GARMENT_PASSIVE_POWER";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("StartRound", 2),
        new DynamicVar(
            "BrotherLimit",
            LiteratureFloorBlackSwanBoss.MaxLivingBrothers)
    ];

    public override Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        return TurnParticipants.IsRoundPlayerTurn(side)
               && Owner.Monster is LiteratureFloorBlackSwanBoss boss
            ? boss.OnPlayerSideTurnStart(combatState)
            : Task.CompletedTask;
    }
}

public sealed class LiteratureFloorBlackSwanProtectFamilyPassivePower :
    LiteratureFloorGreenPassivePower
{
    protected override string LegacyPowerId =>
        "LITERATURE_FLOOR_BLACK_SWAN_PROTECT_FAMILY_PASSIVE_POWER";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar(
            "BrotherLimit",
            LiteratureFloorBlackSwanBoss.MaxLivingBrothers)
    ];
}

public sealed class LiteratureFloorBlackSwanBrokenDreamPassivePower :
    LiteratureFloorGreenPassivePower
{
    protected override string LegacyPowerId =>
        "LITERATURE_FLOOR_BLACK_SWAN_BROKEN_DREAM_PASSIVE_POWER";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar(
            "Deaths",
            LiteratureFloorBlackSwanBoss.DeadBrothersForSwanSong)
    ];
}

public sealed class LiteratureFloorBlackSwanVanishingFamilyPower :
    LibraryOfRuinaPowerModel
{
    public const string CustomIconPath =
        "res://images/powers/literature_floor_black_swan_vanishing_family_power.png";

    protected override string LegacyPowerId =>
        "LITERATURE_FLOOR_BLACK_SWAN_VANISHING_FAMILY_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override string PackedIconPath => CustomIconPath;

    public override string ResolvedBigIconPath => PackedIconPath;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar(
            "Threshold",
            LiteratureFloorBlackSwanBoss.DeadBrothersForSwanSong)
    ];

    internal void InitializeCounter()
    {
        SetAmount(0, silent: true);
    }

    internal void IncrementCounter()
    {
        SetAmount(Amount + 1, silent: true);
        InvokeDisplayAmountChanged();
    }

    internal void SynchronizeCounter(int fallenBrothers)
    {
        int synchronized = Math.Clamp(
            fallenBrothers,
            0,
            LiteratureFloorBlackSwanBoss.DeadBrothersForSwanSong);
        if (Amount == synchronized)
        {
            return;
        }

        SetAmount(synchronized, silent: true);
        InvokeDisplayAmountChanged();
    }

}

public abstract class LiteratureFloorBlackSwanBrotherPassivePower :
    LiteratureFloorGreenPassivePower
{
    protected LiteratureFloorBlackSwanBoss? ResolveBlackSwan()
    {
        return Owner.CombatState?.LivingEnemies()
            .Select(static enemy => enemy.Monster)
            .OfType<LiteratureFloorBlackSwanBoss>()
            .FirstOrDefault();
    }
}

public sealed class LiteratureFloorBlackSwanFirstBrotherPassivePower :
    LiteratureFloorBlackSwanBrotherPassivePower
{
    protected override string LegacyPowerId =>
        "LITERATURE_FLOOR_BLACK_SWAN_FIRST_BROTHER_PASSIVE_POWER";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new PowerVar<IntangiblePower>(1)];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [HoverTipFactory.FromPower<IntangiblePower>()];
}

public sealed class LiteratureFloorBlackSwanSecondBrotherPassivePower :
    LiteratureFloorBlackSwanBrotherPassivePower
{
    protected override string LegacyPowerId =>
        "LITERATURE_FLOOR_BLACK_SWAN_SECOND_BROTHER_PASSIVE_POWER";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new PowerVar<StrengthPower>(1)];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [HoverTipFactory.FromPower<StrengthPower>()];
}

public sealed class LiteratureFloorBlackSwanThirdBrotherPassivePower :
    LiteratureFloorBlackSwanBrotherPassivePower
{
    protected override string LegacyPowerId =>
        "LITERATURE_FLOOR_BLACK_SWAN_THIRD_BROTHER_PASSIVE_POWER";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new PowerVar<PlatingPower>(LiteratureFloorBlackSwanBoss.ThirdBrotherPlating)];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [HoverTipFactory.FromPower<PlatingPower>()];
}

public sealed class LiteratureFloorBlackSwanFourthBrotherPassivePower :
    LiteratureFloorBlackSwanBrotherPassivePower
{
    protected override string LegacyPowerId =>
        "LITERATURE_FLOOR_BLACK_SWAN_FOURTH_BROTHER_PASSIVE_POWER";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar(
            "DamageIncreasePercent",
            LiteratureFloorBlackSwanBoss.FourthBrotherDamageIncreasePercent)
    ];

    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        if (Owner.IsDead
            || dealer?.Monster is not LiteratureFloorBlackSwanBoss
            || !ValuePropCompat.IsPoweredAttack(props))
        {
            return 1m;
        }

        return 1m
               + LiteratureFloorBlackSwanBoss
                   .FourthBrotherDamageIncreasePercent / 100m;
    }
}

public sealed class LiteratureFloorBlackSwanFifthBrotherPassivePower :
    LiteratureFloorBlackSwanBrotherPassivePower
{
    protected override string LegacyPowerId =>
        "LITERATURE_FLOOR_BLACK_SWAN_FIFTH_BROTHER_PASSIVE_POWER";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar(
            "HealPercent",
            LiteratureFloorBlackSwanBoss.FifthBrotherHealPercent)
    ];

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Enemy || Owner.IsDead)
        {
            return;
        }

        LiteratureFloorBlackSwanBoss? boss = ResolveBlackSwan();
        if (boss?.Creature is not { IsAlive: true } bossCreature)
        {
            return;
        }

        decimal heal = Math.Max(
            1m,
            Math.Ceiling(
                bossCreature.MaxHp
                * LiteratureFloorBlackSwanBoss.FifthBrotherHealPercent
                / 100m));
        Flash();
        await CreatureCmd.Heal(
            bossCreature,
            heal);
    }
}

public sealed class LiteratureFloorBlackSwanSixthBrotherPassivePower :
    LiteratureFloorBlackSwanBrotherPassivePower
{
    protected override string LegacyPowerId =>
        "LITERATURE_FLOOR_BLACK_SWAN_SIXTH_BROTHER_PASSIVE_POWER";

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [HoverTipFactory.Static(StaticHoverTip.Stun)];
}
