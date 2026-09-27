using LibraryOfRuina.guests;
using LibraryOfRuina.guests.DawnOffice;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.encounters.KuroKumo;

public sealed class KuroKumoNormal : EncounterModel, IGuestReceptionEncounter
{
    public override RoomType RoomType => RoomType.Monster;

    public override bool HasScene => true;

    public override IReadOnlyList<string> Slots => new[] { "yang", "sayo", "gin" };

    protected override bool HasCustomBackground => true;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new MonsterModel[]
        {
            ModelDb.Monster<Yang>(),
            ModelDb.Monster<Sayo>(),
            ModelDb.Monster<Gin>()
        };

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        return new List<(MonsterModel, string?)>
        {
            (ModelDb.Monster<Yang>().ToMutable(), "yang"),
            (ModelDb.Monster<Sayo>().ToMutable(), "sayo"),
            (ModelDb.Monster<Gin>().ToMutable(), "gin")
        };
    }
}
