using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using LibraryOfRuina.framework.powers;
using Scarecrow = LibraryOfRuina.content.abnormalities.ScarecrowSearchingForWisdom.ScarecrowSearchingForWisdom;

namespace LibraryOfRuina.content.abnormalities.ScarecrowSearchingForWisdom;

public sealed class ScarecrowHarvestWisdomPassivePower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "SCARECROW_HARVEST_WISDOM_PASSIVE_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Turns", Scarecrow.HarvestIntervalTurns),
        new DynamicVar("HealPercent", Scarecrow.HarvestHealPercent),
        new DynamicVar("Cards", Scarecrow.HarvestCardsToSteal)
    ];
}
