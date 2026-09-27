using LibraryOfRuina.relics.DespairKnight;
using LibraryOfRuina.relics.NaturalFloorLiberation;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.cards.DespairKnight;

public abstract class DespairKnightPageChoiceCardBase() : CardModel(-1, CardType.Skill, CardRarity.Ancient,
    TargetType.None, shouldShowInCardLibrary: false)
{
    public const string BlessingChoiceId = "DESPAIR_KNIGHT_BLESSING_CHOICE_CARD";
    public const string DespairChoiceId = "DESPAIR_KNIGHT_DESPAIR_CHOICE_CARD";
    public const string TearSwordChoiceId = "DESPAIR_KNIGHT_TEAR_SWORD_CHOICE_CARD";

    protected abstract string PortraitFileName { get; }

    public override int MaxUpgradeLevel => 1;

    public override bool CanBeGeneratedInCombat => false;

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

    public override string PortraitPath =>
        ImageHelper.GetImagePath($"packed/card_portraits/colorless/{PortraitFileName}");

    public override IEnumerable<string> AllPortraitPaths => [PortraitPath];

    public static bool IsDespairKnightPageChoiceCard(CardModel? card)
    {
        string? id = card?.Id.Entry;
        return id is BlessingChoiceId or DespairChoiceId or TearSwordChoiceId;
    }
}
