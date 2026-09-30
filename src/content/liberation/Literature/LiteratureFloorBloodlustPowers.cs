using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
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
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.liberation.Literature;

public sealed class LiteratureFloorDeepWoundPower :
    LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId =>
        "LITERATURE_FLOOR_DEEP_WOUND_POWER";

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override bool AllowNegative => false;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [HoverTipFactory.FromPower<LibraryBleedingPower>()];

    public override async Task AfterCardPlayed(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        if (Amount <= 0
            || Owner.IsDead
            || cardPlay.Card.Owner.Creature != Owner)
        {
            return;
        }

        Flash();
        await PowerCmdCompat.ApplyDebuff<LibraryBleedingPower>(
            choiceContext,
            Owner,
            Amount,
            Applier ?? Owner,
            null);
    }

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (TurnParticipants.IsOwnTurn(Owner, side, participants))
        {
            await PowerCmd.Decrement(this);
        }
    }
}

public sealed class LiteratureFloorBloodlustGiantAxePassivePower :
    LiteratureFloorGreenPassivePower
{
    public const int DeepWoundPerHit = 1;

    protected override string LegacyPowerId =>
        "LITERATURE_FLOOR_BLOODLUST_GIANT_AXE_PASSIVE_POWER";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("DeepWound", DeepWoundPerHit)];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [HoverTipFactory.FromPower<LiteratureFloorDeepWoundPower>()];

    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        if (dealer != Owner
            || !target.IsPlayer
            || result.UnblockedDamage <= 0
            || !ValuePropCompat.IsPoweredAttack(props))
        {
            return;
        }

        Flash();
        await PowerCmdCompat.ApplyDebuff<LiteratureFloorDeepWoundPower>(
            choiceContext,
            target,
            DeepWoundPerHit,
            Owner,
            null);
    }
}

public sealed class LiteratureFloorBloodlustGlitterPassivePower :
    LiteratureFloorGreenPassivePower
{
    public const int BleedThreshold = 6;
    public const int Interval = 3;

    protected override string LegacyPowerId =>
        "LITERATURE_FLOOR_BLOODLUST_GLITTER_PASSIVE_POWER";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("BleedThreshold", BleedThreshold),
        new DynamicVar("Interval", Interval)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [HoverTipFactory.FromPower<LibraryBleedingPower>()];

    public override async Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants,
        CombatStateLike combatState)
    {
        if (!TurnParticipants.IsRoundPlayerTurn(side)
            || Owner.IsDead
            || Owner.Monster is not LiteratureFloorBloodlustBoss boss)
        {
            return;
        }

        if (Amount < Interval)
        {
            SetAmount(Amount + 1, silent: true);
        }

        bool thresholdReached = combatState.PlayerCreatures.Any(
            static player =>
                player.IsAlive
                && (player.GetPower<LibraryBleedingPower>()?.Amount ?? 0)
                    >= BleedThreshold);
        if (Amount < Interval || !thresholdReached)
        {
            return;
        }

        if (await boss.TryQueueUnbearableMove())
        {
            Flash();
        }
    }

    internal void MarkSpecialUsed()
    {
        SetAmount(0, silent: true);
    }
}
