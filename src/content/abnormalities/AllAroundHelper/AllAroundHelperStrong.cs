using LibraryOfRuina.encounters;
using LibraryOfRuina.framework.audio;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.content.abnormalities.AllAroundHelper;

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
        ModelDb.Monster<AllAroundHelper>()
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

    private static AllAroundHelper CreateHelper(AllAroundHelperInitialMove initialMove)
    {
        var helper = (AllAroundHelper)ModelDb.Monster<AllAroundHelper>().ToMutable();
        helper.ConfigureInitialMove(initialMove);
        return helper;
    }
}
