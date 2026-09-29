using System.Linq;
using LibraryOfRuina.monsters.SpiderBud;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.encounters.SpiderBud;

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
        ModelDb.Monster<monsters.SpiderBud.SpiderBud>(),
        ModelDb.Monster<SpiderBudSmallSpider>()
    ];

    public override IEnumerable<string> ExtraAssetPaths
    {
        get
        {
            var paths = new List<string>
            {
                "res://images/backgrounds/spider_bud_strong/spider_bud_strong_background.png"
            };
            paths.AddRange(ModelDb.Monster<monsters.SpiderBud.SpiderBud>().AssetPaths);
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
            (ModelDb.Monster<monsters.SpiderBud.SpiderBud>().ToMutable(), "spider_bud")
        ];
    }
}
