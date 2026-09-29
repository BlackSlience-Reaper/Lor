using System.Linq;
using Godot;
using LibraryOfRuina.monsters.DespairKnight;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.encounters.DespairKnight;

public sealed class DespairKnightStrong : EncounterModel, IEncounterBgmSource
{
    EncounterBgmConfig IEncounterBgmSource.Bgm => EncounterBgmConfig.RoundBased(
        "DespairKnightBGM",
        GuestReceptionPoolRegistry.NaturalReceptionFloorBgmTracks,
        volumeScale: 0.85f,
        GuestReceptionPoolRegistry.StandardRoundThresholds);

    public const string SwordSlotOne = "sword_1";
    public const string SwordSlotTwo = "sword_2";
    public const string BossSlot = "despair_knight";
    public const string SwordSlotThree = "sword_3";

    private const float CameraScaling = 0.82f;
    private static readonly Vector2 CameraOffset = Vector2.Down * 50f + Vector2.Left * 100f;

    public override RoomType RoomType => RoomType.Monster;

    public override bool HasScene => true;

    public override IReadOnlyList<string> Slots => [SwordSlotOne, SwordSlotTwo, SwordSlotThree, BossSlot];

    protected override bool HasCustomBackground => true;

    public override float GetCameraScaling() => CameraScaling;

    public override Vector2 GetCameraOffset() => CameraOffset;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<monsters.DespairKnight.DespairKnight>(),
        ModelDb.Monster<ForgottenKnightSword>()
    ];

    public override IEnumerable<string> ExtraAssetPaths =>
        monsters.DespairKnight.DespairKnight.AssetPathsStatic
            .Concat(ForgottenKnightSword.AssetPathsStatic)
            .Concat(GuestReceptionPoolRegistry.NaturalReceptionFloorBgmTracks)
            .Concat(new[]
            {
                "res://images/backgrounds/despair_knight_strong/despair_knight_background.png",
                "res://scenes/backgrounds/despair_knight_strong/despair_knight_strong_background.tscn",
                "res://scenes/backgrounds/despair_knight_strong/layers/despair_knight_strong_bg_00_a.tscn"
            })
            .Distinct();

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        return
        [
            (CreateSword(0), SwordSlotOne),
            (CreateSword(1), SwordSlotTwo),
            (CreateSword(2), SwordSlotThree),
            (ModelDb.Monster<monsters.DespairKnight.DespairKnight>().ToMutable(), BossSlot)
        ];
    }

    private static ForgottenKnightSword CreateSword(int index)
    {
        var sword = (ForgottenKnightSword)ModelDb.Monster<ForgottenKnightSword>().ToMutable();
        sword.ConfigureSwordIndex(index);
        return sword;
    }
}
