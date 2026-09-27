using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace LibraryOfRuina.powers;

public sealed class LibraryOfRuinaGhostConcealPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "GHOST_CONCEAL_POWER";

    public const int HideCycleLength = 2;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Turns", HideCycleLength)
    ];

    public override int DisplayAmount => IsMutable ? ExplicitCycleCount : Amount;

    public int TurnsUntilHiddenIntent => Amount <= 0 ? 1 : Amount;

    
    
    private int ExplicitCycleCount => HideCycleLength - TurnsUntilHiddenIntent + 1;

    public bool ShouldHideNextIntent()
    {
        return TurnsUntilHiddenIntent == 1;
    }

    public void AdvanceCounter()
    {
        if (ShouldHideNextIntent())
        {
            Flash();
            SetAmount(HideCycleLength, silent: true);
            return;
        }

        SetAmount(TurnsUntilHiddenIntent - 1, silent: true);
    }
}
