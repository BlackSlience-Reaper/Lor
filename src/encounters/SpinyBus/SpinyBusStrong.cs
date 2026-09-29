using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.encounters.SpinyBus;

public sealed class SpinyBusWeak : EncounterModel, IEncounterBgmSource
{
    EncounterBgmConfig IEncounterBgmSource.Bgm => EncounterBgmConfig.RoundBased(
        "SpinyBusBGM",
        GuestReceptionPoolRegistry.NetzachReceptionFloorBgmTracks,
        volumeScale: 0.85f,
        GuestReceptionPoolRegistry.StandardRoundThresholds);

    public override RoomType RoomType => RoomType.Monster;

    public override bool IsWeak => false;

    public override bool HasScene => true;

    public override IReadOnlyList<string> Slots => new[] { "bus" };

    protected override bool HasCustomBackground => true;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<monsters.SpinyBus.SpinyBus>()
    ];

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        return
        [
            (ModelDb.Monster<monsters.SpinyBus.SpinyBus>().ToMutable(), "bus")
        ];
    }
}
