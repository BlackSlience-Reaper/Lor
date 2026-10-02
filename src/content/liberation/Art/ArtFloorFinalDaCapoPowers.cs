using System.Linq;
using System.Threading.Tasks;
using LibraryOfRuina.framework.combat;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Afflictions;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using LibraryOfRuina.framework.powers;

namespace LibraryOfRuina.content.liberation.Art;

public sealed class ArtFloorFinalDaCapoCyclePower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "ART_FLOOR_FINAL_DA_CAPO_CYCLE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Turns", 6)
    ];
}

public sealed class ArtFloorFinalDaCapoAriaPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "ART_FLOOR_FINAL_DA_CAPO_ARIA_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;
}

public sealed class ArtFloorFinalDaCapoPerformerPassivePower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "ART_FLOOR_FINAL_DA_CAPO_PERFORMER_PASSIVE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;
}

public sealed class ArtFloorAdagioCantabilePower : LibraryOfRuinaPowerModel
{
    private const int HealAmount = 6;

    protected override string LegacyPowerId => "ART_FLOOR_ADAGIO_CANTABILE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Heal", HealAmount)
    ];

#if STS2_0_111_0
    public override async Task AfterBlockBroken(
        PlayerChoiceContext choiceContext,
        Creature target,
        Creature? breaker)
#else
    public override async Task AfterBlockBroken(Creature target)
#endif
    {
        if (target != Owner)
        {
            return;
        }

        foreach (Creature player in Owner.CombatState?.LivingPlayerCreatures() ?? [])
        {
            await CreatureCmd.Heal(player, HealAmount);
        }

        await PowerCmd.Remove(this);
    }
}

public sealed class ArtFloorImbalancedPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "ART_FLOOR_IMBALANCED_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new EnergyVar(1)
    ];

    public override async Task AfterCardDrawn(
        PlayerChoiceContext choiceContext,
        CardModel card,
        bool fromHandDraw)
    {
        if (fromHandDraw || card.Owner?.Creature != Owner || Owner.Player == null)
        {
            return;
        }

        await PlayerCmd.LoseEnergy(1m, Owner.Player);
    }

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (TurnParticipants.IsOwnTurn(Owner, side, participants))
        {
            await PowerCmd.Remove(this);
        }
    }
}

public sealed class ArtFloorDaCapoSoulBindingPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "ART_FLOOR_DA_CAPO_SOUL_BINDING_POWER";

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips => HoverTipFactory.FromAffliction<Bound>();

    public override async Task AfterCardDrawn(
        PlayerChoiceContext choiceContext,
        CardModel card,
        bool fromHandDraw)
    {
        if (card.Owner != Owner.Player
            || CombatState.CurrentSide != Owner.Side
            || !ModelDb.Affliction<Bound>().CanAfflict(card))
        {
            return;
        }

        int boundThisTurn = CombatManager.Instance.History.Entries
            .OfType<CardAfflictedEntry>()
            .Count(entry => entry.HappenedThisTurn(CombatState)
                            && entry.Actor == Owner
                            && entry.Affliction is Bound);
        if (boundThisTurn >= Amount)
        {
            return;
        }

        await CardCmd.AfflictAndPreview<Bound>([card], 1m, CardPreviewStyle.None);
    }

    public override bool ShouldPlay(CardModel card, AutoPlayType autoPlayType)
    {
        return card.Owner?.Creature != Owner || card.Affliction is not Bound;
    }

    public override async Task BeforeSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner))
        {
            await PowerCmd.Remove(this);
        }
    }

    public override Task AfterRemoved(Creature oldOwner)
    {
        foreach (CardModel card in oldOwner.Player?.PlayerCombatState?.AllCards ?? [])
        {
            if (card.Affliction is Bound)
            {
                CardCmd.ClearAffliction(card);
            }
        }

        return Task.CompletedTask;
    }
}
