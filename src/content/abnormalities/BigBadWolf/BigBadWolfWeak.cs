using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using LibraryOfRuina.encounters;
using LibraryOfRuina.framework.audio;

namespace LibraryOfRuina.content.abnormalities.BigBadWolf;

public sealed class BigBadWolfWeak : EncounterModel, IEncounterBgmSource
{
    EncounterBgmConfig IEncounterBgmSource.Bgm => EncounterBgmConfig.RoundBased(
        "BigBadWolfBGM",
        GuestReceptionPoolRegistry.LanguageReceptionFloorBgmTracks,
        volumeScale: 0.85f,
        GuestReceptionPoolRegistry.StandardRoundThresholds);

    public override RoomType RoomType => RoomType.Monster;

    public override bool IsWeak => true;

    public override bool HasScene => true;

    public override IReadOnlyList<string> Slots => new[] { "wolf" };

    protected override bool HasCustomBackground => true;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<BigBadWolf>()
    ];

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        return
        [
            (ModelDb.Monster<BigBadWolf>().ToMutable(), "wolf")
        ];
    }
}
