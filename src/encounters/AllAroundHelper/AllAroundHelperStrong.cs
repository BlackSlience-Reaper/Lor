using LibraryOfRuina.monsters.AllAroundHelper;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.encounters.AllAroundHelper;

public sealed class AllAroundHelperStrong : EncounterModel, IEncounterBgmSource
{
    EncounterBgmConfig IEncounterBgmSource.Bgm => EncounterBgmConfig.RoundBased(
        "AllAroundHelperBGM",
        GuestReceptionPoolRegistry.YesodReceptionFloorBgmTracks,
        volumeScale: 0.85f,
        GuestReceptionPoolRegistry.StandardRoundThresholds);

    public override RoomType RoomType => RoomType.Monster;

    public override bool HasScene => true;

    public override IReadOnlyList<string> Slots => new[] { "left", "center", "right" };

    protected override bool HasCustomBackground => true;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<monsters.AllAroundHelper.AllAroundHelper>()
    ];

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        return
        [
            (CreateHelper(AllAroundHelperInitialMove.Charge), "left"),
            (CreateHelper(AllAroundHelperInitialMove.Clean), "center"),
            (CreateHelper(AllAroundHelperInitialMove.Rest), "right")
        ];
    }

    private static monsters.AllAroundHelper.AllAroundHelper CreateHelper(AllAroundHelperInitialMove initialMove)
    {
        var helper = (monsters.AllAroundHelper.AllAroundHelper)ModelDb.Monster<monsters.AllAroundHelper.AllAroundHelper>().ToMutable();
        helper.ConfigureInitialMove(initialMove);
        return helper;
    }
}
