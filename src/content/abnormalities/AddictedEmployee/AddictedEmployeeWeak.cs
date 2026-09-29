using LibraryOfRuina.content.guests;
using LibraryOfRuina.framework.audio;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.content.abnormalities.AddictedEmployee;

public sealed class AddictedEmployeeWeak : EncounterModel, IEncounterBgmSource
{
    EncounterBgmConfig IEncounterBgmSource.Bgm => EncounterBgmConfig.RoundBased(
        "AddictedEmployeeBGM",
        GuestReceptionPoolRegistry.YesodReceptionFloorBgmTracks,
        volumeScale: 0.85f,
        GuestReceptionPoolRegistry.StandardRoundThresholds);

    public override RoomType RoomType => RoomType.Monster;

    public override bool IsWeak => true;

    public override bool HasScene => true;

    public override IReadOnlyList<string> Slots => new[] { "left", "right" };

    protected override bool HasCustomBackground => true;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<AddictedEmployee>()
    ];

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        return
        [
            (CreateEmployee(AddictedEmployeeInitialMove.Move1), "left"),
            (CreateEmployee(AddictedEmployeeInitialMove.Move2), "right")
        ];
    }

    private static AddictedEmployee CreateEmployee(AddictedEmployeeInitialMove initialMove)
    {
        var employee = (AddictedEmployee)ModelDb.Monster<AddictedEmployee>().ToMutable();
        employee.ConfigureInitialMove(initialMove);
        return employee;
    }
}

