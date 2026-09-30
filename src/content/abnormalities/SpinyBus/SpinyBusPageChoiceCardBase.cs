using LibraryOfRuina.framework.cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace LibraryOfRuina.content.abnormalities.SpinyBus;

public abstract class SpinyBusPageChoiceCardBase : PageChoiceCard<SpinyBusPageMode>
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<LibraryDisarmPower>(),
        HoverTipFactory.FromPower<LibraryStrongPower>()
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Damage", SpinyBusPageRelic.ThornsDamageBonus),
        new DynamicVar("Flaw", SpinyBusPageRelic.PleasureFlawStacks),
        new DynamicVar("Strong", SpinyBusPageRelic.PleasureStrongStacks),
        new DynamicVar("Turns", SpinyBusPageRelic.PleasureTurns),
        new HealVar(SpinyBusPageRelic.LaughingPowderHeal)
    ];
}
