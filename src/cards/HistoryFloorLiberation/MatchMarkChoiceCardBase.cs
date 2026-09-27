using LibraryOfRuina.enchantments.HistoryFloorLiberation;
using LibraryOfRuina.relics.HistoryFloorLiberation;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.cards.HistoryFloorLiberation;

public abstract class MatchMarkChoiceCardBase : CardModel
{
    public const string EmberChoiceId = "MATCH_MARK_EMBER_CHOICE_CARD";
    public const string FootstepsChoiceId = "MATCH_MARK_FOOTSTEPS_CHOICE_CARD";
    public const string AfterglowChoiceId = "MATCH_MARK_AFTERGLOW_CHOICE_CARD";

    protected abstract string PortraitFileName { get; }

    public override int MaxUpgradeLevel => 0;

    public override bool CanBeGeneratedInCombat => false;

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

    public override string PortraitPath => ImageHelper.GetImagePath($"packed/card_portraits/colorless/{PortraitFileName}");

    public override IEnumerable<string> AllPortraitPaths =>
    [
        PortraitPath
    ];

    protected MatchMarkChoiceCardBase()
        : base(-1, CardType.Skill, CardRarity.Ancient, TargetType.None, shouldShowInCardLibrary: false)
    {
    }

    public static bool IsMatchMarkChoiceCard(CardModel? card)
    {
        string? id = card?.Id.Entry;
        return id is EmberChoiceId or FootstepsChoiceId or AfterglowChoiceId;
    }
}
