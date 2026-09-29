using LibraryOfRuina.framework.cards;
using LibraryOfRuina.infra.helpers;
using LibraryOfRuina.relics.WarmheartedWoodsman;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.cards.WarmheartedWoodsman;

public abstract class WarmheartedWoodsmanPageChoiceCardBase : PageChoiceCard<WarmheartedWoodsmanPageMode>
{
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
}

[CardPool(typeof(TokenCardPool))]
public sealed class WarmheartedWoodsmanWarmHeartChoiceCard : WarmheartedWoodsmanPageChoiceCardBase
{
    public override WarmheartedWoodsmanPageMode PageMode => WarmheartedWoodsmanPageMode.WarmHeart;

    protected override string PortraitFileName => "warmhearted_woodsman_warm_heart_choice_card.png";
}

[CardPool(typeof(TokenCardPool))]
public sealed class WarmheartedWoodsmanHeartChoiceCard : WarmheartedWoodsmanPageChoiceCardBase
{
    public override WarmheartedWoodsmanPageMode PageMode => WarmheartedWoodsmanPageMode.Heart;

    protected override string PortraitFileName => "warmhearted_woodsman_heart_choice_card.png";
}

[CardPool(typeof(TokenCardPool))]
public sealed class WarmheartedWoodsmanLoggingChoiceCard : WarmheartedWoodsmanPageChoiceCardBase
{
    public override WarmheartedWoodsmanPageMode PageMode => WarmheartedWoodsmanPageMode.Logging;

    protected override string PortraitFileName => "warmhearted_woodsman_logging_choice_card.png";
}
