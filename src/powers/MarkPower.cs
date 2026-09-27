using LibraryOfRuina.encounters.QueenOfHatred;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace LibraryOfRuina.powers;

public sealed class LibraryOfRuinaMarkPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "LIBRARY_OF_RUINA_MARK_POWER";

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("RapidWear", QueenOfHatredMarkHelper.PermanentRapidWearAmount)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<LibraryVulnerablePower>()
    ];
}
