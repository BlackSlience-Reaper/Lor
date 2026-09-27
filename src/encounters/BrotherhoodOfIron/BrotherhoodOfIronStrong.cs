using LibraryOfRuina.guests;
using LibraryOfRuina.guests.MusiciansOfBremen;
using LibraryOfRuina.guests.WedgeOffice;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.encounters.BrotherhoodOfIron;

public sealed class BrotherhoodOfIronStrong : EncounterModel, IGuestReceptionEncounter
{
    public override RoomType RoomType => RoomType.Monster;

    public override bool HasScene => true;

    public override IReadOnlyList<string> Slots => new[] { "mo", "consta", "arnold" };

    protected override bool HasCustomBackground => true;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new MonsterModel[]
        {
            ModelDb.Monster<Mo>(),
            ModelDb.Monster<Consta>(),
            ModelDb.Monster<Arnold>()
        };

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        return new List<(MonsterModel, string?)>
        {
            (ModelDb.Monster<Mo>().ToMutable(), "mo"),
            (ModelDb.Monster<Consta>().ToMutable(), "consta"),
            (ModelDb.Monster<Arnold>().ToMutable(), "arnold")
        };
    }
}
