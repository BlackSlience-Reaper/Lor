using LibraryOfRuina.enchantments.HistoryFloorLiberation;
using LibraryOfRuina.relics.HistoryFloorLiberation;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace LibraryOfRuina.cards.HistoryFloorLiberation;

public abstract class SnowWhiteAppleChoiceCardBase : CardModel
{
    public const string StranglingVineChoiceId =
        "SNOW_WHITE_STRANGLING_VINE_CHOICE_CARD";
    public const string PoisonStingBarrierChoiceId =
        "SNOW_WHITE_POISON_STING_BARRIER_CHOICE_CARD";
    public const string MaliceChoiceId =
        "SNOW_WHITE_MALICE_CHOICE_CARD";

    protected abstract string PortraitFileName { get; }

    public override int MaxUpgradeLevel => 0;

    public override bool CanBeGeneratedInCombat => false;

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

    public override string PortraitPath =>
        ImageHelper.GetImagePath(
            $"packed/card_portraits/colorless/{PortraitFileName}");

    public override IEnumerable<string> AllPortraitPaths =>
    [
        PortraitPath
    ];

    protected SnowWhiteAppleChoiceCardBase()
        : base(
            -1,
            CardType.Skill,
            CardRarity.Ancient,
            TargetType.None,
            shouldShowInCardLibrary: false)
    {
    }

    public static bool IsSnowWhiteAppleChoiceCard(CardModel? card)
    {
        string? id = card?.Id.Entry;
        return id is StranglingVineChoiceId
            or PoisonStingBarrierChoiceId
            or MaliceChoiceId;
    }
}
