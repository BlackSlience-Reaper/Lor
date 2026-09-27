using LibraryOfRuina.helpers;
using LibraryOfRuina.relics.KingOfGreed;
using LibraryOfRuina.relics.NaturalFloorLiberation;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models;

namespace LibraryOfRuina.cards.KingOfGreed;

public abstract class KingOfGreedPageChoiceCardBase : CardModel
{
    public const string IndulgenceChoiceId = "KING_OF_GREED_INDULGENCE_CHOICE_CARD";
    public const string HappinessPathChoiceId = "KING_OF_GREED_HAPPINESS_PATH_CHOICE_CARD";
    public const string GreedChoiceId = "KING_OF_GREED_GREED_CHOICE_CARD";

    protected abstract string PortraitFileName { get; }

    public override int MaxUpgradeLevel => 1;

    public override bool CanBeGeneratedInCombat => false;

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

    public override string PortraitPath =>
        ImageHelper.GetImagePath($"packed/card_portraits/colorless/{PortraitFileName}");

    public override IEnumerable<string> AllPortraitPaths => [PortraitPath];

    protected KingOfGreedPageChoiceCardBase()
        : base(-1, CardType.Skill, CardRarity.Ancient, TargetType.None, shouldShowInCardLibrary: false)
    {
    }

    public static bool IsKingOfGreedPageChoiceCard(CardModel? card)
    {
        string? id = card?.Id.Entry;
        return id is IndulgenceChoiceId or HappinessPathChoiceId or GreedChoiceId;
    }
}

[CardPool(typeof(TokenCardPool))]
public sealed class KingOfGreedIndulgenceChoiceCard : KingOfGreedPageChoiceCardBase
{
    protected override string PortraitFileName => "king_of_greed_indulgence.png";
}

[CardPool(typeof(TokenCardPool))]
public sealed class KingOfGreedHappinessPathChoiceCard : KingOfGreedPageChoiceCardBase
{
    protected override string PortraitFileName => "king_of_greed_happiness_path.png";
}

[CardPool(typeof(TokenCardPool))]
public sealed class KingOfGreedGreedChoiceCard : KingOfGreedPageChoiceCardBase
{
    protected override string PortraitFileName => "king_of_greed_greed.png";
}
