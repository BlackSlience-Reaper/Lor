using ActLikeIt2;
using Godot;
using LibraryOfRuina.content.liberation.Art;
using LibraryOfRuina.content.liberation.History;
using LibraryOfRuina.content.liberation.Language;
using LibraryOfRuina.content.liberation.Literature;
using LibraryOfRuina.content.liberation.Natural;
using LibraryOfRuina.content.liberation.Philosophy;
using LibraryOfRuina.content.liberation.Social;
using LibraryOfRuina.content.liberation.Technology;
using LibraryOfRuina.patches;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;

namespace LibraryOfRuina.content.acts;

internal enum LibraryActFamily
{
    First,
    Second,
    Third
}

/// <summary>
/// Shared implementation for the Library acts exposed through ActLikeIt2.
/// Each Act reuses its template's non-monster data while the Library owns the complete
/// encounter roster and fixed liberation boss.
/// </summary>
public abstract class LibraryOfRuinaActModel : TemplateActModel
{
    internal abstract string MapTopBackground { get; }

    internal abstract string MapMiddleBackground { get; }

    internal abstract string MapBottomBackground { get; }

    public abstract override ActModel TemplateAct { get; }

    protected abstract EncounterModel FixedBoss { get; }

    internal virtual Color BossMapTraveledColor => MapTraveledColor;

    internal virtual Color BossMapUntraveledColor => MapUntraveledColor;

    internal virtual Color BossMapOutlineColor => MapBgColor;

    internal EncounterModel ExpectedBoss => FixedBoss;

    internal LibraryActFamily Family => Index switch
    {
        0 => LibraryActFamily.First,
        1 => LibraryActFamily.Second,
        _ => LibraryActFamily.Third
    };

    public override IEnumerable<EncounterModel> BossDiscoveryOrder => [FixedBoss];

    public override IEnumerable<EventModel> AllEvents =>
        LibraryOfRuinaEventPool.ForLibraryAct(Family, base.AllEvents);

    public override IEnumerable<EncounterModel> GenerateAllEncounters() =>
        LibraryActEncounterPools.Build(Family, FixedBoss);

    public override bool IsUnlocked(UnlockState unlockState) => true;

    internal static bool IsLibraryAct(ActModel? act) =>
        act is LibraryOfRuinaActModel;

    internal static bool IsFirstFamily(ActModel? act) =>
        act is Malkuth or Yesod or Hod;

    internal static bool IsSecondFamily(ActModel? act) => act is NetZech or Gebura or Tiphereth;

    internal static bool IsThirdFamily(ActModel? act) => act is Chesed or Binah;

    internal static bool IsFirstFamily(IRunState? runState) =>
        runState != null && IsFirstFamily(runState.Act);

    internal static bool IsSecondFamily(IRunState? runState) =>
        runState != null && IsSecondFamily(runState.Act);

    internal static bool IsThirdFamily(IRunState? runState) =>
        runState != null && IsThirdFamily(runState.Act);
}

public sealed class Malkuth : LibraryOfRuinaActModel
{
    internal const string MapTopBackgroundPath =
        "res://images/packed/map/map_bgs/malkuth/map_top_malkuth.png";

    internal const string MapMiddleBackgroundPath =
        "res://images/packed/map/map_bgs/malkuth/map_middle_malkuth.png";

    internal const string MapBottomBackgroundPath =
        "res://images/packed/map/map_bgs/malkuth/map_bottom_malkuth.png";

    internal override string MapTopBackground => MapTopBackgroundPath;

    internal override string MapMiddleBackground => MapMiddleBackgroundPath;

    internal override string MapBottomBackground => MapBottomBackgroundPath;

    public override ActModel TemplateAct => ModelDb.Act<Overgrowth>();

    public override Color MapTraveledColor => new("30210F");

    public override Color MapUntraveledColor => new("715234");

    // 历史层节点描边贴近黄褐纸色，避免浅色外圈在地图上过亮。
    public override Color MapBgColor => new("B18D5D");

    protected override EncounterModel FixedBoss =>
        ModelDb.Encounter<HistoryFloorLiberationEncounter>();
}

public sealed class Yesod : LibraryOfRuinaActModel
{
    internal const string MapTopBackgroundPath =
        "res://images/packed/map/map_bgs/yesod/map_top_yesod.png";

    internal const string MapMiddleBackgroundPath =
        "res://images/packed/map/map_bgs/yesod/map_middle_yesod.png";

    internal const string MapBottomBackgroundPath =
        "res://images/packed/map/map_bgs/yesod/map_bottom_yesod.png";

    internal override string MapTopBackground => MapTopBackgroundPath;

    internal override string MapMiddleBackground => MapMiddleBackgroundPath;

    internal override string MapBottomBackground => MapBottomBackgroundPath;

    public override ActModel TemplateAct => ModelDb.Act<Underdocks>();

    public override Color MapTraveledColor => new("30336B");

    public override Color MapUntraveledColor => new("575078");

    // 科技层节点描边贴近灰紫纸色，避免浅色外圈在地图上过亮。
    public override Color MapBgColor => new("9B95B2");

    protected override EncounterModel FixedBoss =>
        ModelDb.Encounter<TechnologyFloorLiberationEncounter>();
}

public sealed class Hod : LibraryOfRuinaActModel
{
    internal const string MapTopBackgroundPath =
        "res://images/packed/map/map_bgs/hod/map_top_hod.png";

    internal const string MapMiddleBackgroundPath =
        "res://images/packed/map/map_bgs/hod/map_middle_hod.png";

    internal const string MapBottomBackgroundPath =
        "res://images/packed/map/map_bgs/hod/map_bottom_hod.png";

    internal override string MapTopBackground => MapTopBackgroundPath;

    internal override string MapMiddleBackground => MapMiddleBackgroundPath;

    internal override string MapBottomBackground => MapBottomBackgroundPath;

    public override ActModel TemplateAct => ModelDb.Act<Overgrowth>();

    public override Color MapTraveledColor => new("3B2118");

    public override Color MapUntraveledColor => new("795039");

    // 文学层节点描边贴近暖褐纸色，避免浅色外圈在地图上过亮。
    public override Color MapBgColor => new("B08764");

    protected override EncounterModel FixedBoss =>
        ModelDb.Encounter<LiteratureFloorLiberationEncounter>();
}

public sealed class NetZech : LibraryOfRuinaActModel
{
    internal const string MapTopBackgroundPath =
        "res://images/packed/map/map_bgs/netzach/map_top_netzach.png";

    internal const string MapMiddleBackgroundPath =
        "res://images/packed/map/map_bgs/netzach/map_middle_netzach.png";

    internal const string MapBottomBackgroundPath =
        "res://images/packed/map/map_bgs/netzach/map_bottom_netzach.png";

    internal override string MapTopBackground => MapTopBackgroundPath;

    internal override string MapMiddleBackground => MapMiddleBackgroundPath;

    internal override string MapBottomBackground => MapBottomBackgroundPath;

    public override ActModel TemplateAct => ModelDb.Act<Hive>();

    public override Color MapTraveledColor => new("02361B");

    public override Color MapUntraveledColor => new("355940");

    // 艺术层节点描边贴近统一后的橄榄绿纸色，避免浅色外圈过亮。
    public override Color MapBgColor => new("809069");

    protected override EncounterModel FixedBoss =>
        ModelDb.Encounter<ArtFloorLiberationEncounter>();
}

public sealed class Gebura : LibraryOfRuinaActModel
{
    internal const string MapTopBackgroundPath =
        "res://images/packed/map/map_bgs/gebura/map_top_gebura.png";

    internal const string MapMiddleBackgroundPath =
        "res://images/packed/map/map_bgs/gebura/map_middle_gebura.png";

    internal const string MapBottomBackgroundPath =
        "res://images/packed/map/map_bgs/gebura/map_bottom_gebura.png";

    internal override string MapTopBackground => MapTopBackgroundPath;

    internal override string MapMiddleBackground => MapMiddleBackgroundPath;

    internal override string MapBottomBackground => MapBottomBackgroundPath;

    public override ActModel TemplateAct => ModelDb.Act<Hive>();

    public override Color MapTraveledColor => new("520A09");

    public override Color MapUntraveledColor => new("69392F");

    // 语言层节点描边贴近暗砖红纸色，避免浅色外圈在地图上过亮。
    public override Color MapBgColor => new("915549");

    protected override EncounterModel FixedBoss =>
        ModelDb.Encounter<LanguageFloorLiberationEncounter>();
}

public sealed class Tiphereth : LibraryOfRuinaActModel
{
    internal const string MapTopBackgroundPath =
        "res://images/packed/map/map_bgs/tiphereth/map_top_tiphereth.png";

    internal const string MapMiddleBackgroundPath =
        "res://images/packed/map/map_bgs/tiphereth/map_middle_tiphereth.png";

    internal const string MapBottomBackgroundPath =
        "res://images/packed/map/map_bgs/tiphereth/map_bottom_tiphereth.png";

    internal override string MapTopBackground => MapTopBackgroundPath;

    internal override string MapMiddleBackground => MapMiddleBackgroundPath;

    internal override string MapBottomBackground => MapBottomBackgroundPath;

    public override ActModel TemplateAct => ModelDb.Act<Hive>();

    public override Color MapTraveledColor => new("6B3C05");

    public override Color MapUntraveledColor => new("8A602E");

    // 自然层节点描边贴近沙金纸色，避免浅色外圈在地图上过亮。
    public override Color MapBgColor => new("CAB07F");

    protected override EncounterModel FixedBoss => ModelDb.Encounter<NaturalFloorLiberationEncounter>();
}

public sealed class Chesed : LibraryOfRuinaActModel
{
    internal const string MapTopBackgroundPath =
        "res://images/packed/map/map_bgs/chesed/map_top_chesed.png";

    internal const string MapMiddleBackgroundPath =
        "res://images/packed/map/map_bgs/chesed/map_middle_chesed.png";

    internal const string MapBottomBackgroundPath =
        "res://images/packed/map/map_bgs/chesed/map_bottom_chesed.png";

    internal override string MapTopBackground => MapTopBackgroundPath;

    internal override string MapMiddleBackground => MapMiddleBackgroundPath;

    internal override string MapBottomBackground => MapBottomBackgroundPath;

    public override ActModel TemplateAct => ModelDb.Act<Glory>();

    public override Color MapTraveledColor => new("182866");

    public override Color MapUntraveledColor => new("365D78");

    // 社会层节点描边贴近蓝灰纸色，避免浅色外圈在地图上过亮。
    public override Color MapBgColor => new("7892A9");

    protected override EncounterModel FixedBoss =>
        ModelDb.Encounter<SocialFloorLiberationEncounter>();
}

public sealed class Binah : LibraryOfRuinaActModel
{
    internal const string MapTopBackgroundPath =
        "res://images/packed/map/map_bgs/binah/map_top_binah.png";

    internal const string MapMiddleBackgroundPath =
        "res://images/packed/map/map_bgs/binah/map_middle_binah.png";

    internal const string MapBottomBackgroundPath =
        "res://images/packed/map/map_bgs/binah/map_bottom_binah.png";

    internal override string MapTopBackground => MapTopBackgroundPath;

    internal override string MapMiddleBackground => MapMiddleBackgroundPath;

    internal override string MapBottomBackground => MapBottomBackgroundPath;

    public override ActModel TemplateAct => ModelDb.Act<Glory>();

    // 哲学层深色纸面使用旧金色节点线条，保持可通行状态清晰可辨。
    public override Color MapTraveledColor => new("D2B77C");

    public override Color MapUntraveledColor => new("A68C5E");

    // 节点描边贴近提亮后的棕褐纸色，避免亮金色外圈掩盖线条。
    public override Color MapBgColor => new("645540");

    protected override EncounterModel FixedBoss =>
        ModelDb.Encounter<PhilosophyFloorLiberationEncounter>();
}
