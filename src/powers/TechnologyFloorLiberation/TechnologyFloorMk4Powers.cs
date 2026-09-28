using System;
using System.Linq;
using System.Threading.Tasks;
using LibraryLib.Entities.Creatures;
using LibraryOfRuina.compat;
using LibraryOfRuina.monsters.TechnologyFloorLiberation;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.powers.TechnologyFloorLiberation;

public sealed class TechnologyFloorMk4MaxChargePower : LibraryOfRuinaPowerModel
{
    public const int ChargeThreshold = 3;

    private sealed class Data
    {
        public int Charges;
        public bool DealtUnblockedDamageThisTurn;
    }

    protected override string LegacyPowerId => "TECHNOLOGY_FLOOR_MK4_MAX_CHARGE_POWER";

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override int DisplayAmount =>
        IsMutable ? GetInternalData<Data>().Charges : 0;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Threshold", ChargeThreshold)
    ];

    protected override object InitInternalData()
    {
        return new Data();
    }

    public override Task AfterAttack(
        PlayerChoiceContext choiceContext,
        AttackCommand command)
    {
        if (Owner.IsDead || command.Attacker != Owner)
        {
            return Task.CompletedTask;
        }

        bool dealt = AttackCommandCompat.Results(command)
            .Any(static result => result.Receiver.IsPlayer && result.UnblockedDamage > 0);

        if (dealt)
        {
            GetInternalData<Data>().DealtUnblockedDamageThisTurn = true;
        }

        return Task.CompletedTask;
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != Owner.Side || Owner.IsDead)
        {
            return;
        }

        Data data = GetInternalData<Data>();
        bool dealtDamage = data.DealtUnblockedDamageThisTurn;
        data.DealtUnblockedDamageThisTurn = false;

        if (dealtDamage)
        {
            return;
        }

        data.Charges += 1;
        InvokeDisplayAmountChanged();
        Flash();

        if (data.Charges >= ChargeThreshold && Owner.Monster is TechnologyFloorGrinderMk4Boss mk4)
        {
            data.Charges = 0;
            InvokeDisplayAmountChanged();
            await mk4.QueueLimiterReleaseSequence();
        }
    }
}

public sealed class TechnologyFloorMk4IdentificationMk2Power : LibraryOfRuinaPowerModel
{
    public const int DamageThreshold = 10;
    public const int StrengthGrant = 1;


    private sealed class Data
    {
        public int DamageDealtThisTurn;
    }

    // Internal data only exists on mutable instances; canonical models show the full threshold.
    public override int DisplayAmount =>
        IsMutable ? DamageThreshold - GetInternalData<Data>().DamageDealtThisTurn : DamageThreshold;

    protected override string LegacyPowerId => "TECHNOLOGY_FLOOR_MK4_IDENTIFICATION_MK2_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Threshold", DamageThreshold),
        new PowerVar<StrengthPower>("Strength", StrengthGrant)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<StrengthPower>()
    ];

    protected override object InitInternalData()
    {
        return new Data();
    }

    public override Task AfterAttack(
        PlayerChoiceContext choiceContext,
        AttackCommand command)
    {
        if (Owner.IsDead || command.Attacker != Owner)
        {
            return Task.CompletedTask;
        }

        int unblockedDealt = AttackCommandCompat.Results(command)
            .Where(static result => result.Receiver.IsPlayer)
            .Sum(static result => Math.Max(0, result.UnblockedDamage));

        if (unblockedDealt > 0)
        {
            GetInternalData<Data>().DamageDealtThisTurn += unblockedDealt;
            InvokeDisplayAmountChanged();
        }

        return Task.CompletedTask;
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != Owner.Side || Owner.IsDead)
        {
            return;
        }

        Data data = GetInternalData<Data>();
        int damageDealt = data.DamageDealtThisTurn;
        data.DamageDealtThisTurn = 0;
        InvokeDisplayAmountChanged();

        if (damageDealt < DamageThreshold)
        {
            return;
        }

        Flash();
        await PowerCmdCompat.Apply<StrengthPower>(Owner, StrengthGrant, Owner, null);
    }
}

public sealed class TechnologyFloorMk4LimiterReleasedPower : LibraryOfRuinaPowerModel
{
    private const int ReducedChao = 0;

    private sealed class Data
    {
        public bool TriggeredOnce;
    }

    protected override string LegacyPowerId => "TECHNOLOGY_FLOOR_MK4_LIMITER_RELEASED_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("ReducedChao", ReducedChao)
    ];

    protected override object InitInternalData()
    {
        return new Data();
    }

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (side != CombatSide.Player || Owner.IsDead)
        {
            return;
        }

        Data data = GetInternalData<Data>();
        if (data.TriggeredOnce)
        {
            await PowerCmd.Remove(this);
            return;
        }

        data.TriggeredOnce = true;
        Flash();

        if (Owner is LibraryCreature lc && lc.CurrentChaoValue > ReducedChao)
        {
            await LibraryCreatureCmd.ChaoDamage(
                choiceContext,
                [lc],
                lc.CurrentChaoValue,
                ValueProp.Unblockable | ValueProp.Unpowered,
                Owner,
                null,
                null);
        }
    }
}
