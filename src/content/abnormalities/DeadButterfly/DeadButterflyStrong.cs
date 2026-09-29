using LibraryOfRuina.encounters;
using LibraryOfRuina.framework.audio;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.content.abnormalities.DeadButterfly;

public sealed class DeadButterflyStrong : EncounterModel, IEncounterBgmSource
{
    EncounterBgmConfig IEncounterBgmSource.Bgm => EncounterBgmConfig.RoundBased(
        "DeadButterflyBGM",
        GuestReceptionPoolRegistry.LiteratureReceptionFloorBgmTracks,
        volumeScale: 0.85f,
        GuestReceptionPoolRegistry.StandardRoundThresholds);

    public override RoomType RoomType => RoomType.Monster;

    public override bool HasScene => true;

    public override IReadOnlyList<string> Slots => ["left_butterfly", "middle_left_butterfly", "middle_right_butterfly", "right_butterfly"];

    protected override bool HasCustomBackground => true;

    public override IEnumerable<MonsterModel> AllPossibleMonsters => [ModelDb.Monster<DeadButterfly>()];

    public override IEnumerable<string> ExtraAssetPaths =>
    [
        "res://images/backgrounds/dead_butterfly/funeral_background.png"
    ];

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        return
        [
            (Create(DeadButterflyInitialMove.AngryRelease, DeadButterflyInitialMove.SpiritRelease, DeadButterflyInitialMove.PainfulRelease, DeadButterflyInitialMove.PeacefulRepose), "left_butterfly"),
            (Create(DeadButterflyInitialMove.PainfulRelease, DeadButterflyInitialMove.AngryRelease, DeadButterflyInitialMove.PeacefulRepose, DeadButterflyInitialMove.SpiritRelease), "middle_left_butterfly"),
            (Create(DeadButterflyInitialMove.SpiritRelease, DeadButterflyInitialMove.PeacefulRepose, DeadButterflyInitialMove.PainfulRelease, DeadButterflyInitialMove.AngryRelease), "middle_right_butterfly"),
            (Create(DeadButterflyInitialMove.AngryRelease, DeadButterflyInitialMove.PainfulRelease, DeadButterflyInitialMove.PeacefulRepose, DeadButterflyInitialMove.SpiritRelease), "right_butterfly")
        ];
    }

    private static DeadButterfly Create(params DeadButterflyInitialMove[] sequence)
    {
        var monster = (DeadButterfly)ModelDb.Monster<DeadButterfly>().ToMutable();
        monster.ConfigureMoveSequence(sequence);
        return monster;
    }
}
