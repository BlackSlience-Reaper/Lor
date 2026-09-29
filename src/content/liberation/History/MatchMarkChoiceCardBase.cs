using LibraryOfRuina.framework.cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace LibraryOfRuina.content.liberation.History;

public abstract class MatchMarkChoiceCardBase : PageChoiceCard<MatchMarkMode>
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<LibraryBurnPower>(),
        ..HoverTipFactory.FromEnchantment<MatchFlameEnchantment>()
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("EmberBurn", MatchMarkRelic.EmberBurnStacks),
        new DynamicVar("PreventedHpLoss", MatchMarkRelic.EmberHpLossReduction),
        new DynamicVar("FootstepsBurn", MatchMarkRelic.FootstepsBurnStacks),
        new CardsVar(MatchMarkRelic.AfterglowSelectionMax)
    ];
}
