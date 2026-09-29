using LibraryOfRuina.framework.audio;
using LibraryOfRuina.monsters.AddictedEmployee;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.encounters.AddictedEmployee;

public sealed class AddictedEmployeeStrong : EncounterModel, IEncounterBgmSource
{
    EncounterBgmConfig IEncounterBgmSource.Bgm => EncounterBgmConfig.RoundBased(
        "AddictedEmployeeBGM",
        GuestReceptionPoolRegistry.YesodReceptionFloorBgmTracks,
        volumeScale: 0.85f,
        GuestReceptionPoolRegistry.StandardRoundThresholds);

    public override RoomType RoomType => RoomType.Monster;

    public override bool HasScene => true;

    public override IReadOnlyList<string> Slots => new[] { "left", "center", "right" };

    protected override bool HasCustomBackground => true;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<monsters.AddictedEmployee.AddictedEmployee>()
    ];

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        return
        [
            (CreateEmployee(AddictedEmployeeInitialMove.Move1), "left"),
            (CreateEmployee(AddictedEmployeeInitialMove.Move2), "center"),
            (CreateEmployee(AddictedEmployeeInitialMove.Move3), "right")
        ];
    }

    private static monsters.AddictedEmployee.AddictedEmployee CreateEmployee(AddictedEmployeeInitialMove initialMove)
    {
        var employee = (monsters.AddictedEmployee.AddictedEmployee)ModelDb.Monster<monsters.AddictedEmployee.AddictedEmployee>().ToMutable();
        employee.ConfigureInitialMove(initialMove);
        return employee;
    }
}

