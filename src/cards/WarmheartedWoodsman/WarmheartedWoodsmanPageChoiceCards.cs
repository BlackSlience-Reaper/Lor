using LibraryOfRuina.helpers;
using LibraryOfRuina.relics.WarmheartedWoodsman;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.WarmheartedWoodsman;

public abstract class WarmheartedWoodsmanPageChoiceCardBase : CardModel
{
    public const string WarmHeartChoiceId = "WARMHEARTED_WOODSMAN_WARM_HEART_CHOICE_CARD";
    public const string HeartChoiceId = "WARMHEARTED_WOODSMAN_HEART_CHOICE_CARD";
    public const string LoggingChoiceId = "WARMHEARTED_WOODSMAN_LOGGING_CHOICE_CARD";

    protected abstract string PortraitFileName { get; }

    public override int MaxUpgradeLevel => 0;

    public override bool CanBeGeneratedInCombat => false;

    public override string PortraitPath =>
        ImageHelper.GetImagePath($"packed/card_portraits/colorless/{PortraitFileName}");

    public override IEnumerable<string> AllPortraitPaths => [PortraitPath];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.ForEnergy(this),
        HoverTipFactory.FromPower<LibraryStrongPower>()
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("EnergyThreshold", WarmheartedWoodsmanPageRelic.WarmHeartEnergyThreshold),
        new DynamicVar("Strong", WarmheartedWoodsmanPageRelic.WarmHeartStrongStacks),
        new DynamicVar("Turns", WarmheartedWoodsmanPageRelic.OneTurnDuration),
        new DynamicVar("HpLoss", WarmheartedWoodsmanPageRelic.HeartHpLossThreshold),
        new EnergyVar(WarmheartedWoodsmanPageRelic.HeartEnergyReduction),
        new DynamicVar("EnergyReductionPerTurn", WarmheartedWoodsmanPageRelic.EnergyReductionPerTurn),
        new DynamicVar("HpGap", WarmheartedWoodsmanPageRelic.LoggingHpGapPerTrigger),
        new DynamicVar("LoggingStrong", WarmheartedWoodsmanPageRelic.LoggingStrongPerGap),
        new DynamicVar("LoggingMaxStrong", WarmheartedWoodsmanPageRelic.LoggingMaxStrong)
    ];

    protected WarmheartedWoodsmanPageChoiceCardBase()
        : base(-1, CardType.Skill, CardRarity.Ancient, TargetType.None, shouldShowInCardLibrary: false)
    {
    }

    public static bool IsWarmheartedWoodsmanPageChoiceCard(CardModel? card)
    {
        string? id = card?.Id.Entry;
        return id is WarmHeartChoiceId or HeartChoiceId or LoggingChoiceId;
    }
}

[CardPool(typeof(TokenCardPool))]
public sealed class WarmheartedWoodsmanWarmHeartChoiceCard : WarmheartedWoodsmanPageChoiceCardBase
{
    protected override string PortraitFileName => "warmhearted_woodsman_warm_heart_choice_card.png";
}

[CardPool(typeof(TokenCardPool))]
public sealed class WarmheartedWoodsmanHeartChoiceCard : WarmheartedWoodsmanPageChoiceCardBase
{
    protected override string PortraitFileName => "warmhearted_woodsman_heart_choice_card.png";
}

[CardPool(typeof(TokenCardPool))]
public sealed class WarmheartedWoodsmanLoggingChoiceCard : WarmheartedWoodsmanPageChoiceCardBase
{
    protected override string PortraitFileName => "warmhearted_woodsman_logging_choice_card.png";
}
