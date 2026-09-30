using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.content.abnormalities.Nosferatu;

public sealed class NosferatuElite : EncounterModel
{
    public const string LeftBatSlot = "blood_bat_left";
    public const string RightBatSlot = "blood_bat_right";
    public const string BossSlot = "nosferatu";

    public static readonly string[] BatSlots = [LeftBatSlot, RightBatSlot];

    public override RoomType RoomType => RoomType.Elite;

    public override bool HasScene => true;

    public override IReadOnlyList<string> Slots =>
    [
        LeftBatSlot,
        BossSlot,
        RightBatSlot
    ];

    public override float GetCameraScaling() => 0.82f;

    public override Vector2 GetCameraOffset() => Vector2.Down * 50f + Vector2.Left * 100f;

    protected override bool HasCustomBackground => true;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<Nosferatu>(),
        ModelDb.Monster<BloodBat>()
    ];

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        return
        [
            (ModelDb.Monster<BloodBat>().ToMutable(), LeftBatSlot),
            (ModelDb.Monster<Nosferatu>().ToMutable(), BossSlot),
            (ModelDb.Monster<BloodBat>().ToMutable(), RightBatSlot)
        ];
    }
}
