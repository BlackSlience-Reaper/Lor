using LibraryOfRuina.framework.cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.content.abnormalities.ForsakenMurderer;

public abstract class ForsakenMurdererPageChoiceCardBase : PageChoiceCard<ForsakenMurdererPageMode>
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<StrengthPower>(),
        HoverTipFactory.FromPower<LibraryVulnerablePower>()
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("StrengthLoss", ForsakenMurdererPageRelic.IronEchoStrengthLoss),
        new DamageVar(ForsakenMurdererPageRelic.BoundWrathDamage, ValueProp.Unpowered),
        new DynamicVar("Strength", ForsakenMurdererPageRelic.ExtremeViolenceStrength),
        new DynamicVar("RapidWear", ForsakenMurdererPageRelic.ExtremeViolenceRapidWear)
    ];
}
