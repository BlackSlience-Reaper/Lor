using System.Linq;
using LibraryOfRuina.afflictions.FuneralOfTheDeadButterflies;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.encounters.FuneralOfTheDeadButterflies;

public sealed class FuneralOfTheDeadButterfliesEncounter : EncounterModel
{
    public override RoomType RoomType => RoomType.Elite;

    public override bool ShouldGiveRewards => true;

    public override bool HasScene => true;

    public override IReadOnlyList<string> Slots =>
    [
        "butterfly_left",
        "butterfly_middle_left",
        "butterfly_middle_right",
        "butterfly_right",
        "boss"
    ];

    protected override bool HasCustomBackground => true;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<monsters.FuneralOfTheDeadButterflies.FuneralOfTheDeadButterflies>(),
        ModelDb.Monster<monsters.DeadButterfly.DeadButterfly>()
    ];

    public override IEnumerable<string> ExtraAssetPaths
    {
        get
        {
            var paths = new List<string>
            {
                "res://images/backgrounds/dead_butterfly/funeral_background.png",
                ModelDb.Affliction<FuneralSealAffliction>().OverlayPath
            };
            paths.AddRange(ModelDb.Monster<monsters.FuneralOfTheDeadButterflies.FuneralOfTheDeadButterflies>().AssetPaths);
            paths.AddRange(ModelDb.Monster<monsters.DeadButterfly.DeadButterfly>().AssetPaths);
            return paths.Distinct();
        }
    }

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        return
        [
            (ModelDb.Monster<monsters.FuneralOfTheDeadButterflies.FuneralOfTheDeadButterflies>().ToMutable(), "boss")
        ];
    }
}
