using System;
using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.powers;
using LibraryOfRuina.interop;
using LibraryOfRuina.patches;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.liberation.Literature;

public sealed class LiteratureFloorExpressionPassivePower :
    LiteratureFloorGreenPassivePower
{
    public const int BaseCardsPerTrigger = 6;
    public const int StrengthGain = 3;

    private sealed class CardsVar : DynamicVar
    {
        public CardsVar() : base("Cards", BaseCardsPerTrigger)
        {
        }
    }

    protected override string LegacyPowerId =>
        "LITERATURE_FLOOR_EXPRESSION_PASSIVE_POWER";

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(),
        new PowerVar<StrengthPower>("Strength", StrengthGain)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<StrengthPower>()
    ];

    public override Task BeforeApplied(
        Creature target,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        UpdateCardsRequiredText(
            ResolveCardsRequiredForCombatState(target.CombatState));
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        if (Owner.IsDead || !cardPlay.Card.Owner.Creature.IsPlayer)
        {
            return;
        }

        int cardsRequired = ResolveCardsRequiredForCombatState(CombatState);
        UpdateCardsRequiredText(cardsRequired);
        int remaining = Math.Clamp(Amount, 1, cardsRequired);
        if (remaining > 1)
        {
            SetAmount(remaining - 1, silent: true);
            return;
        }

        SetAmount(cardsRequired, silent: true);
        Flash();
        await PowerCmdCompat.Apply<StrengthPower>(
            Owner,
            StrengthGain,
            Owner,
            null);
    }

    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (target != Owner
            || Owner.IsDead
            || result.UnblockedDamage <= 0
            || !ValuePropCompat.IsPoweredAttack(props)
            || Owner.Monster
                is not LiteratureFloorTodaysExpressionBoss expression)
        {
            return;
        }

        if (await expression.RandomizeExpressionFromUnblockedHit())
        {
            Flash();
        }
    }

    internal static int ResolveCardsRequiredForCombatState(
        CombatStateLike? combatState) =>
        ResolveCardsRequiredForPlayerCount(
            MultiplayerScalingPatchHelper.ResolveRunPlayerCount(
                combatState));

    internal static int ResolveCardsRequiredForPlayerCount(
        int playerCount) => playerCount switch
        {
            <= 1 => 6,
            2 => 9,
            3 => 12,
            _ => 15
        };

    private void UpdateCardsRequiredText(int cardsRequired)
    {
        DynamicVars["Cards"].BaseValue = cardsRequired;
    }
}

public sealed class LiteratureFloorWaveringFeelingsPassivePower :
    LiteratureFloorGreenPassivePower
{
    public const int Interval = 4;

    protected override string LegacyPowerId =>
        "LITERATURE_FLOOR_WAVERING_FEELINGS_PASSIVE_POWER";

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Interval", Interval)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<LibraryOfRuinaConfusionPower>()
    ];

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (side != CombatSide.Player
            || Owner.IsDead
            || Owner.Monster
                is not LiteratureFloorTodaysExpressionBoss expression
            || expression.IsWaveringFeelingsQueued)
        {
            return;
        }

        if (Amount > 1)
        {
            SetAmount(Amount - 1, silent: true);
            return;
        }

        if (await expression.TryQueueWaveringFeelings())
        {
            Flash();
        }
    }

    internal void MarkSpecialUsed()
    {
        SetAmount(Interval, silent: true);
    }
}
