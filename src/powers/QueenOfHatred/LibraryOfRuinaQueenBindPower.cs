using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.framework.powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.powers.QueenOfHatred;

public sealed class LibraryOfRuinaQueenBindPower : LibraryOfRuinaPowerModel
{
    private const int CardsPerTrigger = 1;

    protected override string LegacyPowerId => "QUEEN_BIND_POWER";

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Cards", CardsPerTrigger)
    ];

    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        if (Amount <= 0 || Owner.IsDead || cardPlay.Card.Owner.Creature != Owner)
        {
            return;
        }

        Flash();
        await CreatureCmdCompat.Damage(
            context,
            Owner,
            Amount,
            ValueProp.Unpowered,
            Applier ?? Owner,
            null);
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Player)
        {
            await PowerCmd.Remove(this);
        }
    }
}
