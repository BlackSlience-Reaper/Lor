using LibraryOfRuina.content.guests;
using LibraryOfRuina.framework.audio;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.content.abnormalities.Alriune;

public sealed class AlriuneStrong : EncounterModel, IEncounterBgmSource
{
    public const string DustbornSlot = "dustborn_front";
    public const string AlriuneSlot = "alriune_back";

    EncounterBgmConfig IEncounterBgmSource.Bgm => EncounterBgmConfig.RoundBased(
        "AlriuneBGM",
        GuestReceptionPoolRegistry.NetzachReceptionFloorBgmTracks,
        volumeScale: 1f,
        GuestReceptionPoolRegistry.StandardRoundThresholds);

    public override RoomType RoomType => RoomType.Monster;

    public override bool IsWeak => false;

    public override bool HasScene => true;

    protected override bool HasCustomBackground => true;

    public override IReadOnlyList<string> Slots => [DustbornSlot, AlriuneSlot];

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<AlriuneDustborn>(),
        ModelDb.Monster<Alriune>()
    ];

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters() =>
    [
        (ModelDb.Monster<AlriuneDustborn>().ToMutable(), DustbornSlot),
        (ModelDb.Monster<Alriune>().ToMutable(), AlriuneSlot)
    ];

    public override float GetCameraScaling() => 0.9f;
}
