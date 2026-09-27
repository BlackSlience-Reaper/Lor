using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.encounters.Tomerry;

public sealed class TomerryEncounter : EncounterModel
{
    public override RoomType RoomType => RoomType.Elite;

    public override bool HasScene => true;

    public override IReadOnlyList<string> Slots => new[] { "tomerry" };

    protected override bool HasCustomBackground => true;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new MonsterModel[]
        {
            ModelDb.Monster<monsters.Tomerry.Tomerry>()
        };

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        return
        [
            (ModelDb.Monster<monsters.Tomerry.Tomerry>().ToMutable(), "tomerry")
        ];
    }
}
