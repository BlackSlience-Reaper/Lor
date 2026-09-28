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

    // 与原版 NoEnergyGainPower 同一写法：GainEnergy 经 Hook.ModifyEnergyGain 链式询问监听者，
    // 这里把玩家回合内的获得量改成 0；原版只对改过数值的监听者调 AfterModifyingEnergyGain，用来闪光。
    // 回合开始的能量重置不经过这个钩子，不受影响。
    public override decimal ModifyEnergyGain(Player player, decimal amount) =>
        amount > 0m && ShouldBlockExtraEnergyGain(player) ? 0m : amount;

    public override Task AfterModifyingEnergyGain()
    {
        Flash();
        return Task.CompletedTask;
    }

    private bool ShouldBlockExtraEnergyGain(Player player)
    {
        if (player != Owner.Player || Amount <= 0)
        {
            return false;
        }

        CombatStateLike? CombatState = player.Creature.CombatState;
        return CombatState != null && CombatState.CurrentSide == CombatSide.Player;
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
