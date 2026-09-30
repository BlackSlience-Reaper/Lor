using System;
using System.Threading.Tasks;
using LibraryOfRuina.content.abnormalities.AllAroundHelper;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.framework.powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.abnormalities.Nosferatu;

public sealed class NosferatuBloodPower : LibraryOfRuinaPowerModel
{
    private sealed class Data
    {
        public bool Initialized;
        public int LostHpStep;
    }

    protected override string LegacyPowerId => "NOSFERATU_BLOOD_POWER";

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override object InitInternalData() => new Data();

    public static int GetStacks(Creature creature) =>
        Math.Max(0, creature.GetPower<NosferatuBloodPower>()?.Amount ?? 0);

    public static async Task Change(Creature creature, int delta, Creature? applier = null, CardModel? cardSource = null)
    {
        if (delta == 0 || creature.IsDead)
        {
            return;
        }

        NosferatuBloodPower? existing = creature.GetPower<NosferatuBloodPower>();
        if (existing == null)
        {
            if (delta > 0)
            {
                NosferatuBloodPower? added = await PowerCmdCompat.Apply<NosferatuBloodPower>(
                    creature,
                    delta,
                    applier ?? creature,
                    cardSource);
                added?.InitializeLostHpStep(creature);
            }

            return;
        }

        await PowerCmdCompat.ModifyAmount(existing, delta, applier ?? creature, cardSource);
    }

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        InitializeLostHpStep();
        return Task.CompletedTask;
    }

    public override async Task AfterCurrentHpChanged(Creature creature, decimal delta)
    {
        if (creature != Owner || delta >= 0m || Owner.MaxHp <= 0 || Amount <= 0)
        {
            return;
        }

        Data data = GetInternalData<Data>();
        if (!data.Initialized)
        {
            InitializeLostHpStep(creature);
        }

        int currentStep = CurrentLostHpStep();
        int stepsLost = currentStep - data.LostHpStep;
        if (stepsLost <= 0)
        {
            return;
        }

        data.LostHpStep = currentStep;
        Flash();
        await PowerCmdCompat.ModifyAmount(this, -stepsLost, null, null);
    }

    private void InitializeLostHpStep(Creature? creature = null)
    {
        Data data = GetInternalData<Data>();
        data.Initialized = true;
        data.LostHpStep = CurrentLostHpStep(creature);
    }

    private int CurrentLostHpStep(Creature? creature = null)
    {
        Creature? owner = creature ?? Owner;
        if (owner == null || owner.MaxHp <= 0)
        {
            return 0;
        }

        decimal lostPercent = Math.Max(0m, owner.MaxHp - owner.CurrentHp) * 100m / owner.MaxHp;
        return (int)Math.Floor(lostPercent / 5m);
    }
}

public sealed class NosferatuHydrophobiaPassivePower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "NOSFERATU_HYDROPHOBIA_PASSIVE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("NosferatuThreshold", 4),
        new DynamicVar("BatThreshold", 1),
        new DynamicVar("NosferatuMaxStrong", 2),
        new DynamicVar("BatStrong", 1)
    ];

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

        if (Owner.Monster is Nosferatu nosferatu)
        {
            if (nosferatu.IsTransformed)
            {
                await NosferatuBloodPower.Change(Owner, -1, Owner);
            }

            int blood = NosferatuBloodPower.GetStacks(Owner);
            if (blood <= 4)
            {
                int strong = Math.Clamp(5 - blood, 1, 2);
                Flash();
                await LibraryPowerCmd.Apply<LibraryStrongPower>(
                    new ThrowingPlayerChoiceContext(),
                    Owner,
                    strong,
                    0,
                    IsPermanent: false,
                    Owner,
                    null);
            }

            return;
        }

        if (Owner.Monster is BloodBat && NosferatuBloodPower.GetStacks(Owner) <= 1)
        {
            Flash();
            await LibraryPowerCmd.Apply<LibraryStrongPower>(
                new ThrowingPlayerChoiceContext(),
                Owner,
                1,
                0,
                IsPermanent: false,
                Owner,
                null);
        }
    }
}

public sealed class NosferatuTransformPower : LibraryOfRuinaPowerModel, LibraryOfRuina.infra.helpers.IFinalHpLossClamp
{
    private sealed class Data
    {
        public bool Triggered;
        public bool PendingTransform;
    }

    protected override string LegacyPowerId => "NOSFERATU_TRANSFORM_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override object InitInternalData() => new Data();

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("HpPercent", 50)
    ];

    public bool IsPendingTransform => GetInternalData<Data>().PendingTransform;

    public decimal ClampFinalHpLoss(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (target != Owner
            || amount <= 0m
            || Owner.Monster is not Nosferatu { IsTransformed: false })
        {
            return amount;
        }

        if (GetInternalData<Data>().PendingTransform)
        {
            return 0m;
        }

        decimal maxLossBeforeTransform = Owner.CurrentHp - TransformHpThreshold();
        return maxLossBeforeTransform > 0m
            ? Math.Min(amount, maxLossBeforeTransform)
            : amount;
    }

    public override async Task AfterCurrentHpChanged(Creature creature, decimal delta)
    {
        if (creature != Owner
            || delta >= 0m
            || Owner.Monster is not Nosferatu { IsTransformed: false })
        {
            return;
        }

        if (Owner.CurrentHp <= TransformHpThreshold())
        {
            await QueueTransformAndClampHp();
        }
    }

    public override bool ShouldDie(Creature creature)
    {
        if (creature != Owner || Owner.Monster is not Nosferatu { IsTransformed: false })
        {
            return true;
        }

        return !GetInternalData<Data>().PendingTransform && Owner.CurrentHp > TransformHpThreshold();
    }

    public override async Task AfterPreventingDeath(Creature creature)
    {
        if (creature == Owner && Owner.Monster is Nosferatu { IsTransformed: false })
        {
            await QueueTransformAndClampHp();
        }
    }

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (side != CombatSide.Player
            || Owner.IsDead
            || !GetInternalData<Data>().PendingTransform
            || Owner.Monster is not Nosferatu nosferatu)
        {
            return;
        }

        Flash();
        await nosferatu.TransformToBloodfiend();
    }

    private async Task QueueTransformAndClampHp()
    {
        Data data = GetInternalData<Data>();
        if (data.Triggered && data.PendingTransform)
        {
            if (Owner.CurrentHp < TransformHpThreshold())
            {
                await CreatureCmd.SetCurrentHp(Owner, TransformHpThreshold());
            }

            return;
        }

        data.Triggered = true;
        data.PendingTransform = true;
        Flash();

        decimal threshold = TransformHpThreshold();
        if (Owner.CurrentHp < threshold)
        {
            await CreatureCmd.SetCurrentHp(Owner, threshold);
        }
    }

    private decimal TransformHpThreshold() => Math.Max(1m, Math.Ceiling(Owner.MaxHp * 0.5m));
}

public sealed class NosferatuFlowingBloodPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "NOSFERATU_FLOWING_BLOOD_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;
}

public sealed class NosferatuHydrophobiaPower : LibraryOfRuinaPowerModel
{
    private const int HpLossPercent = 5;
    private const int StrongAmount = 1;
    private const int SwiftAmount = 1;
    private const int BleedAmount = 5;

    protected override string LegacyPowerId => "NOSFERATU_HYDROPHOBIA_POWER";

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("HpLossPercent", HpLossPercent),
        new DynamicVar("Strong", StrongAmount),
        new DynamicVar("Swift", SwiftAmount),
        new DynamicVar("Bleed", BleedAmount)
    ];

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (Owner.IsDead || !TurnParticipants.IsOwnTurn(Owner, side, participants) || Amount <= 0)
        {
            return;
        }

        Flash();
        int hpLoss = Math.Max(1, (int)Math.Ceiling(Owner.MaxHp * HpLossPercent / 100m));
        await CreatureCmdCompat.Damage(
            choiceContext,
            Owner,
            hpLoss,
            ValueProp.Unblockable | ValueProp.Unpowered,
            null,
            null);

        if (!Owner.IsDead)
        {
            await LibraryPowerCmd.Apply<LibraryStrongPower>(
                new ThrowingPlayerChoiceContext(),
                Owner,
                StrongAmount,
                0,
                IsPermanent: false,
                Owner,
                null);
            await PowerCmdCompat.Apply<LibraryOfRuinaAllAroundHelperSwiftPower>(
                Owner,
                SwiftAmount,
                Owner,
                null);
            await PowerCmdCompat.ApplyDebuff<LibraryBleedingPower>(
                Owner,
                BleedAmount,
                Owner,
                null);
        }

        if (Amount <= 1)
        {
            await PowerCmd.Remove(this);
        }
        else
        {
            await PowerCmdCompat.ModifyAmount(this, -1, Owner, null);
        }
    }
}
