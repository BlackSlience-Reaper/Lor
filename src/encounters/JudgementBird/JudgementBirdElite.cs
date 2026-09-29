using LibraryOfRuina.monsters.JudgementBird;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace LibraryOfRuina.encounters.JudgementBird;

public sealed class JudgementBirdElite : EncounterModel, IEncounterBgmSource
{
    EncounterBgmConfig IEncounterBgmSource.Bgm => EncounterBgmConfig.RoundBased(
        "JudgementBirdBGM",
        GuestReceptionPoolRegistry.PhilosophyReceptionFloorBgmTracks,
        volumeScale: 0.85f,
        GuestReceptionPoolRegistry.StandardRoundThresholds);

    public const string LeftEscapedBirdSlot =
        "judgement_bird_escaped_left";
    public const string RightEscapedBirdSlot =
        "judgement_bird_escaped_right";
    public const string BossSlot = "judgement_bird_boss";

    public override RoomType RoomType => RoomType.Elite;

    public override bool HasScene => true;

    protected override bool HasCustomBackground => true;

    public override IReadOnlyList<string> Slots =>
    [
        LeftEscapedBirdSlot,
        RightEscapedBirdSlot,
        BossSlot
    ];

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<EscapedBird>(),
        ModelDb.Monster<monsters.JudgementBird.JudgementBird>()
    ];

    protected override IReadOnlyList<(MonsterModel, string?)>
        GenerateMonsters() =>
    [
        (ModelDb.Monster<EscapedBird>().ToMutable(),
            LeftEscapedBirdSlot),
        (ModelDb.Monster<EscapedBird>().ToMutable(),
            RightEscapedBirdSlot),
        (ModelDb.Monster<monsters.JudgementBird.JudgementBird>()
            .ToMutable(), BossSlot)
    ];

    public override float GetCameraScaling() => 0.82f;
}
