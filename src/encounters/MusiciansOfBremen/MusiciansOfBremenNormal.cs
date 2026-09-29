using LibraryOfRuina.guests;
using LibraryOfRuina.guests.MusiciansOfBremen;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.encounters.MusiciansOfBremen;

public sealed class MusiciansOfBremenNormal : EncounterModel, IGuestReceptionEncounter, IEncounterBgmSource
{
    EncounterBgmConfig IEncounterBgmSource.Bgm => EncounterBgmConfig.DeathBased(
        "MusiciansOfBremenBGM",
        "res://audio/bgm/musicians_of_bremen/musicians_of_bremen_battle_1.ogg",
        "res://audio/bgm/musicians_of_bremen/musicians_of_bremen_battle_2.ogg",
        "res://audio/bgm/musicians_of_bremen/musicians_of_bremen_battle_3.ogg");

    public override RoomType RoomType => RoomType.Monster;

    public override bool HasScene => true;

    public override IReadOnlyList<string> Slots => new[] { "mumu", "meow", "oink" };

    protected override bool HasCustomBackground => true;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new MonsterModel[]
        {
            ModelDb.Monster<MuMu>(),
            ModelDb.Monster<Meow>(),
            ModelDb.Monster<Oink>()
        };

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        return new List<(MonsterModel, string?)>
        {
            (ModelDb.Monster<MuMu>().ToMutable(), "mumu"),
            (ModelDb.Monster<Meow>().ToMutable(), "meow"),
            (ModelDb.Monster<Oink>().ToMutable(), "oink")
        };
    }
}
