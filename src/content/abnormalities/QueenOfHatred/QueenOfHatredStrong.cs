using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using LibraryOfRuina.content.guests;
using LibraryOfRuina.framework.audio;

namespace LibraryOfRuina.content.abnormalities.QueenOfHatred;

public sealed class QueenOfHatredStrong : EncounterModel, IEncounterBgmSource
{
    EncounterBgmConfig IEncounterBgmSource.Bgm => EncounterBgmConfig.RoundBased(
        "QueenOfHatredBGM",
        GuestReceptionPoolRegistry.NaturalReceptionFloorBgmTracks,
        volumeScale: 0.85f,
        GuestReceptionPoolRegistry.StandardRoundThresholds);

    public override RoomType RoomType => RoomType.Monster;

    protected override bool HasCustomBackground => true;

    public override MegaSkeletonDataResource? BossNodeSpineResource => null;

    public override string BossNodePath => "res://images/map/placeholder/queen_of_hatred_boss_icon";

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<QueenOfHatred>()
    ];

    public override IEnumerable<string> ExtraAssetPaths =>
        ModelDb.Monster<QueenOfHatred>().AssetPaths;

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        return
        [
            (ModelDb.Monster<QueenOfHatred>().ToMutable(), null)
        ];
    }
}
