using LibraryOfRuina.framework.audio;
using LibraryOfRuina.monsters.HappyTeddy;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.encounters.HappyTeddy;

public sealed class HappyTeddyWeak : EncounterModel, IEncounterBgmSource
{
    EncounterBgmConfig IEncounterBgmSource.Bgm => EncounterBgmConfig.RoundBased(
        "HappyTeddyBGM",
        new[]
        {
            "res://audio/bgm/scorched_girl/scorched_girl_battle_1.ogg",
            "res://audio/bgm/scorched_girl/scorched_girl_battle_2.ogg",
            "res://audio/bgm/scorched_girl/scorched_girl_battle_3.ogg"
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
