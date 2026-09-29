using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.content.abnormalities.KingOfGreed;

public sealed class KingOfGreedElite : EncounterModel
{
    public const string BossSlot = "king_of_greed";
    public const string HappinessLeftSlot = "happiness_left";
    public const string HappinessRightSlot = "happiness_right";

    public static readonly string[] HappinessSlots = [HappinessLeftSlot, HappinessRightSlot];

    public override RoomType RoomType => RoomType.Elite;

    public override bool HasScene => true;

    public override IReadOnlyList<string> Slots =>
    [
        HappinessLeftSlot,
        HappinessRightSlot,
        BossSlot
    ];

    public override float GetCameraScaling() => 0.82f;

    public override Vector2 GetCameraOffset() => Vector2.Down * 50f + Vector2.Left * 100f;

    protected override bool HasCustomBackground => true;

    public override MegaSkeletonDataResource? BossNodeSpineResource => null;

    public override string BossNodePath => "res://images/map/placeholder/king_of_greed_boss_icon";

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<GoldenAmber>(),
        ModelDb.Monster<KingOfGreed>(),
        ModelDb.Monster<ShiningHappiness>()
    ];

    public override IEnumerable<string> ExtraAssetPaths =>
        AllPossibleMonsters
            .SelectMany(static monster => monster.AssetPaths)
            .Distinct();

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        return [(ModelDb.Monster<GoldenAmber>().ToMutable(), BossSlot)];
    }
}
