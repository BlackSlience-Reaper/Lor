using LibraryOfRuina.framework.cards;
using LibraryOfRuina.infra.helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Powers;

namespace LibraryOfRuina.content.abnormalities.RoadHome;

public abstract class RoadHomePageChoiceCardBase : PageChoiceCard<RoadHomePageMode>
{
    public static readonly string[] PortraitResourcePaths =
    [
        RoadHomeAssets.CourageChoiceCardTexture,
        RoadHomeAssets.CompanionRoadChoiceCardTexture,
        RoadHomeAssets.HomeChoiceCardTexture
    ];

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
}

[CardPool(typeof(TokenCardPool))]
public sealed class RoadHomeCourageChoiceCard : RoadHomePageChoiceCardBase
{
    public override RoadHomePageMode PageMode => RoadHomePageMode.Courage;

    protected override string PortraitFileName => "road_home_courage_choice_card.png";
}

[CardPool(typeof(TokenCardPool))]
public sealed class RoadHomeCompanionRoadChoiceCard : RoadHomePageChoiceCardBase
{
    public override RoadHomePageMode PageMode => RoadHomePageMode.CompanionRoad;

    protected override string PortraitFileName => "road_home_companion_road_choice_card.png";
}

[CardPool(typeof(TokenCardPool))]
public sealed class RoadHomeHomeChoiceCard : RoadHomePageChoiceCardBase
{
    public override RoadHomePageMode PageMode => RoadHomePageMode.Home;

    protected override string PortraitFileName => "road_home_home_choice_card.png";
}
