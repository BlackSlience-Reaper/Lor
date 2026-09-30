using LibraryOfRuina.framework.audio;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.content.abnormalities.HappyTeddy;

public sealed class HappyTeddyWeak : EncounterModel, IEncounterBgmSource
{
    EncounterBgmConfig IEncounterBgmSource.Bgm => EncounterBgmConfig.RoundBased(
        "HappyTeddyBGM",
        new[]
        {
            HappyTeddyAssets.ScorchedGirlBattle1Bgm,
            HappyTeddyAssets.ScorchedGirlBattle2Bgm,
            HappyTeddyAssets.ScorchedGirlBattle3Bgm
        },
        volumeScale: 0.85f,
        4,
        7);

    public override RoomType RoomType => RoomType.Monster;

    public override bool IsWeak => true;

    public override bool HasScene => true;

    public override IReadOnlyList<string> Slots => new[] { "teddy" };

    protected override bool HasCustomBackground => true;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<HappyTeddyMonster>()
    ];

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        return
        [
            (ModelDb.Monster<HappyTeddyMonster>().ToMutable(), "teddy")
        ];
    }
}
