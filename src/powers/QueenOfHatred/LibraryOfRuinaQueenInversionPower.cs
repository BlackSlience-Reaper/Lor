using System;
using System.Threading.Tasks;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.powers.QueenOfHatred;

public sealed class LibraryOfRuinaQueenInversionPower : LibraryOfRuinaPowerModel
{
    private sealed class Data
    {
        public bool TransitionTriggered;
    }

    private const int DisplayOffset = 1;
    private const int MaxHysteria = 100;
    private const int EndOfTurnGain = 6;
    private const int FullyBlockedGain = 17;
    private const int AnyHpLossGain = 6;
    private const int BlockBreakLoss = 12;
    private const int MarkedVictimLoss = 10;

    private sealed class HysteriaVar() : DynamicVar("Hysteria", 0m)
    {
        protected override decimal GetBaseValueForIConvertible()
        {
            return _owner is LibraryOfRuinaQueenInversionPower inversionPower
                ? inversionPower.DisplayAmount
                : base.GetBaseValueForIConvertible();
        }
    }

    protected override string LegacyPowerId => "QUEEN_INVERSION_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override int DisplayAmount => Math.Max(0, Amount - DisplayOffset);

    protected override object InitInternalData()
    {
        return new Data();
    }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new HysteriaVar(),
        new DynamicVar("MaxHysteria", MaxHysteria),
        new DynamicVar("EndOfTurnGain", EndOfTurnGain),
        new DynamicVar("FullyBlockedGain", FullyBlockedGain),
        new DynamicVar("AnyHpLossGain", AnyHpLossGain),
        new DynamicVar("BlockBreakLoss", BlockBreakLoss),
        new DynamicVar("MarkedVictimLoss", MarkedVictimLoss)
    ];

    public override async Task AfterCurrentHpChanged(Creature creature, decimal delta)
    {
        if (creature != Owner || delta >= 0m || Owner.IsDead)
        {
            return;
        }

        await ChangeHysteria(AnyHpLossGain);
    }

    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        if (dealer != Owner || !ValuePropCompat.IsPoweredAttack(props))
        {
            return;
        }

        int delta = 0;
        if (result.WasFullyBlocked)
        {
            delta += FullyBlockedGain;
        }

        if (result.WasBlockBroken)
        {
            delta -= BlockBreakLoss;
        }

        if (result.UnblockedDamage > 0 && target.GetPower<LibraryOfRuinaMarkPower>() != null)
        {
            delta -= MarkedVictimLoss;
        }

        if (delta != 0)
        {
            await ChangeHysteria(delta);
        }
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != Owner.Side || Owner.Monster is not monsters.QueenOfHatred.QueenOfHatred queen || Owner.IsDead)
        {
            return;
        }

        await ChangeHysteria(EndOfTurnGain);
        await queen.EnsurePersistentMarkPackages();
    }

    public override async Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (power != this || amount <= 0m)
        {
            return;
        }

        await TryTransformToSnake();
    }

    private async Task ChangeHysteria(int delta)
    {
        int currentHysteria = DisplayAmount;
        int nextHysteria = Math.Clamp(currentHysteria + delta, 0, MaxHysteria);
        int desiredInternalAmount = nextHysteria + DisplayOffset;
        int actualDelta = desiredInternalAmount - Amount;
        if (actualDelta == 0)
        {
            return;
        }

        SetAmount(desiredInternalAmount, silent: true);
        if (actualDelta > 0)
        {
            await TryTransformToSnake();
        }
    }

    private async Task TryTransformToSnake()
    {
        Data data = GetInternalData<Data>();
        if (data.TransitionTriggered
            || Owner.IsDead
            || Owner.Monster is not monsters.QueenOfHatred.QueenOfHatred queen
            || queen.IsSnakeForm
            || DisplayAmount < MaxHysteria)
        {
            return;
        }

        data.TransitionTriggered = true;
        await queen.TransformToSnake();
    }
}
