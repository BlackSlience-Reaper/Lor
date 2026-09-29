using System.Threading.Tasks;
using LibraryOfRuina.core.compat;
using LibraryOfRuina.monsters.Tomerry;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.powers;

public sealed class LibraryOfRuinaLastLovePower : LibraryOfRuinaPowerModel
{
    private sealed class Data
    {
        public bool Triggered;
        public bool PendingTransition;
    }

    private const int PhaseThresholdPercent = 50;
    private const int TransitionStrength = 3;
    private const int TransitionGuard = 2;
    private const int TransitionStunTurns = 1;

    protected override string LegacyPowerId => "LAST_LOVE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Threshold", PhaseThresholdPercent),
        new DynamicVar("StunTurns", TransitionStunTurns),
        new PowerVar<StrengthPower>(TransitionStrength),
        new DynamicVar("Guard", TransitionGuard)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<StrengthPower>(),
        HoverTipFactory.FromPower<LibraryEndurancePower>()
    ];

    protected override object InitInternalData()
    {
        return new Data();
    }

    public override async Task AfterCurrentHpChanged(Creature creature, decimal delta)
    {
        if (creature != Owner || delta >= 0m)
        {
            return;
        }

        Data data = GetInternalData<Data>();
        if (!data.PendingTransition && CrossesPhaseThreshold(creature, creature.CurrentHp - delta, creature.CurrentHp))
        {
            data.PendingTransition = true;
        }

        await TryTriggerPhaseTransition();
    }

    private static bool CrossesPhaseThreshold(Creature target, decimal previousHp, decimal nextHp)
    {
        decimal threshold = target.MaxHp * PhaseThresholdPercent / 100m;
        return previousHp > threshold && nextHp <= threshold;
    }

    private async Task TryTriggerPhaseTransition()
    {
        Data data = GetInternalData<Data>();
        if (!data.PendingTransition || data.Triggered || Owner.IsDead)
        {
            return;
        }

        data.PendingTransition = false;
        data.Triggered = true;


        if (Owner.Monster is Tomerry tomerry)
        {
            await tomerry.EnterPhaseTwo(stunTurns: TransitionStunTurns);
        }

        await PowerCmdCompat.Apply<StrengthPower>(Owner, TransitionStrength, Owner, null);
        await LibraryPowerCmd.Apply<LibraryEndurancePower>(
            new ThrowingPlayerChoiceContext(),
            Owner,
            TransitionGuard,
            0,
            true,
            applier: Owner,
            cardSource: null);
    }
}
