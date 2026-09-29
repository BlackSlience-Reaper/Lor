using LibraryOfRuina.framework.cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;

namespace LibraryOfRuina.content.liberation.History;

public abstract class SnowWhiteAppleChoiceCardBase : PageChoiceCard<SnowWhiteApplePageMode>
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        ..HoverTipFactory.FromEnchantment<StranglingVineEnchantment>(),
        HoverTipFactory.FromPower<PoisonPower>(),
        HoverTipFactory.FromPower<LibraryBindingPower>()
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(SnowWhiteApplePageRelic.StranglingVineSelectionMax),
        new DynamicVar("Binding", SnowWhiteApplePageRelic.StranglingVineBinding),
        new DynamicVar("BindingTurns", SnowWhiteApplePageRelic.StranglingVineBindingTurns),
        new DynamicVar("Poison", SnowWhiteApplePageRelic.PoisonStingBarrierPoison),
        new DynamicVar("TurnInterval", SnowWhiteApplePageRelic.PoisonStingBarrierTurnInterval),
        new DynamicVar("HealPercent", SnowWhiteApplePageRelic.PoisonStingBarrierHealPercent),
        new DynamicVar("MinDamage", SnowWhiteApplePageRelic.MaliceMinDamage),
        new DynamicVar("MaxDamage", SnowWhiteApplePageRelic.MaliceMaxDamage),
        new DynamicVar("MaxHpThresholdPercent", SnowWhiteApplePageRelic.MaliceMaxDamageHpThresholdPercent),
        new DynamicVar("FullHpPercent", 100)
    ];
}
