using LibraryOfRuina.guests;
using LibraryOfRuina.guests.HookOffice;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.encounters.HookOffice;

public sealed class HookOfficeStrong : EncounterModel, IGuestReceptionEncounter, IEncounterBgmSource
{
    EncounterBgmConfig IEncounterBgmSource.Bgm => EncounterBgmConfig.RoundBased(
        "HookOfficeBGM",
        GuestReceptionPoolRegistry.GeneralReceptionFloorBgmTracks,
        volumeScale: 0.85f,
        GuestReceptionPoolRegistry.StandardRoundThresholds);

    public override RoomType RoomType => RoomType.Monster;

    public override bool HasScene => true;

    public override IReadOnlyList<string> Slots => new[] { "taein", "mccullin", "naoki" };

    protected override bool HasCustomBackground => true;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new MonsterModel[]
        {
            ModelDb.Monster<Taein>(),
            ModelDb.Monster<Mccullin>(),
            ModelDb.Monster<Naoki>()
        };

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        return new List<(MonsterModel, string?)>
        {
            (ModelDb.Monster<Taein>().ToMutable(), "taein"),
            (ModelDb.Monster<Mccullin>().ToMutable(), "mccullin"),
            (ModelDb.Monster<Naoki>().ToMutable(), "naoki")
        };
    }
}
