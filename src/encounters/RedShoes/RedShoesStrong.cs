using LibraryOfRuina.monsters.RedShoes;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.encounters.RedShoes;

public sealed class RedShoesStrong : EncounterModel
{
    public override RoomType RoomType => RoomType.Monster;

    public override bool HasScene => true;

    public override IReadOnlyList<string> Slots => new[] { "left_shoe", "right_shoe" };

    protected override bool HasCustomBackground => true;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<RedShoesLeft>(),
        ModelDb.Monster<RedShoesRight>()
    ];

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        return
        [
            (ModelDb.Monster<RedShoesLeft>().ToMutable(), "left_shoe"),
            (ModelDb.Monster<RedShoesRight>().ToMutable(), "right_shoe")
        ];
    }
}
