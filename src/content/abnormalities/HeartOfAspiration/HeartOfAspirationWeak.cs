using LibraryOfRuina.encounters;
using LibraryOfRuina.framework.audio;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.content.abnormalities.HeartOfAspiration;

public sealed class HeartOfAspirationWeak : EncounterModel, IEncounterBgmSource
{
    EncounterBgmConfig IEncounterBgmSource.Bgm => EncounterBgmConfig.RoundBased(
        "HeartOfAspirationBGM",
        GuestReceptionPoolRegistry.GeneralReceptionFloorBgmTracks,
        volumeScale: 0.85f,
        GuestReceptionPoolRegistry.StandardRoundThresholds);

    public const string LungSlot = "lung_left";
    public const string HeartSlot = "heart_right";

    public override RoomType RoomType => RoomType.Monster;

    public override bool IsWeak => true;

    public override bool HasScene => true;

    public override bool FullyCenterPlayers => true;

    protected override bool HasCustomBackground => true;

    public override IReadOnlyList<string> Slots => [LungSlot, HeartSlot];

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<LungOfAspiration>(),
        ModelDb.Monster<HeartOfAspiration>()
    ];

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        return
        [
            (ModelDb.Monster<LungOfAspiration>().ToMutable(), LungSlot),
            (ModelDb.Monster<HeartOfAspiration>().ToMutable(), HeartSlot)
        ];
    }

    public override float GetCameraScaling() => 0.9f;
}
