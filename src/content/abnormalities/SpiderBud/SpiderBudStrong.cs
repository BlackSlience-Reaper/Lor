using System.Linq;
using LibraryOfRuina.content.guests;
using LibraryOfRuina.framework.audio;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.content.abnormalities.SpiderBud;

public sealed class SpiderBudStrong : EncounterModel, IEncounterBgmSource
{
    EncounterBgmConfig IEncounterBgmSource.Bgm => EncounterBgmConfig.RoundBased(
        "SpiderBudBGM",
        GuestReceptionPoolRegistry.LiteratureReceptionFloorBgmTracks,
        volumeScale: 0.85f,
        GuestReceptionPoolRegistry.StandardRoundThresholds);

    public override RoomType RoomType => RoomType.Monster;

    public override bool HasScene => true;

    public override IReadOnlyList<string> Slots => ["left_spider", "right_spider", "spider_bud"];

    protected override bool HasCustomBackground => true;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<SpiderBud>(),
        ModelDb.Monster<SpiderBudSmallSpider>()
    ];

    public override IEnumerable<string> ExtraAssetPaths
    {
        get
        {
            var paths = new List<string>
            {
                SpiderBudAssets.StrongBackground
            };
            paths.AddRange(ModelDb.Monster<SpiderBud>().AssetPaths);
            paths.AddRange(ModelDb.Monster<SpiderBudSmallSpider>().AssetPaths);
            return paths.Distinct();
        }
    }

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        var leftSpider = (SpiderBudSmallSpider)ModelDb.Monster<SpiderBudSmallSpider>().ToMutable();
        leftSpider.IsLeftSpider = true;

        var rightSpider = (SpiderBudSmallSpider)ModelDb.Monster<SpiderBudSmallSpider>().ToMutable();
        rightSpider.IsLeftSpider = false;

        return
        [
            (leftSpider, "left_spider"),
            (rightSpider, "right_spider"),
            (ModelDb.Monster<SpiderBud>().ToMutable(), "spider_bud")
        ];
    }
}
