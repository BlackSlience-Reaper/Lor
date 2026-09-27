using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace LibraryOfRuina.powers;

public sealed class LibraryOfRuinaConfusionPower : LibraryOfRuinaPowerModel
{
    private const int ExtraEnergyGain = 0;

    protected override string LegacyPowerId => "LIBRARY_OF_RUINA_CONFUSION_POWER";

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Energy", ExtraEnergyGain)
    ];

    public override bool ShouldDraw(Player player, bool fromHandDraw)
    {
        if (fromHandDraw || player != Owner.Player || Amount <= 0)
        {
            return true;
        }

        CombatStateLike? CombatState = player.Creature.CombatState;
        if (CombatState == null || CombatState.CurrentSide != CombatSide.Player)
        {
            return true;
        }

        Flash();
        return false;
    }

    public bool ShouldBlockExtraEnergyGain(Player player)
    {
        if (player != Owner.Player || Amount <= 0)
        {
            return false;
        }

        CombatStateLike? CombatState = player.Creature.CombatState;
        return CombatState != null && CombatState.CurrentSide == CombatSide.Player;
    }

    public void NotifyExtraEnergyGainPrevented()
    {
        Flash();
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Player)
        {
            return;
        }

        await PowerCmd.Decrement(this);
    }
}
