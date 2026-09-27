using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.encounters.QueenOfHatred;

public sealed class QueenOfHatredStrong : EncounterModel
{
    public override RoomType RoomType => RoomType.Monster;

    protected override bool HasCustomBackground => true;

    public override MegaSkeletonDataResource? BossNodeSpineResource => null;

    public override string BossNodePath => "res://images/map/placeholder/queen_of_hatred_boss_icon";

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<monsters.QueenOfHatred.QueenOfHatred>()
    ];

    public override IEnumerable<string> ExtraAssetPaths =>
        ModelDb.Monster<monsters.QueenOfHatred.QueenOfHatred>().AssetPaths;

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        return
        [
            (ModelDb.Monster<monsters.QueenOfHatred.QueenOfHatred>().ToMutable(), null)
        ];
    }
}
