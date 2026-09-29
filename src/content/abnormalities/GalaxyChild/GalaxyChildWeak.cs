using LibraryOfRuina.encounters;
using LibraryOfRuina.framework.audio;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.content.abnormalities.GalaxyChild;

public sealed class GalaxyChildWeak : EncounterModel, IEncounterBgmSource
{
    EncounterBgmConfig IEncounterBgmSource.Bgm => EncounterBgmConfig.RoundBased(
        "GalaxyChildBGM",
        GuestReceptionPoolRegistry.NetzachReceptionFloorBgmTracks,
        volumeScale: 0.85f,
        GuestReceptionPoolRegistry.StandardRoundThresholds);

    public override RoomType RoomType => RoomType.Monster;

    public override bool IsWeak => true;

    public override bool HasScene => true;

    public override IReadOnlyList<string> Slots => new[] { "left", "right" };

    protected override bool HasCustomBackground => true;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<GalaxyFriend>()
    ];

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        return
        [
            (CreateFriend(GalaxyFriendInitialMove.Wait), "left"),
            (CreateFriend(GalaxyFriendInitialMove.StarlightFall), "right")
        ];
    }

    private static GalaxyFriend CreateFriend(GalaxyFriendInitialMove initialMove)
    {
        var friend = (GalaxyFriend)ModelDb.Monster<GalaxyFriend>().ToMutable();
        friend.ConfigureInitialMove(initialMove);
        return friend;
    }
}
