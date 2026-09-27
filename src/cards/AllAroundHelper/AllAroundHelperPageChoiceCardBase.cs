using LibraryOfRuina.relics.AllAroundHelper;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace LibraryOfRuina.cards.AllAroundHelper;

public abstract class AllAroundHelperPageChoiceCardBase : CardModel
{
    public const string ChargeChoiceId = "ALL_AROUND_HELPER_CHARGE_CHOICE_CARD";
    public const string RecognitionFunctionChoiceId = "ALL_AROUND_HELPER_RECOGNITION_FUNCTION_CHOICE_CARD";
    public const string CleanChoiceId = "ALL_AROUND_HELPER_CLEAN_CHOICE_CARD";

    protected abstract string PortraitFileName { get; }

    public override int MaxUpgradeLevel => 0;

    public override bool CanBeGeneratedInCombat => false;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        EnergyHoverTip,
        HoverTipFactory.FromPower<DrawCardsNextTurnPower>()
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("HandThreshold", AllAroundHelperPageRelic.ChargeHandThreshold),
        new EnergyVar(AllAroundHelperPageRelic.ChargeEnergyNextTurn),
        new CardsVar(AllAroundHelperPageRelic.RecognitionCardsPerSwift),
        new DynamicVar("Swift", AllAroundHelperPageRelic.RecognitionSwift)
    ];

    public override string PortraitPath => ImageHelper.GetImagePath($"packed/card_portraits/colorless/{PortraitFileName}");

    public override IEnumerable<string> AllPortraitPaths =>
    [
        PortraitPath
    ];

    protected AllAroundHelperPageChoiceCardBase()
        : base(-1, CardType.Skill, CardRarity.Ancient, TargetType.None, shouldShowInCardLibrary: false)
    {
    }

    public static bool IsAllAroundHelperPageChoiceCard(CardModel? card)
    {
        string? id = card?.Id.Entry;
        return id is ChargeChoiceId or RecognitionFunctionChoiceId or CleanChoiceId;
    }
}
