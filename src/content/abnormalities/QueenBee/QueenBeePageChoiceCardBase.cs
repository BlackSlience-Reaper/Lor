using LibraryOfRuina.content.liberation.History;
using LibraryOfRuina.framework.cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace LibraryOfRuina.content.abnormalities.QueenBee;

public abstract class QueenBeePageChoiceCardBase : PageChoiceCard<QueenBeePageMode>
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<HistoryFloorWaspSporePower>(),
        HoverTipFactory.FromPower<LibraryStrongPower>(),
        HoverTipFactory.FromPower<LibraryBleedingPower>(),
        HoverTipFactory.FromPower<LibraryBurnPower>(),
        HoverTipFactory.FromPower<QueenBeeThreatPower>()
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Mode", (int)QueenBeePageMode.None),
        new DynamicVar("SporeBurn", QueenBeePageRelic.SporeBurnAmount),
        new DynamicVar("SporeBleed", QueenBeePageRelic.SporeBleedAmount),
        new DynamicVar("ChaosDamage", QueenBeePageRelic.ThreatChaosDamage),
        new DynamicVar("HpLoss", QueenBeePageRelic.LoyaltyHpLossPerTrigger),
        new DynamicVar("Strength", QueenBeePageRelic.LoyaltyStrength),
        new DynamicVar("Turns", QueenBeePageRelic.LoyaltyTurns)
    ];
}
