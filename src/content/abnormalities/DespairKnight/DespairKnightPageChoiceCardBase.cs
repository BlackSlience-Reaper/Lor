using LibraryOfRuina.content.liberation.Natural;
using LibraryOfRuina.framework.cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Enchantments;

namespace LibraryOfRuina.content.abnormalities.DespairKnight;

public abstract class DespairKnightPageChoiceCardBase : PageChoiceCard<DespairKnightPageMode>
{
    public override int MaxUpgradeLevel => 1;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        IsUpgraded
            ? HoverTipFactory.FromPower<LibraryProtectionPower>()
            : HoverTipFactory.FromPower<LibraryEndurancePower>(),
        HoverTipFactory.FromPower<LibraryStrongPower>(),
        HoverTipFactory.FromPower<LibraryProtectionPower>(),
        ..HoverTipFactory.FromEnchantment<Sharp>(IsUpgraded
            ? DespairKnightEnhancedPageRelic.TearSwordSharpAmount
            : DespairKnightPageRelic.TearSwordSharpAmount)
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Enhanced", 0),
        new DynamicVar("Guard", DespairKnightPageRelic.BlessingGuard),
        new DynamicVar("Turns", DespairKnightPageRelic.BlessingTurns),
        new DynamicVar("Strength", DespairKnightPageRelic.DespairStrength),
        new DynamicVar("PermanentGuard", DespairKnightPageRelic.DespairGuard),
        new CardsVar(DespairKnightPageRelic.TearSwordEnchantCards),
        new DynamicVar("SharpAmount", DespairKnightPageRelic.TearSwordSharpAmount),
        new DynamicVar("MaxHpPercent", DespairKnightPageRelic.TearSwordMaxHpPercent)
    ];

    protected override void OnUpgrade()
    {
        DynamicVars["Enhanced"].UpgradeValueBy(
            1);
        DynamicVars["Guard"].UpgradeValueBy(
            DespairKnightEnhancedPageRelic.BlessingGuard - DynamicVars["Guard"].BaseValue);
        DynamicVars["Strength"].UpgradeValueBy(
            DespairKnightEnhancedPageRelic.DespairStrength - DynamicVars["Strength"].BaseValue);
        DynamicVars["PermanentGuard"].UpgradeValueBy(
            DespairKnightEnhancedPageRelic.DespairGuard - DynamicVars["PermanentGuard"].BaseValue);
        DynamicVars["Cards"].UpgradeValueBy(
            DespairKnightEnhancedPageRelic.TearSwordEnchantCards - DynamicVars["Cards"].BaseValue);
        DynamicVars["SharpAmount"].UpgradeValueBy(
            DespairKnightEnhancedPageRelic.TearSwordSharpAmount - DynamicVars["SharpAmount"].BaseValue);
        DynamicVars["MaxHpPercent"].UpgradeValueBy(
            DespairKnightEnhancedPageRelic.TearSwordMaxHpPercent - DynamicVars["MaxHpPercent"].BaseValue);
    }
}
