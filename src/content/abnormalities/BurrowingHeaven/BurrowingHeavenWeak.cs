using LibraryOfRuina.encounters;
using LibraryOfRuina.framework.audio;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.content.abnormalities.BurrowingHeaven;

public sealed class BurrowingHeavenWeak : EncounterModel, IEncounterBgmSource
{
    EncounterBgmConfig IEncounterBgmSource.Bgm => EncounterBgmConfig.RoundBased(
        "BurrowingHeavenBGM",
        GuestReceptionPoolRegistry.ReligionReceptionFloorBgmTracks,
        volumeScale: 0.85f,
        GuestReceptionPoolRegistry.StandardRoundThresholds);

    public const string BossSlot = "burrowing_heaven";
    public const string LeftThornSlot = "heaven_thorn_left";
    public const string RightThornSlot = "heaven_thorn_right";

    public override RoomType RoomType => RoomType.Monster;

    public override bool IsWeak => true;

    public override bool HasScene => true;

    protected override bool HasCustomBackground => true;

    public override IReadOnlyList<string> Slots => [LeftThornSlot, BossSlot, RightThornSlot];

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<BurrowingHeaven>(),
        ModelDb.Monster<HeavenThorn>()
    ];

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        var boss = (BurrowingHeaven)ModelDb.Monster<BurrowingHeaven>().ToMutable();
        var leftThorn = (HeavenThorn)ModelDb.Monster<HeavenThorn>().ToMutable();
        var rightThorn = (HeavenThorn)ModelDb.Monster<HeavenThorn>().ToMutable();

        boss.SetInitialAwake(false);
        leftThorn.SetInitialAwake(true);
        rightThorn.SetInitialAwake(false);

        return
        [
            (boss, BossSlot),
            (leftThorn, LeftThornSlot),
            (rightThorn, RightThornSlot)
        ];
    }

    public override float GetCameraScaling() => 0.88f;
}
