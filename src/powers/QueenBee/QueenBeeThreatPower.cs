using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace LibraryOfRuina.powers.QueenBee;

public sealed class QueenBeeThreatPower : LibraryOfRuinaPowerModel
{
    internal const int ChaosDamagePerAttack = 4;

    protected override string LegacyPowerId => "QUEEN_BEE_THREAT_POWER";

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.None;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("ChaosDamage", ChaosDamagePerAttack)
    ];
}
