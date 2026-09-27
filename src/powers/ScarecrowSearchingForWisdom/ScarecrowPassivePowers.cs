using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace LibraryOfRuina.powers.ScarecrowSearchingForWisdom;

public sealed class ScarecrowHarvestWisdomPassivePower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "SCARECROW_HARVEST_WISDOM_PASSIVE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Turns", 3),
        new DynamicVar("Uses", 1),
        new DynamicVar("HealPercent", 10),
        new DynamicVar("Cards", 4),
        new DynamicVar("BonusDamage", 20)
    ];
}

public sealed class ScarecrowPeaceOfObtainedWisdomPassivePower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "SCARECROW_PEACE_OF_OBTAINED_WISDOM_PASSIVE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("ChaoDamage", 20)
    ];
}

public sealed class ScarecrowEmptyHeadPassivePower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "SCARECROW_EMPTY_HEAD_PASSIVE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("HpLoss", 12)
    ];
}
