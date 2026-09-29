using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using LibraryOfRuina.encounters;
using LibraryOfRuina.framework.audio;

namespace LibraryOfRuina.content.abnormalities.TodaysShyLook;

public sealed class TodaysShyLookStrong : EncounterModel, IEncounterBgmSource
{
    EncounterBgmConfig IEncounterBgmSource.Bgm => EncounterBgmConfig.RoundBased(
        "TodaysShyLookBGM",
        GuestReceptionPoolRegistry.LiteratureReceptionFloorBgmTracks,
        volumeScale: 0.85f,
        GuestReceptionPoolRegistry.StandardRoundThresholds);

    public override RoomType RoomType => RoomType.Monster;

    public override bool HasScene => true;

    public override IReadOnlyList<string> Slots => new[] { "shy_look" };

    protected override bool HasCustomBackground => true;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<TodaysShyLook>()
    ];

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        return
        [
            (ModelDb.Monster<TodaysShyLook>().ToMutable(), "shy_look")
        ];
    }
}
