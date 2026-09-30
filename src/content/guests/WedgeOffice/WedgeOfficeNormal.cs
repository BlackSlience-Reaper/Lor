using LibraryOfRuina.content.guests.DawnOffice;
using LibraryOfRuina.framework.audio;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.content.guests.WedgeOffice;

public sealed class WedgeOfficeNormal : EncounterModel, IGuestReceptionEncounter, IEncounterBgmSource
{
    EncounterBgmConfig IEncounterBgmSource.Bgm => EncounterBgmConfig.RoundBased(
        "WedgeOfficeBGM",
        GuestReceptionPoolRegistry.GeneralReceptionFloorBgmTracks,
        volumeScale: 0.85f,
        GuestReceptionPoolRegistry.StandardRoundThresholds);

    public override RoomType RoomType => RoomType.Monster;

    public override bool HasScene => true;

    public override IReadOnlyList<string> Slots => new[] { "pameli", "oscar", "pamela", "philip" };

    protected override bool HasCustomBackground => true;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new MonsterModel[]
        {
            ModelDb.Monster<Pameli>(),
            ModelDb.Monster<Oscar>(),
            ModelDb.Monster<Pamela>(),
            ModelDb.Monster<Philip>()
        };

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        return new List<(MonsterModel, string?)>
        {
            (ModelDb.Monster<Pameli>().ToMutable(), "pameli"),
            (ModelDb.Monster<Oscar>().ToMutable(), "oscar"),
            (ModelDb.Monster<Pamela>().ToMutable(), "pamela"),
            (ModelDb.Monster<Philip>().ToMutable(), "philip")
        };
    }
}
