using LibraryOfRuina.monsters.HeartOfAspiration;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.encounters.HeartOfAspiration;

public sealed class HeartOfAspirationWeak : EncounterModel
{
    public const string LungSlot = "lung_left";
    public const string HeartSlot = "heart_right";

    public override RoomType RoomType => RoomType.Monster;

    public override bool IsWeak => true;

    public override bool HasScene => true;

    public override bool FullyCenterPlayers => true;

    protected override bool HasCustomBackground => true;

    public override IReadOnlyList<string> Slots => [LungSlot, HeartSlot];

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<LungOfAspiration>(),
        ModelDb.Monster<monsters.HeartOfAspiration.HeartOfAspiration>()
    ];

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        return
        [
            (ModelDb.Monster<LungOfAspiration>().ToMutable(), LungSlot),
            (ModelDb.Monster<monsters.HeartOfAspiration.HeartOfAspiration>().ToMutable(), HeartSlot)
        ];
    }

    public override float GetCameraScaling() => 0.9f;
}
