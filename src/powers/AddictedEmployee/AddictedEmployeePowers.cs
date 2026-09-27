using System.Threading.Tasks;
using LibraryOfRuina.compat;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;

namespace LibraryOfRuina.powers.AddictedEmployee;

public sealed class LibraryOfRuinaAddictedEmployeeMelodyCravingPower : LibraryOfRuinaPowerModel
{
    private const int StrengthGain = 1;
    private const int VulnerableGain = 1;

    protected override string LegacyPowerId => "ADDICTED_EMPLOYEE_MELODY_CRAVING_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<StrengthPower>(StrengthGain),
        new PowerVar<VulnerablePower>(VulnerableGain)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<StrengthPower>(),
        HoverTipFactory.FromPower<VulnerablePower>()
    ];

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (Owner.IsDead || side != CombatSide.Enemy || Owner.Side != CombatSide.Enemy)
        {
            return;
        }

        await PowerCmdCompat.Apply<StrengthPower>(Owner, StrengthGain, Owner, null, silent: false);
        await PowerCmdCompat.Apply<VulnerablePower>(Owner, VulnerableGain, Owner, null, silent: false);

        if (Owner.Monster is monsters.AddictedEmployee.AddictedEmployee addictedEmployee)
        {
            addictedEmployee.TriggerMelodyCravingMoonText();
        }
    }
}
