using LibraryOfRuina.relics.HappyTeddy;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace LibraryOfRuina.cards.HappyTeddy;

public abstract class HappyTeddyPageChoiceCardBase : CardModel
{
    public const string LongingEmbraceChoiceId = "HAPPY_TEDDY_LONGING_EMBRACE_CHOICE_CARD";
    public const string HappyMemoryChoiceId = "HAPPY_TEDDY_HAPPY_MEMORY_CHOICE_CARD";
    public const string ExpressAffectionChoiceId = "HAPPY_TEDDY_EXPRESS_AFFECTION_CHOICE_CARD";

    protected abstract string PortraitFileName { get; }

    public override int MaxUpgradeLevel => 0;

    public override bool CanBeGeneratedInCombat => false;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.Static(StaticHoverTip.Block)
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(HappyTeddyPageRelic.LongingEmbraceBlockGain, ValueProp.Unpowered),
        new DynamicVar("SlashPower", HappyTeddyPageRelic.LongingEmbraceSlashPower),
        new DynamicVar("CostReduction", HappyTeddyPageRelic.HappyMemoryCostReduction),
        new DynamicVar("TurnInterval", HappyTeddyPageRelic.ExpressAffectionTurnInterval),
        new DynamicVar("BlockPercent", HappyTeddyPageRelic.ExpressAffectionBlockPercent)
    ];

    public override string PortraitPath => ImageHelper.GetImagePath($"packed/card_portraits/colorless/{PortraitFileName}");

    public override IEnumerable<string> AllPortraitPaths =>
    [
        PortraitPath
    ];

    protected HappyTeddyPageChoiceCardBase()
        : base(-1, CardType.Skill, CardRarity.Ancient, TargetType.None, shouldShowInCardLibrary: false)
    {
    }

    public static bool IsHappyTeddyPageChoiceCard(CardModel? card)
    {
        string? id = card?.Id.Entry;
        return id is LongingEmbraceChoiceId or HappyMemoryChoiceId or ExpressAffectionChoiceId;
    }
}
