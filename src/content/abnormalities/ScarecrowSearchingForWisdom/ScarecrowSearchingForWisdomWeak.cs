using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using LibraryOfRuina.content.guests;
using LibraryOfRuina.framework.audio;

namespace LibraryOfRuina.content.abnormalities.ScarecrowSearchingForWisdom;

public sealed class ScarecrowSearchingForWisdomWeak : EncounterModel, IEncounterBgmSource
{
    EncounterBgmConfig IEncounterBgmSource.Bgm => EncounterBgmConfig.RoundBased(
        "ScarecrowSearchingForWisdomBGM",
        GuestReceptionPoolRegistry.SocialSciencesReceptionFloorBgmTracks,
        volumeScale: 0.85f,
        GuestReceptionPoolRegistry.StandardRoundThresholds);

    public const string LeftSlot = "scarecrow_1";
    public const string CenterSlot = "scarecrow_2";
    public const string RightSlot = "scarecrow_3";

    public override RoomType RoomType => RoomType.Monster;

    public override bool IsWeak => true;

    public override bool HasScene => true;

    public override IReadOnlyList<string> Slots => [LeftSlot, CenterSlot, RightSlot];

    protected override bool HasCustomBackground => true;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<ScarecrowSearchingForWisdom>()
    ];

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        ScarecrowSearchingForWisdom left = (ScarecrowSearchingForWisdom)ModelDb.Monster<ScarecrowSearchingForWisdom>().ToMutable();
        ScarecrowSearchingForWisdom center = (ScarecrowSearchingForWisdom)ModelDb.Monster<ScarecrowSearchingForWisdom>().ToMutable();
        ScarecrowSearchingForWisdom right = (ScarecrowSearchingForWisdom)ModelDb.Monster<ScarecrowSearchingForWisdom>().ToMutable();

        left.SetFormationIndex(1);
        center.SetFormationIndex(2);
        right.SetFormationIndex(3);

        return
        [
            (center, CenterSlot),
            (left, LeftSlot),
            (right, RightSlot)
        ];
    }

    public override float GetCameraScaling() => 0.9f;
}
