using LibraryOfRuina.content.liberation.Natural;
using LibraryOfRuina.framework.cards;
using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace LibraryOfRuina.content.abnormalities.KingOfGreed;

public abstract class KingOfGreedPageChoiceCardBase : PageChoiceCard<KingOfGreedPageMode>
{
    public override int MaxUpgradeLevel => 1;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        IsUpgraded
            ? HoverTipFactory.FromPower<LibraryProtectionPower>()
            : HoverTipFactory.FromPower<LibraryEndurancePower>(),
        HoverTipFactory.Static(StaticHoverTip.Stun)
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Enhanced", 0),
        new DynamicVar("StunTurns", KingOfGreedEnhancedPageRelic.IndulgenceStunTurns),
        new DynamicVar("DamageEvents", KingOfGreedEnhancedPageRelic.HappinessDamageEvents),
        new DynamicVar("Protection", KingOfGreedEnhancedPageRelic.HappinessProtection),
        new DynamicVar("MaxProtection", KingOfGreedEnhancedPageRelic.HappinessLimit),
        new DynamicVar("BlockTurns", KingOfGreedPageRelic.IndulgenceRequiredTurns),
        new DynamicVar("MaxEndurance", KingOfGreedPageRelic.HappinessPathMaxEndurance),
        new DynamicVar("Turns", KingOfGreedPageRelic.HappinessPathTurns),
        new DynamicVar("MaxHpPercent", KingOfGreedPageRelic.GreedMaxHpLossPercent),
        new DynamicVar("LifestealPercent", KingOfGreedPageRelic.GreedLifestealPercent)
    ];

    protected override void OnUpgrade()
    {
        DynamicVars["Enhanced"].UpgradeValueBy(
            1);
        DynamicVars["BlockTurns"].UpgradeValueBy(
            KingOfGreedEnhancedPageRelic.IndulgenceTurns - DynamicVars["BlockTurns"].BaseValue);
        DynamicVars["Turns"].UpgradeValueBy(
            KingOfGreedEnhancedPageRelic.HappinessTurns - DynamicVars["Turns"].BaseValue);
        DynamicVars["LifestealPercent"].UpgradeValueBy(
            KingOfGreedEnhancedPageRelic.LifestealPercent - DynamicVars["LifestealPercent"].BaseValue);
    }
}

[CardPool(typeof(TokenCardPool))]
public sealed class KingOfGreedIndulgenceChoiceCard : KingOfGreedPageChoiceCardBase
{
    public override KingOfGreedPageMode PageMode => KingOfGreedPageMode.Indulgence;

    protected override string PortraitFileName => "king_of_greed_indulgence.png";
}

[CardPool(typeof(TokenCardPool))]
public sealed class KingOfGreedHappinessPathChoiceCard : KingOfGreedPageChoiceCardBase
{
    public override KingOfGreedPageMode PageMode => KingOfGreedPageMode.HappinessPath;

    protected override string PortraitFileName => "king_of_greed_happiness_path.png";
}

[CardPool(typeof(TokenCardPool))]
public sealed class KingOfGreedGreedChoiceCard : KingOfGreedPageChoiceCardBase
{
    public override KingOfGreedPageMode PageMode => KingOfGreedPageMode.Greed;

    protected override string PortraitFileName => "king_of_greed_greed.png";
}
