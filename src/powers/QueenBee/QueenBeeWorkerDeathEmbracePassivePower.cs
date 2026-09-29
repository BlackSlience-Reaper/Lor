using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using LibraryOfRuina.framework.powers;

namespace LibraryOfRuina.powers.QueenBee;

public sealed class QueenBeeWorkerDeathEmbracePassivePower : LibraryOfRuinaPowerModel
{
    internal const int HpThresholdPercent = 30;

    protected override string LegacyPowerId => "QUEEN_BEE_WORKER_DEATH_EMBRACE_PASSIVE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("HpThresholdPercent", HpThresholdPercent)
    ];
}
