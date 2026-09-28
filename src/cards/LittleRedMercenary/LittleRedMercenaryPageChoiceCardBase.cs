using LibraryOfRuina.powers.LittleRedMercenary;
using LibraryOfRuina.relics.LittleRedMercenary;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.cards.LittleRedMercenary;

public abstract class LittleRedMercenaryPageChoiceCardBase : PageChoiceCard<LittleRedMercenaryPageMode>
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<StrengthPower>(),
        HoverTipFactory.FromPower<LittleRedPreyPower>()
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("ScarHpHigh", LittleRedMercenaryPageRelic.ScarHpPercentHigh),
        new DynamicVar("ScarHpMid", LittleRedMercenaryPageRelic.ScarHpPercentMid),
        new DynamicVar("ScarHpLow", LittleRedMercenaryPageRelic.ScarHpPercentLow),
        new DynamicVar("ScarStrengthHigh", LittleRedMercenaryPageRelic.ScarStrengthBelowHigh),
        new DynamicVar("ScarStrengthMid", LittleRedMercenaryPageRelic.ScarStrengthBelowMid),
        new DynamicVar("ScarStrengthLow", LittleRedMercenaryPageRelic.ScarStrengthBelowLow),
        new DynamicVar("HpLoss", LittleRedMercenaryPageRelic.RevengeHpLossPerTrigger),
        new DynamicVar("Strength", LittleRedMercenaryPageRelic.RevengeStrengthPerTrigger),
        new DynamicVar("MaxTriggers", LittleRedMercenaryPageRelic.RevengeMaxTriggersPerCombat),
        new DamageVar(LittleRedMercenaryPageRelic.PreyDamageBonus, ValueProp.Move)
    ];
}
