using LibraryOfRuina.framework.cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;

namespace LibraryOfRuina.content.abnormalities.SpiderBud;

public abstract class SpiderBudPageChoiceCardBase : PageChoiceCard<SpiderBudPageMode>
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<PoisonPower>(),
        HoverTipFactory.FromPower<StrengthPower>(),
        HoverTipFactory.FromPower<DexterityPower>(),
        HoverTipFactory.FromPower<LibraryVulnerablePower>(),
        HoverTipFactory.FromPower<LibraryBreakVulnerablePower>()
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("DeckSizeThreshold", SpiderBudPageRelic.CocoonDeckSizeThreshold),
        new DynamicVar("Strength", SpiderBudPageRelic.CocoonStrength),
        new DynamicVar("Dexterity", SpiderBudPageRelic.CocoonDexterity),
        new HealVar(SpiderBudPageRelic.FeedingHeal),
        new DynamicVar("Poison", SpiderBudPageRelic.FeedingPoison),
        new DynamicVar("StrengthLoss", SpiderBudPageRelic.VigilanceStrengthLoss),
        new DynamicVar("Vulnerable", SpiderBudPageRelic.VigilancePermanentVulnerable)
    ];
}
