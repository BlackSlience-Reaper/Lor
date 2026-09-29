using LibraryOfRuina.framework.audio;
using LibraryOfRuina.monsters.DeadButterfly;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.encounters.DeadButterfly;

public sealed class DeadButterflyWeak : EncounterModel, IEncounterBgmSource
{
    EncounterBgmConfig IEncounterBgmSource.Bgm => EncounterBgmConfig.RoundBased(
        "DeadButterflyBGM",
        GuestReceptionPoolRegistry.LiteratureReceptionFloorBgmTracks,
        volumeScale: 0.85f,
        GuestReceptionPoolRegistry.StandardRoundThresholds);

    public override RoomType RoomType => RoomType.Monster;

    public override bool IsWeak => true;

    public override bool HasScene => true;

    public override IReadOnlyList<string> Slots => ["left_butterfly", "middle_butterfly", "right_butterfly"];

    protected override bool HasCustomBackground => true;

    public override IEnumerable<MonsterModel> AllPossibleMonsters => [ModelDb.Monster<monsters.DeadButterfly.DeadButterfly>()];

    public override IEnumerable<string> ExtraAssetPaths =>
    [
        "res://images/backgrounds/dead_butterfly/funeral_background.png"
    ];

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        return
        [
            (Create(DeadButterflyInitialMove.AngryRelease, DeadButterflyInitialMove.SpiritRelease, DeadButterflyInitialMove.PainfulRelease, DeadButterflyInitialMove.PeacefulRepose), "left_butterfly"),
            (Create(DeadButterflyInitialMove.SpiritRelease, DeadButterflyInitialMove.PeacefulRepose, DeadButterflyInitialMove.PainfulRelease, DeadButterflyInitialMove.AngryRelease), "middle_butterfly"),
            (Create(DeadButterflyInitialMove.PainfulRelease, DeadButterflyInitialMove.AngryRelease, DeadButterflyInitialMove.PeacefulRepose, DeadButterflyInitialMove.SpiritRelease), "right_butterfly")
        ];
    }

    private static monsters.DeadButterfly.DeadButterfly Create(params DeadButterflyInitialMove[] sequence)
    {
        var monster = (monsters.DeadButterfly.DeadButterfly)ModelDb.Monster<monsters.DeadButterfly.DeadButterfly>().ToMutable();
        monster.ConfigureMoveSequence(sequence);
        return monster;
    }
}
