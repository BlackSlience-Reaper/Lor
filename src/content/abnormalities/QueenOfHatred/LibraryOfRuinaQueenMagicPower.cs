using LibraryOfRuina.framework.powers;
using LibraryOfRuina.guests.DawnOffice;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;

namespace LibraryOfRuina.content.abnormalities.QueenOfHatred;

public sealed class LibraryOfRuinaQueenMagicPower : LibraryOfRuinaPowerModel
{
    protected override string LegacyPowerId => "QUEEN_MAGIC_POWER";

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Bind", QueenOfHatred.BindDamage),
        new PowerVar<LibraryOfRuinaNextTurnStrength>("NextTurnStrength", QueenOfHatred.LoveJusticeNextTurnStrength),
        new PowerVar<VulnerablePower>(QueenOfHatred.LoveAndHateVulnerable)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<LibraryBindingPower>(),
        HoverTipFactory.FromPower<LibraryOfRuinaNextTurnStrength>(),
        HoverTipFactory.FromPower<VulnerablePower>()
    ];
}
