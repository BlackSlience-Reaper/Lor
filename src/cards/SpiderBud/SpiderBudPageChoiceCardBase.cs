using LibraryOfRuina.relics.SpiderBud;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace LibraryOfRuina.cards.SpiderBud;

public abstract class SpiderBudPageChoiceCardBase : CardModel
{
    protected abstract string PortraitFileName { get; }

    public override int MaxUpgradeLevel => 0;

    public override bool CanBeGeneratedInCombat => false;

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

    public override string PortraitPath =>
        ImageHelper.GetImagePath($"packed/card_portraits/colorless/{PortraitFileName}");

    protected SpiderBudPageChoiceCardBase()
        : base(-1, CardType.Skill, CardRarity.Ancient, TargetType.None, shouldShowInCardLibrary: false)
    {
    }
}
