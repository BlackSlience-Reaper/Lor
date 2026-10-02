using System;
using System.Threading.Tasks;
using Godot;
using LibraryLib.SpeedDice;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.combat;
using LibraryOfRuina.interop;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.framework.powers;

internal static class LibraryOfRuinaParalysisCardClassifier
{
    internal static bool CountsAsAttack(CardModel card)
    {
        if (card.Type == CardType.Attack)
        {
            return true;
        }

        return card is ILibrarySpeedDiceCard
            {
                CountsAsAttackDuringSpeedDiceResolution: true
            }
            && LibrarySpeedDice.TryGetResolvingSlot(card, out _);
    }
}

public sealed class LibraryOfRuinaParalysisPower : LibraryOfRuinaPowerModel, ISecondaryDisplayAmountPower
{
    private const int DefaultTurnsRemaining = 2;
    private const int StackLossPerAttack = 1;

    private sealed class Data
    {
        public int TurnsRemaining = DefaultTurnsRemaining;
    }

    private sealed class TurnsVar : DynamicVar
    {
        public TurnsVar() : base("Turns", 0m)
        {
        }

        protected override decimal GetBaseValueForIConvertible()
        {
            return _owner is LibraryOfRuinaParalysisPower power
                ? power.TurnsRemainingForText
                : base.GetBaseValueForIConvertible();
        }

        public override string ToString()
        {
            return GetBaseValueForIConvertible().ToString();
        }
    }

    private sealed class PercentVar : DynamicVar
    {
        public PercentVar() : base("Percent", 0m)
        {
        }

        protected override decimal GetBaseValueForIConvertible()
        {
            return _owner is LibraryOfRuinaParalysisPower power
                ? power.Amount * 25m
                : base.GetBaseValueForIConvertible();
        }

        public override string ToString()
        {
            return GetBaseValueForIConvertible().ToString();
        }
    }

    protected override string LegacyPowerId => "LIBRARY_OF_RUINA_PARALYSIS_POWER";

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new TurnsVar(),
        new PercentVar(),
        new DynamicVar("StackLoss", StackLossPerAttack)
    ];

    public bool ShowSecondaryDisplayAmount => TurnsRemaining > 0;

    public int SecondaryDisplayAmount => TurnsRemaining;

    public Color SecondaryDisplayAmountLabelColor => _normalAmountLabelColor;

    protected override object InitInternalData()
    {
        return new Data();
    }

    private int TurnsRemaining => GetInternalData<Data>().TurnsRemaining;

    public int TurnsRemainingForText => TurnsRemaining;

#if STS2_0_111_0
    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource, CardPlay? cardPlay)
#else
    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
#endif
    {
        if (dealer != Owner || Amount <= 0 || !ValuePropCompat.IsPoweredAttack(props))
        {
            return 1m;
        }

        return Math.Max(0m, 1m - 0.25m * Amount);
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        if (Owner.IsDead
            || cardPlay.Card.Owner.Creature != Owner
            || !LibraryOfRuinaParalysisCardClassifier.CountsAsAttack(
                cardPlay.Card)
            || Amount <= 0)
        {
            return;
        }

        await PowerCmdCompat.ModifyAmount(this, -StackLossPerAttack, Owner, null, silent: true);
        if (Amount <= 0)
        {
            await PowerCmd.Remove(this);
        }
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (!TurnParticipants.IsOwnTurn(Owner, side, participants))
        {
            return;
        }

        int turnsRemaining = Math.Max(0, TurnsRemaining - 1);
        SetTurnsRemaining(turnsRemaining);
        if (turnsRemaining <= 0)
        {
            await PowerCmd.Remove(this);
        }
    }

    public void SetTurnsRemaining(int turnsRemaining)
    {
        AssertMutable();
        GetInternalData<Data>().TurnsRemaining = Math.Max(0, turnsRemaining);
        InvokeDisplayAmountChanged();
    }
}
