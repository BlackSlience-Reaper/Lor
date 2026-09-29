using LibraryOfRuina.framework.audio;
using LibraryOfRuina.guests;
using LibraryOfRuina.guests.DawnOffice;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.encounters.DawnOffice;

public sealed class DawnOfficeNormal : EncounterModel, IGuestReceptionEncounter, IEncounterBgmSource
{
    EncounterBgmConfig IEncounterBgmSource.Bgm => EncounterBgmConfig.DeathBased(
        "DawnOfficeBGM",
        "res://audio/bgm/dawn_office/dawn_office_battle_1.ogg",
        "res://audio/bgm/dawn_office/dawn_office_battle_2.ogg",
        "res://audio/bgm/dawn_office/dawn_office_battle_3.ogg");

    public override RoomType RoomType => RoomType.Monster;

    public override bool HasScene => true;

    public override IReadOnlyList<string> Slots => new[] { "salvador", "yuna", "philip" };

    protected override bool HasCustomBackground => true;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new MonsterModel[]
        {
            ModelDb.Monster<Salvador>(),
            ModelDb.Monster<Yuna>(),
            ModelDb.Monster<Philip>()
        };

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        return new List<(MonsterModel, string?)>
        {
            (ModelDb.Monster<Salvador>().ToMutable(), "salvador"),
            (ModelDb.Monster<Yuna>().ToMutable(), "yuna"),
            (ModelDb.Monster<Philip>().ToMutable(), "philip")
        };
    }
}
