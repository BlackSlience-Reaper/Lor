using LibraryOfRuina.monsters.ScorchedGirl;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.encounters.ScorchedGirl;

public sealed class ScorchedGirl : EncounterModel
{
    public override RoomType RoomType => RoomType.Elite;

    public override bool HasScene => true;

    public override IReadOnlyList<string> Slots => new[] { "match_left", "match_right", "girl" };

    protected override bool HasCustomBackground => true;

    public override IEnumerable<string> ExtraAssetPaths =>
        [ImageHelper.GetImagePath("powers/missing_power.png")];

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        new MonsterModel[]
        {
            ModelDb.Monster<TheFourthMatchFlame>(),
            ModelDb.Monster<ScorchedGirlMonster>()
        };

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        var leftMatch = (TheFourthMatchFlame)ModelDb.Monster<TheFourthMatchFlame>().ToMutable();
        var rightMatch = (TheFourthMatchFlame)ModelDb.Monster<TheFourthMatchFlame>().ToMutable();

        bool leftStartsWithBrokenHope = Rng.NextBool();
        leftMatch.StartsWithBrokenHope = leftStartsWithBrokenHope;
        rightMatch.StartsWithBrokenHope = !leftStartsWithBrokenHope;

        return new List<(MonsterModel, string?)>
        {
            (leftMatch, "match_left"),
            (rightMatch, "match_right"),
            (ModelDb.Monster<ScorchedGirlMonster>().ToMutable(), "girl")
        };
    }
}
