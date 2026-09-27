using LibraryOfRuina.helpers;
using LibraryOfRuina.monsters.ScaredyCat;
using LibraryOfRuina.relics.RoadHome;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Powers;

namespace LibraryOfRuina.cards.RoadHome;

public abstract class RoadHomePageChoiceCardBase : CardModel
{
    public const string CourageChoiceId = "ROAD_HOME_COURAGE_CHOICE_CARD";
    public const string CompanionRoadChoiceId = "ROAD_HOME_COMPANION_ROAD_CHOICE_CARD";
    public const string HomeChoiceId = "ROAD_HOME_HOME_CHOICE_CARD";

    public static readonly string[] PortraitResourcePaths =
    [
        "res://images/packed/card_portraits/colorless/road_home_courage_choice_card.png",
        "res://images/packed/card_portraits/colorless/road_home_companion_road_choice_card.png",
        "res://images/packed/card_portraits/colorless/road_home_home_choice_card.png"
    ];

    protected abstract string PortraitFileName { get; }

    public override int MaxUpgradeLevel => 0;

    public override bool CanBeGeneratedInCombat => false;

    public override string PortraitPath =>
        ImageHelper.GetImagePath($"packed/card_portraits/colorless/{PortraitFileName}");

    public override IEnumerable<string> AllPortraitPaths => [PortraitPath];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<ArtifactPower>(),
        HoverTipFactory.FromPower<BufferPower>(),
        HoverTipFactory.FromPower<StrengthPower>(),
        HoverTipFactory.FromPower<DexterityPower>()
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("MaxHpPercent", RoadHomePageRelic.CourageMaxHpPercent),
        new DynamicVar("CourageUses", RoadHomePageRelic.CourageTotalUses),
        new DynamicVar("BaseHp", ScaredyCatCompanion.BaseHp),
        new DynamicVar("Strength", RoadHomePageRelic.CompanionGrowthStrength),
        new DynamicVar("Dexterity", RoadHomePageRelic.CompanionGrowthDexterity),
        new DynamicVar("MaxHp", RoadHomePageRelic.CompanionGrowthMaxHp),
        new DynamicVar("Artifact", RoadHomePageRelic.HomeArtifact),
        new DynamicVar("Buffer", RoadHomePageRelic.HomeBuffer)
    ];

    protected RoadHomePageChoiceCardBase()
        : base(-1, CardType.Skill, CardRarity.Ancient, TargetType.None, shouldShowInCardLibrary: false)
    {
    }

    public static bool IsRoadHomePageChoiceCard(CardModel? card)
    {
        string? id = card?.Id.Entry;
        return id is CourageChoiceId or CompanionRoadChoiceId or HomeChoiceId;
    }
}

[CardPool(typeof(TokenCardPool))]
public sealed class RoadHomeCourageChoiceCard : RoadHomePageChoiceCardBase
{
    protected override string PortraitFileName => "road_home_courage_choice_card.png";
}

[CardPool(typeof(TokenCardPool))]
public sealed class RoadHomeCompanionRoadChoiceCard : RoadHomePageChoiceCardBase
{
    protected override string PortraitFileName => "road_home_companion_road_choice_card.png";
}

[CardPool(typeof(TokenCardPool))]
public sealed class RoadHomeHomeChoiceCard : RoadHomePageChoiceCardBase
{
    protected override string PortraitFileName => "road_home_home_choice_card.png";
}
