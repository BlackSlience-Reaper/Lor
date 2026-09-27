using System.Threading.Tasks;
using LibraryOfRuina.monsters.Leticia;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace LibraryOfRuina.powers.Leticia;

public sealed class LeticiaSurpriseAppearancePower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "LETICIA_SURPRISE_APPEARANCE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Enemy || Owner.IsDead || Owner.Monster is not SurpriseGiftBox giftBox)
        {
            return;
        }

        int nextAmount = Amount - 1;
        SetAmount(nextAmount);

        if (nextAmount <= 0)
        {
            await giftBox.TriggerCountdownDeath(choiceContext);
        }
    }
}
